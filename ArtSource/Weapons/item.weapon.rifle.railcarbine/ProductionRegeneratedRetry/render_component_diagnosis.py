from __future__ import annotations

import colorsys
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(bpy.data.filepath).resolve().parent
QA = ROOT / "QA"
BODY_NAME = "RailCarbine_Regenerated_Body"


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


body = bpy.data.objects[BODY_NAME]
bpy.ops.object.select_all(action="DESELECT")
body.select_set(True)
bpy.context.view_layer.objects.active = body
bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.separate(type="LOOSE")
bpy.ops.object.mode_set(mode="OBJECT")
parts = [obj for obj in bpy.context.selected_objects if obj.type == "MESH"]

for index, part in enumerate(parts):
    hue = (index * 0.618033988749895) % 1.0
    rgb = colorsys.hsv_to_rgb(hue, 0.72, 0.95)
    material = bpy.data.materials.new(f"Component_{index:03d}")
    material.diffuse_color = (*rgb, 1.0)
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*rgb, 1.0)
    shader.inputs["Roughness"].default_value = 0.72
    part.data.materials.clear()
    part.data.materials.append(material)

scene = bpy.context.scene
for obj in list(bpy.data.objects):
    if obj.type in {"CAMERA", "LIGHT"}:
        bpy.data.objects.remove(obj, do_unlink=True)
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1024
scene.render.resolution_y = 1024
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False
scene.world.color = (0.008, 0.008, 0.008)

camera_data = bpy.data.cameras.new("ComponentQaCamera")
camera_data.type = "ORTHO"
camera = bpy.data.objects.new("ComponentQaCamera", camera_data)
scene.collection.objects.link(camera)
scene.camera = camera

for name, energy, location in (
    ("Key", 18.0, (0.32, 0.42, 0.35)),
    ("Fill", 10.0, (-0.32, 0.22, 0.28)),
):
    light_data = bpy.data.lights.new(name, type="AREA")
    light_data.energy = energy
    light_data.shape = "DISK"
    light_data.size = 0.45
    light = bpy.data.objects.new(name, light_data)
    scene.collection.objects.link(light)
    light.location = location
    look_at(light, Vector((0.0, 0.075, 0.30)))

views = {
    "components_top_close.png": ((0.0, 0.60, 0.30), (0.0, 0.075, 0.30), 0.36),
    "components_side_close.png": ((0.45, 0.075, 0.30), (0.0, 0.075, 0.30), 0.36),
    "components_muzzle_close.png": ((0.0, 0.075, 0.78), (0.0, 0.075, 0.435), 0.13),
}
for filename, (location, target, scale) in views.items():
    camera.location = location
    camera_data.ortho_scale = scale
    look_at(camera, Vector(target))
    scene.render.filepath = str(QA / filename)
    bpy.ops.render.render(write_still=True)
