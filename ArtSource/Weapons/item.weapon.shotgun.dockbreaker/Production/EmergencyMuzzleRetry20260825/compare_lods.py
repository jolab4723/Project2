import json
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


ITEM_ID = "item.weapon.shotgun.dockbreaker"
ITEM_DIR = Path(__file__).resolve().parents[2]
RAW_GLB = ITEM_DIR / "Tripo" / "EmergencyMuzzleRetry20260825" / "Downloaded" / f"{ITEM_ID}_raw.glb"
OUTPUT_DIR = Path(__file__).resolve().parent / "QA" / "LODComparison"
TARGETS = [1440974, 700000, 500000, 350000, 250000]
RAW_GRIP_CENTER = Vector((-0.153, 0.0, -0.053))
RAW_MUZZLE_CENTER = Vector((0.5, 0.0, 0.015))


def point_at(obj, target: Vector) -> None:
    forward = (target - obj.location).normalized()
    reference_up = Vector((0.0, 0.0, 1.0))
    if abs(forward.dot(reference_up)) > 0.98:
        reference_up = Vector((0.0, 1.0, 0.0))
    right = forward.cross(reference_up).normalized()
    corrected_up = right.cross(forward).normalized()
    obj.rotation_euler = Matrix((right, corrected_up, -forward)).transposed().to_euler()


OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
records = []
for target in TARGETS:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(RAW_GLB))
    scene = bpy.context.scene
    mesh = next(obj for obj in scene.objects if obj.type == "MESH")
    mesh.data.calc_loop_triangles()
    raw_triangles = len(mesh.data.loop_triangles)
    if target < raw_triangles:
        modifier = mesh.modifiers.new(name=f"LOD_{target}", type="DECIMATE")
        modifier.decimate_type = "COLLAPSE"
        modifier.ratio = target / raw_triangles
        modifier.use_collapse_triangulate = True
        try:
            modifier.delimit = {"UV"}
        except (AttributeError, TypeError):
            pass
        bpy.context.view_layer.objects.active = mesh
        mesh.select_set(True)
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    mesh.data.calc_loop_triangles()
    actual = len(mesh.data.loop_triangles)

    destination = OUTPUT_DIR / ("raw" if target == raw_triangles else f"{target // 1000}k")
    destination.mkdir(parents=True, exist_ok=True)
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1024
    scene.render.resolution_y = 512
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = True
    scene.view_settings.look = "AgX - Medium High Contrast"

    camera_data = bpy.data.cameras.new("LOD_Camera")
    camera_data.type = "ORTHO"
    camera = bpy.data.objects.new("LOD_Camera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    for name, location, energy in (
        ("LOD_Key", (0.25, -1.2, 1.2), 700.0),
        ("LOD_Fill", (-0.35, 1.0, 0.6), 350.0),
    ):
        light_data = bpy.data.lights.new(name, type="AREA")
        light_data.energy = energy
        light_data.size = 2.0
        light = bpy.data.objects.new(name, light_data)
        scene.collection.objects.link(light)
        light.location = location
        point_at(light, Vector((0.0, 0.0, 0.0)))

    corners = [mesh.matrix_world @ Vector(corner) for corner in mesh.bound_box]
    minimum = Vector(tuple(min(point[index] for point in corners) for index in range(3)))
    maximum = Vector(tuple(max(point[index] for point in corners) for index in range(3)))
    center = (minimum + maximum) * 0.5
    views = {
        "left_full": (Vector((0.0, -1.0, 0.0)), center, None),
        "right_full": (Vector((0.0, 1.0, 0.0)), center, None),
        "muzzle_front_close": (Vector((1.0, 0.0, 0.0)), RAW_MUZZLE_CENTER, 0.22),
        "grip_left_close": (Vector((0.0, -1.0, 0.0)), RAW_GRIP_CENTER, 0.26),
        "foregrip_left_close": (Vector((0.0, -1.0, 0.0)), Vector((0.23, 0.0, -0.025)), 0.28),
    }
    for name, (direction, target_center, fixed_scale) in views.items():
        camera.location = target_center + direction * 2.0
        point_at(camera, target_center)
        if fixed_scale is None:
            inverse = camera.matrix_world.inverted()
            projected = [inverse @ point for point in corners]
            width = max(point.x for point in projected) - min(point.x for point in projected)
            height = max(point.y for point in projected) - min(point.y for point in projected)
            camera.data.ortho_scale = max(height * 1.18, width / 2.0 * 1.18)
        else:
            camera.data.ortho_scale = fixed_scale
        scene.render.filepath = str(destination / f"{name}.png")
        bpy.ops.render.render(write_still=True)
    records.append({
        "label": destination.name,
        "requested_triangles": target,
        "actual_triangles": actual,
        "renders": [f"{destination.name}/{name}.png" for name in views],
    })

(OUTPUT_DIR / "lod_comparison_manifest.json").write_text(
    json.dumps({
        "source": str(RAW_GLB),
        "comparison": records,
        "criteria": [
            "whole left/right silhouette and side material continuity",
            "single open muzzle ring and deep bore fidelity",
            "isolated trigger grip silhouette",
            "clear foregrip contact pad and corridor",
        ],
    }, indent=2),
    encoding="utf-8",
)
print(json.dumps(records, indent=2))
