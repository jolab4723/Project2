import json
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


ITEM_ID = "item.weapon.rifle.dustsparrow"
ITEM_DIR = Path(__file__).resolve().parents[2]
RAW_GLB = ITEM_DIR / "Tripo" / "CostSafeRetry20260825" / "Downloaded" / f"{ITEM_ID}_raw.glb"
OUTPUT_DIR = Path(__file__).resolve().parent / "QA" / "LODComparison"
RAW_RIGHT_HAND_CENTER = Vector((0.2015, 0.0005, -0.0710))
RAW_MUZZLE_CENTER = Vector((-0.5000, 0.00056, 0.06326))
MODEL_SCALE = 0.90248964
TARGETS = [933617, 450000, 300000, 150000, 100000]


def transformed_point(point: Vector) -> Vector:
    local = point - RAW_RIGHT_HAND_CENTER
    return Vector((-local.y, local.z, -local.x)) * MODEL_SCALE


def point_at(obj, target: Vector) -> None:
    forward = (target - obj.location).normalized()
    reference_up = Vector((0.0, 1.0, 0.0))
    if abs(forward.dot(reference_up)) > 0.98:
        reference_up = Vector((0.0, 0.0, 1.0))
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
    for vertex in mesh.data.vertices:
        vertex.co = transformed_point(vertex.co)
    mesh.data.update()
    mesh.data.calc_loop_triangles()
    final_triangles = len(mesh.data.loop_triangles)

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
        ("LOD_Key", (-1.2, 1.4, 0.25), 750.0),
        ("LOD_Fill", (1.1, 0.8, 0.45), 380.0),
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
    views = {
        "left_full": (Vector((-1.0, 0.0, 0.0)), center, None),
        "right_full": (Vector((1.0, 0.0, 0.0)), center, None),
        "muzzle_front_close": (Vector((0.0, 0.0, 1.0)), transformed_point(RAW_MUZZLE_CENTER), 0.11),
        "grip_left_close": (Vector((-1.0, 0.0, 0.0)), Vector((0.0, 0.02, 0.0)), 0.30),
    }
    for name, (direction, target_center, fixed_scale) in views.items():
        camera.location = target_center + direction * 2.2
        point_at(camera, target_center)
        if fixed_scale is None:
            inverse = camera.matrix_world.inverted()
            projected = [inverse @ point for point in corners]
            width = max(point.x for point in projected) - min(point.x for point in projected)
            height = max(point.y for point in projected) - min(point.y for point in projected)
            camera.data.ortho_scale = max(height * 1.22, width / 2.0 * 1.22)
        else:
            camera.data.ortho_scale = fixed_scale
        scene.render.filepath = str(destination / f"{name}.png")
        bpy.ops.render.render(write_still=True)
    records.append(
        {
            "label": destination.name,
            "requested_triangles": target,
            "actual_triangles": final_triangles,
            "renders": [f"{destination.name}/{name}.png" for name in views],
        }
    )

(OUTPUT_DIR / "lod_comparison_manifest.json").write_text(
    json.dumps(
        {
            "source": str(RAW_GLB),
            "comparison": records,
            "criteria": [
                "whole left/right silhouette and side material continuity",
                "open muzzle ring and bore fidelity",
                "isolated trigger-grip silhouette and surface fidelity"
            ],
        },
        indent=2,
    ),
    encoding="utf-8",
)
print(json.dumps(records, indent=2))
