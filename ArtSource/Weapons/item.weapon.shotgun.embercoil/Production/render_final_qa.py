from __future__ import annotations

import os
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parent
OUT_DIR = ROOT / "QA" / "FinalBlend"
OUT_DIR.mkdir(parents=True, exist_ok=True)


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def fit_orthographic(
    camera: bpy.types.Object,
    target: Vector,
    direction: Vector,
    points: list[Vector],
    margin: float = 1.18,
) -> None:
    direction.normalize()
    camera.location = target + direction * 3.5
    look_at(camera, target)
    bpy.context.view_layer.update()
    right = camera.matrix_world.to_quaternion() @ Vector((1.0, 0.0, 0.0))
    up = camera.matrix_world.to_quaternion() @ Vector((0.0, 1.0, 0.0))
    half_width = max(abs((point - target).dot(right)) for point in points)
    half_height = max(abs((point - target).dot(up)) for point in points)
    camera.data.ortho_scale = max(half_height * 2.0, half_width * 2.0) * margin


model = bpy.data.objects.get("Model")
if model is None or model.type != "MESH":
    raise RuntimeError("Final Model mesh not found")

points = [model.matrix_world @ Vector(corner) for corner in model.bound_box]
minimum = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
maximum = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))
center = (minimum + maximum) * 0.5
maximum_dimension = max(maximum - minimum)

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1024
scene.render.resolution_y = 1024
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.film_transparent = False
scene.render.image_settings.color_depth = "8"
scene.view_settings.look = "AgX - Medium High Contrast"
scene.world.color = (0.012, 0.014, 0.020)

camera_data = bpy.data.cameras.new("FinalQACamera")
camera_data.type = "ORTHO"
camera = bpy.data.objects.new("FinalQACamera", camera_data)
scene.collection.objects.link(camera)
scene.camera = camera

for name, location, energy, size, color in (
    ("FinalQA_Key", (1.6, 1.8, 1.4), 180.0, 1.2, (1.0, 0.80, 0.63)),
    ("FinalQA_Fill", (-1.5, 0.7, 0.2), 95.0, 1.6, (0.42, 0.58, 1.0)),
    ("FinalQA_Rim", (0.3, 1.2, -1.4), 130.0, 1.0, (0.72, 0.84, 1.0)),
):
    data = bpy.data.lights.new(name=name, type="AREA")
    data.energy = energy
    data.shape = "DISK"
    data.size = size
    data.color = color
    light = bpy.data.objects.new(name, data)
    scene.collection.objects.link(light)
    light.location = center + Vector(location) * maximum_dimension
    look_at(light, center)

views = {
    "side_left_x_pos": Vector((1.0, 0.0, 0.0)),
    "side_right_x_neg": Vector((-1.0, 0.0, 0.0)),
    "top_y_pos": Vector((0.0, 1.0, 0.0)),
    "bottom_y_neg": Vector((0.0, -1.0, 0.0)),
    "muzzle_z_pos": Vector((0.0, 0.0, 1.0)),
    "stock_z_neg": Vector((0.0, 0.0, -1.0)),
    "iso_muzzle": Vector((1.0, 0.8, 1.0)),
    "iso_stock": Vector((-1.0, 0.8, -1.0)),
}
for name, direction in views.items():
    fit_orthographic(camera, center, direction, points)
    scene.render.filepath = os.fspath(OUT_DIR / f"{name}.png")
    bpy.ops.render.render(write_still=True)
