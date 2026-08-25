from __future__ import annotations

import json
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


ROOT = Path(__file__).resolve().parents[1]
GLB = ROOT / "Tripo" / "RetryAfterExpired20260825" / "Downloaded" / "item.weapon.rifle.smilesignal_raw.glb"
OUT = ROOT / "Production" / "QA" / "Decimation"
REPORT = OUT / "candidate_report.json"

ITEM_ID = "item.weapon.rifle.smilesignal"
SCALE_METERS = 0.82
RIGHT_HAND_RAW = Vector((0.24, 0.0, -0.105))
ORIENTATION = Matrix(
    (
        (0.0, -1.0, 0.0, 0.0),
        (0.0, 0.0, 1.0, 0.0),
        (-1.0, 0.0, 0.0, 0.0),
        (0.0, 0.0, 0.0, 1.0),
    )
)
TRANSFORM = Matrix.Scale(SCALE_METERS, 4) @ ORIENTATION @ Matrix.Translation(-RIGHT_HAND_RAW)
TARGETS = (
    ("original", None),
    ("600k", 600_000),
    ("400k", 400_000),
    ("250k", 250_000),
)


def orient(camera, target):
    direction = (target - camera.location).normalized()
    world_up = Vector((0.0, 1.0, 0.0))
    if abs(direction.dot(world_up)) > 0.995:
        world_up = Vector((0.0, 0.0, 1.0))
    right = direction.cross(world_up).normalized()
    up = right.cross(direction).normalized()
    camera.rotation_euler = Matrix((right, up, -direction)).transposed().to_euler()


def bounds(obj):
    points = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
    minimum = Vector(tuple(min(point[index] for point in points) for index in range(3)))
    maximum = Vector(tuple(max(point[index] for point in points) for index in range(3)))
    return minimum, maximum


def triangle_count(obj):
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def setup_scene(center, longest):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1024
    scene.render.resolution_y = 1024
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = True
    scene.view_settings.look = "AgX - Medium High Contrast"

    camera_data = bpy.data.cameras.new("CandidateQA_Camera")
    camera_data.type = "ORTHO"
    camera = bpy.data.objects.new("CandidateQA_Camera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera

    for name, offset, energy, size in (
        ("Key", (1.8, -1.8, 2.2), 1150.0, 2.5),
        ("Fill", (-1.5, -1.0, 0.9), 650.0, 2.3),
        ("Rim", (0.3, 2.0, 1.5), 850.0, 2.0),
        ("Top", (0.0, 0.2, 2.7), 420.0, 1.7),
    ):
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.size = size * longest
        light = bpy.data.objects.new(name, data)
        scene.collection.objects.link(light)
        light.location = center + Vector(offset) * longest
        orient(light, center)

    world = bpy.data.worlds.new("CandidateQA_World")
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (0.025, 0.03, 0.045, 1.0)
    background.inputs["Strength"].default_value = 0.25
    scene.world = world
    return scene, camera


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(GLB))
source = next(obj for obj in bpy.context.scene.objects if obj.type == "MESH")
source.name = f"{ITEM_ID}_Source"
source.data.transform(TRANSFORM)
source.data.update()
source.location = (0.0, 0.0, 0.0)
source.rotation_euler = (0.0, 0.0, 0.0)
source.scale = (1.0, 1.0, 1.0)

minimum, maximum = bounds(source)
center = (minimum + maximum) * 0.5
longest = max(maximum - minimum)
scene, camera = setup_scene(center, longest)
OUT.mkdir(parents=True, exist_ok=True)

distance = longest * 3.2
views = {
    "left": Vector((-distance, 0.0, 0.0)),
    "iso_left": Vector((-distance, distance * 0.72, distance)),
    "front": Vector((0.0, 0.0, distance)),
    "decal_close": Vector((-distance, 0.0, distance * 0.04)),
}

source_triangles = triangle_count(source)
records = []
for label, target in TARGETS:
    candidate = source.copy()
    candidate.data = source.data.copy()
    candidate.name = f"{ITEM_ID}_{label}"
    candidate.hide_render = False
    candidate.hide_viewport = False
    scene.collection.objects.link(candidate)
    source.hide_render = True

    if target is not None:
        modifier = candidate.modifiers.new(name=f"Preserve_{target}", type="DECIMATE")
        modifier.decimate_type = "COLLAPSE"
        modifier.ratio = min(1.0, target / source_triangles)
        modifier.use_collapse_triangulate = True
        modifier.use_symmetry = True
        modifier.symmetry_axis = "X"
        bpy.context.view_layer.objects.active = candidate
        candidate.select_set(True)
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        candidate.select_set(False)

    triangles = triangle_count(candidate)
    for view_name, offset in views.items():
        camera.location = center + offset
        orient(camera, center)
        if view_name == "decal_close":
            camera.data.ortho_scale = longest * 0.66
        elif view_name == "front":
            camera.data.ortho_scale = longest * 0.82
        else:
            camera.data.ortho_scale = longest * 1.10
        scene.render.filepath = str(OUT / f"{label}_{view_name}.png")
        bpy.ops.render.render(write_still=True)

    records.append(
        {
            "candidate": label,
            "target_triangles": target,
            "actual_triangles": triangles,
            "ratio_of_source": round(triangles / source_triangles, 6),
        }
    )
    bpy.data.objects.remove(candidate, do_unlink=True)

REPORT.write_text(
    json.dumps(
        {
            "source_triangles": source_triangles,
            "scale_meters": SCALE_METERS,
            "right_hand_raw": list(RIGHT_HAND_RAW),
            "production_axes": {"muzzle": "+Z", "up": "+Y"},
            "candidates": records,
        },
        ensure_ascii=False,
        indent=2,
    ),
    encoding="utf-8",
)
print(REPORT)
