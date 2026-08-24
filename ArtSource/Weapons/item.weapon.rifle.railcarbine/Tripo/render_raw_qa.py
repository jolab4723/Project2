from pathlib import Path

import bpy
from mathutils import Matrix, Vector


ROOT = Path(__file__).resolve().parents[1]
GLB = ROOT / "Tripo" / "Downloaded" / "item.weapon.rifle.railcarbine_raw.glb"
OUT = ROOT / "Tripo" / "RawQA"


def orient(camera, target):
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(GLB))
meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
points = [obj.matrix_world @ Vector(corner) for obj in meshes for corner in obj.bound_box]
minimum = Vector((min(point[index] for point in points) for index in range(3)))
maximum = Vector((max(point[index] for point in points) for index in range(3)))
center = (minimum + maximum) * 0.5
longest = max(maximum - minimum)

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1024
scene.render.resolution_y = 1024
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.film_transparent = True
scene.view_settings.look = "AgX - Medium High Contrast"

camera_data = bpy.data.cameras.new("RawQA_Camera")
camera_data.type = "ORTHO"
camera = bpy.data.objects.new("RawQA_Camera", camera_data)
scene.collection.objects.link(camera)
scene.camera = camera

for name, offset, energy, size in (
    ("Key", (1.7, -1.8, 2.0), 160.0, 2.2),
    ("Fill", (-1.4, -1.0, 0.8), 80.0, 2.0),
    ("Rim", (0.2, 2.0, 1.4), 120.0, 1.8),
    ("Top", (0.0, 0.2, 2.5), 60.0, 1.5),
):
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.size = size
    light = bpy.data.objects.new(name, data)
    scene.collection.objects.link(light)
    light.location = center + Vector(offset) * longest
    light.rotation_euler = (center - light.location).to_track_quat("-Z", "Y").to_euler()

world = bpy.data.worlds.new("RawQA_World")
world.use_nodes = True
background = world.node_tree.nodes.get("Background")
background.inputs["Color"].default_value = (0.09, 0.105, 0.14, 1.0)
background.inputs["Strength"].default_value = 0.32
scene.world = world
OUT.mkdir(parents=True, exist_ok=True)

distance = longest * 3.2
views = {
    "side_minus_y": Vector((0.0, -distance, 0.0)),
    "side_plus_y": Vector((0.0, distance, 0.0)),
    "raw_plus_x": Vector((distance, 0.0, 0.0)),
    "raw_minus_x": Vector((-distance, 0.0, 0.0)),
    "top_plus_z": Vector((0.0, 0.0, distance)),
    "isometric": Vector((distance, -distance, distance * 0.75)),
}
for name, offset in views.items():
    camera.location = center + offset
    orient(camera, center)
    camera.data.ortho_scale = longest * (1.34 if "side" in name else 0.80)
    scene.render.filepath = str(OUT / f"{name}.png")
    bpy.ops.render.render(write_still=True)

# Geometry-only pass separates lighting/material issues from actual mesh loss.
scene.render.engine = "BLENDER_WORKBENCH"
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "SINGLE"
scene.display.shading.single_color = (0.26, 0.42, 0.68)
scene.display.shading.show_shadows = False
scene.display.shading.show_cavity = True
for name, offset in {
    "geo_side_minus_y": views["side_minus_y"],
    "geo_side_plus_y": views["side_plus_y"],
    "geo_isometric": views["isometric"],
}.items():
    camera.location = center + offset
    orient(camera, center)
    camera.data.ortho_scale = longest * (1.34 if "side" in name else 0.80)
    scene.render.filepath = str(OUT / f"{name}.png")
    bpy.ops.render.render(write_still=True)
