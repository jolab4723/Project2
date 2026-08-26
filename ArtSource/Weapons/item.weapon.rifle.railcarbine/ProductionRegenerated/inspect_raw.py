import bpy
import json
from pathlib import Path
from mathutils import Matrix, Vector


ROOT = Path(__file__).resolve().parents[1]
GLB = ROOT / "Tripo" / "Downloaded" / "item.weapon.rifle.railcarbine_raw.glb"
OUT = Path(__file__).resolve().parent
QA = OUT / "RawQA"
TEXTURES = OUT / "RawTextures"


def v3(value):
    return [round(float(value[i]), 7) for i in range(3)]


def bounds(obj):
    points = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    return (
        Vector((min(point[i] for point in points) for i in range(3))),
        Vector((max(point[i] for point in points) for i in range(3))),
    )


def triangles(obj):
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def look_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def orient_camera(camera, target):
    target = Vector(target)
    direction = (target - camera.location).normalized()
    distance = (camera.location - target).length
    up = Vector((0.0, 0.0, 1.0))
    if abs(direction.dot(up)) > 0.98:
        up = Vector((0.0, 1.0, 0.0))
    right = direction.cross(up).normalized()
    up = right.cross(direction).normalized()
    camera.matrix_world = Matrix((right, up, -direction)).transposed().to_4x4()
    camera.location = target - direction * distance


QA.mkdir(parents=True, exist_ok=True)
TEXTURES.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(GLB))
meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
if not meshes:
    raise RuntimeError("GLB contains no mesh")

for image in bpy.data.images:
    prefix = image.name.split("_")[0]
    suffix = {"Color": "BaseColor", "NormalGL": "Normal", "ORM": "ORM"}.get(prefix)
    if not suffix:
        continue
    target = TEXTURES / f"item.weapon.rifle.railcarbine_{suffix}.png"
    image.colorspace_settings.name = "sRGB" if suffix == "BaseColor" else "Non-Color"
    image.filepath_raw = str(target)
    image.file_format = "PNG"
    image.save()

minimum = Vector((min(bounds(obj)[0][i] for obj in meshes) for i in range(3)))
maximum = Vector((max(bounds(obj)[1][i] for obj in meshes) for i in range(3)))
center = (minimum + maximum) * 0.5
longest = max(maximum - minimum)

world = bpy.data.worlds.new("RawQAWorld")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.025, 0.025, 0.025, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.35
bpy.context.scene.world = world

for name, offset, energy, size in (
    ("Key", (-1.2, -1.4, 1.3), 850, 2.0),
    ("Fill", (1.3, -0.8, 0.5), 550, 1.5),
    ("Rim", (0.0, 1.4, 1.0), 700, 1.2),
):
    data = bpy.data.lights.new(name, "AREA")
    data.energy, data.shape, data.size = energy, "DISK", size
    light = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(light)
    light.location = center + Vector(offset) * longest
    look_at(light, center)

camera_data = bpy.data.cameras.new("RawQACamera")
camera_data.type = "ORTHO"
camera_data.ortho_scale = longest * 1.35
camera = bpy.data.objects.new("RawQACamera", camera_data)
bpy.context.collection.objects.link(camera)
bpy.context.scene.camera = camera

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1024
scene.render.resolution_y = 768
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.film_transparent = True
scene.view_settings.look = "AgX - Medium High Contrast"

distance = longest * 3.0
views = {
    "side_negative_y": Vector((0, -distance, 0)),
    "side_positive_y": Vector((0, distance, 0)),
    "end_positive_x": Vector((distance, 0, 0)),
    "end_negative_x": Vector((-distance, 0, 0)),
    "top_positive_z": Vector((0, 0, distance)),
    "bottom_negative_z": Vector((0, 0, -distance)),
    "iso_negative_y": Vector((distance * 0.72, -distance * 0.66, distance * 0.58)),
    "iso_positive_y": Vector((distance * 0.72, distance * 0.66, distance * 0.58)),
}
for name, offset in views.items():
    camera.location = center + offset
    orient_camera(camera, center)
    if name.startswith("end_"):
        camera.data.ortho_scale = max((maximum - minimum).y, (maximum - minimum).z) * 1.65
    else:
        camera.data.ortho_scale = longest * (1.52 if name.startswith("iso") else 1.35)
    scene.render.filepath = str(QA / f"{name}.png")
    bpy.ops.render.render(write_still=True)

payload = {
    "source": str(GLB),
    "task_id": "73ed8f0b-00e9-415f-81be-36baa9bdd9d4",
    "bounds": {"min": v3(minimum), "max": v3(maximum), "dimensions": v3(maximum - minimum)},
    "objects": [
        {
            "name": obj.name,
            "vertices": len(obj.data.vertices),
            "polygons": len(obj.data.polygons),
            "triangles": triangles(obj),
            "materials": [slot.material.name if slot.material else None for slot in obj.material_slots],
            "uv_layers": [layer.name for layer in obj.data.uv_layers],
            "location": v3(obj.location),
            "rotation": v3(obj.rotation_euler),
            "scale": v3(obj.scale),
        }
        for obj in meshes
    ],
    "images": [
        {"name": image.name, "size": list(image.size), "colorspace": image.colorspace_settings.name, "packed": image.packed_file is not None}
        for image in bpy.data.images
    ],
    "qa_views": {name: str(QA / f"{name}.png") for name in views},
    "visual_gate": "pending direct inspection",
}
(OUT / "raw_inspection.json").write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(payload, ensure_ascii=False, indent=2))
