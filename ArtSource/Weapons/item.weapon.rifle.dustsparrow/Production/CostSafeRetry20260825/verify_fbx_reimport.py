import hashlib
import json
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


values = sys.argv[sys.argv.index("--") + 1 :]
fbx_path = Path(values[values.index("--fbx") + 1]).resolve()
output_dir = Path(values[values.index("--output") + 1]).resolve()
output_dir.mkdir(parents=True, exist_ok=True)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def point_at(obj, target: Vector) -> None:
    forward = (target - obj.location).normalized()
    reference_up = Vector((0.0, 1.0, 0.0))
    if abs(forward.dot(reference_up)) > 0.98:
        reference_up = Vector((0.0, 0.0, 1.0))
    right = forward.cross(reference_up).normalized()
    corrected_up = right.cross(forward).normalized()
    obj.rotation_euler = Matrix((right, corrected_up, -forward)).transposed().to_euler()


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(fbx_path), use_anim=False)
scene = bpy.context.scene
objects = list(scene.objects)
meshes = [obj for obj in objects if obj.type == "MESH"]
empties = [obj for obj in objects if obj.type == "EMPTY"]
if len(meshes) != 1:
    raise RuntimeError(f"Expected one reimported mesh, found {len(meshes)}")
mesh = meshes[0]
mesh.data.calc_loop_triangles()

named = {obj.name: obj for obj in objects}
required = {
    "item.weapon.rifle.dustsparrow",
    "RightHandGrip",
    "LeftHandGrip",
    "Muzzle",
}
missing = sorted(required - set(named))
if missing:
    raise RuntimeError(f"Missing FBX hierarchy objects: {missing}")

root = named["item.weapon.rifle.dustsparrow"]
right = named["RightHandGrip"]
left = named["LeftHandGrip"]
muzzle = named["Muzzle"]

def transform_record(obj):
    return {
        "location": [round(value, 6) for value in obj.location],
        "rotation_euler": [round(value, 6) for value in obj.rotation_euler],
        "scale": [round(value, 6) for value in obj.scale],
    }


records = {
    "fbx": str(fbx_path),
    "fbx_sha256": sha256(fbx_path),
    "blender_version": bpy.app.version_string,
    "objects": [{"name": obj.name, "type": obj.type, "parent": obj.parent.name if obj.parent else None} for obj in objects],
    "mesh_count": len(meshes),
    "mesh_triangles": len(mesh.data.loop_triangles),
    "material_count": len(mesh.data.materials),
    "uv_layers": [layer.name for layer in mesh.data.uv_layers],
    "root": transform_record(root),
    "right_hand_grip": transform_record(right),
    "left_hand_grip": transform_record(left),
    "muzzle": transform_record(muzzle),
    "muzzle_world_plus_z": [round(value, 6) for value in (muzzle.matrix_world.to_3x3() @ Vector((0.0, 0.0, 1.0))).normalized()],
    "forbidden_objects": [obj.name for obj in objects if obj.type in {"CAMERA", "LIGHT", "ARMATURE"}],
    "checks": {
        "required_hierarchy_present": not missing,
        "single_mesh": len(meshes) == 1,
        "right_grip_at_origin": right.matrix_world.translation.length < 1e-5,
        "muzzle_forward_plus_z": (muzzle.matrix_world.to_3x3() @ Vector((0.0, 0.0, 1.0))).normalized().dot(Vector((0.0, 0.0, 1.0))) > 0.999,
        "uniform_positive_scales": all(min(obj.scale) > 0 and max(obj.scale) - min(obj.scale) < 1e-5 for obj in objects),
        "no_forbidden_objects": not any(obj.type in {"CAMERA", "LIGHT", "ARMATURE"} for obj in objects),
        "pbr_material_preserved": len(mesh.data.materials) == 1 and len(mesh.data.uv_layers) >= 1,
    },
}
(output_dir / "fbx_reimport_validation.json").write_text(json.dumps(records, indent=2), encoding="utf-8")
print(json.dumps(records, indent=2))

# Render the empty-scene reimport as independent visual evidence.
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1024
scene.render.resolution_y = 512
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.film_transparent = True
scene.view_settings.look = "AgX - Medium High Contrast"

camera_data = bpy.data.cameras.new("Reimport_QA_Camera")
camera_data.type = "ORTHO"
camera = bpy.data.objects.new("Reimport_QA_Camera", camera_data)
scene.collection.objects.link(camera)
scene.camera = camera

for name, location, energy in (
    ("Reimport_Key", (-1.2, 1.4, 0.3), 750.0),
    ("Reimport_Fill", (1.0, 0.8, 0.4), 380.0),
):
    light_data = bpy.data.lights.new(name, type="AREA")
    light_data.energy = energy
    light_data.size = 2.0
    light = bpy.data.objects.new(name, light_data)
    scene.collection.objects.link(light)
    light.location = location
    point_at(light, Vector((0.0, 0.03, 0.18)))

corners = [mesh.matrix_world @ Vector(corner) for corner in mesh.bound_box]
minimum = Vector(tuple(min(point[index] for point in corners) for index in range(3)))
maximum = Vector(tuple(max(point[index] for point in corners) for index in range(3)))
center = (minimum + maximum) * 0.5
for name, direction in {
    "left": Vector((-1.0, 0.0, 0.0)),
    "front_muzzle": Vector((0.0, 0.0, 1.0)),
    "iso": Vector((-0.65, 0.45, 0.8)).normalized(),
}.items():
    camera.location = center + direction * 2.2
    point_at(camera, center)
    bpy.context.view_layer.update()
    inverse = camera.matrix_world.inverted()
    projected = [inverse @ point for point in corners]
    width = max(point.x for point in projected) - min(point.x for point in projected)
    height = max(point.y for point in projected) - min(point.y for point in projected)
    camera.data.ortho_scale = max(height * 1.22, width / 2.0 * 1.22)
    scene.render.filepath = str(output_dir / f"reimport_{name}.png")
    bpy.ops.render.render(write_still=True)
