from __future__ import annotations

import json
import math
from pathlib import Path

import bpy
from mathutils import Vector


ITEM_ID = "item.weapon.grenadelauncher.halomortar"
ROOT = Path(bpy.data.filepath).resolve().parents[1]
OUTPUT = ROOT / "QA" / "EmissionRevision" / "Renders"
OUTPUT.mkdir(parents=True, exist_ok=True)


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def render(camera: bpy.types.Object, name: str) -> None:
    bpy.context.scene.camera = camera
    bpy.context.scene.render.filepath = str(OUTPUT / f"{name}.png")
    bpy.ops.render.render(write_still=True)


for image in bpy.data.images:
    if image.source == "FILE" and image.filepath:
        image.reload()

for obj in list(bpy.data.objects):
    if obj.type in {"CAMERA", "LIGHT"}:
        bpy.data.objects.remove(obj, do_unlink=True)

meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
if len(meshes) != 1:
    raise RuntimeError(f"Expected one mesh, found {len(meshes)}")
mesh = meshes[0]
production_material_slots = [slot.material.name if slot.material else None for slot in mesh.material_slots]

corners = [mesh.matrix_world @ Vector(corner) for corner in mesh.bound_box]
minimum = Vector((min(v.x for v in corners), min(v.y for v in corners), min(v.z for v in corners)))
maximum = Vector((max(v.x for v in corners), max(v.y for v in corners), max(v.z for v in corners)))
center = (minimum + maximum) * 0.5
size = maximum - minimum
distance = max(size) * 1.55

camera_data = bpy.data.cameras.new("EmissionRevisionCamera")
camera_data.type = "ORTHO"
camera_data.ortho_scale = max(size.z * 1.28, size.y * 3.0)
camera = bpy.data.objects.new("EmissionRevisionCamera", camera_data)
bpy.context.scene.collection.objects.link(camera)

key_data = bpy.data.lights.new("EmissionRevisionKey", "AREA")
key_data.energy = 700.0
key_data.shape = "DISK"
key_data.size = max(size) * 0.9
key = bpy.data.objects.new("EmissionRevisionKey", key_data)
bpy.context.scene.collection.objects.link(key)
key.location = center + Vector((distance * 0.7, distance * 0.9, distance * 0.3))
look_at(key, center)

fill_data = bpy.data.lights.new("EmissionRevisionFill", "AREA")
fill_data.energy = 320.0
fill_data.size = max(size) * 0.75
fill = bpy.data.objects.new("EmissionRevisionFill", fill_data)
bpy.context.scene.collection.objects.link(fill)
fill.location = center + Vector((-distance * 0.8, distance * 0.35, -distance * 0.15))
look_at(fill, center)

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1000
scene.render.resolution_y = 700
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.film_transparent = False
scene.render.use_file_extension = True
scene.render.resolution_percentage = 100
if scene.world is None:
    scene.world = bpy.data.worlds.new("EmissionRevisionWorld")
scene.world.color = (0.008, 0.008, 0.012)
scene.view_settings.look = "AgX - Medium High Contrast"

views = {
    "left": center + Vector((distance, 0.0, 0.0)),
    "right": center + Vector((-distance, 0.0, 0.0)),
    "front": center + Vector((0.0, 0.0, distance)),
    "iso": center + Vector((distance * 0.8, distance * 0.42, distance * 0.75)),
}

for view_name, location in views.items():
    camera.location = location
    look_at(camera, center)
    if view_name in {"front"}:
        camera.data.ortho_scale = max(size.x, size.y) * 1.7
    else:
        camera.data.ortho_scale = max(size.z * 1.28, size.y * 3.0)
    render(camera, f"pbr_emission_{view_name}")

# Replace the two slots only in this unsaved QA session so the next views display
# authored emission pixels alone. The production blend and mesh remain untouched.
source_pbr = bpy.data.materials[f"{ITEM_ID}_PBR"]
emission_image = source_pbr.node_tree.nodes["EmissionMask"].image
qa_emission = bpy.data.materials.new("QA_ExactPurpleEmissionOnly")
qa_emission.use_nodes = True
nodes = qa_emission.node_tree.nodes
nodes.clear()
output_node = nodes.new("ShaderNodeOutputMaterial")
principled = nodes.new("ShaderNodeBsdfPrincipled")
principled.inputs["Base Color"].default_value = (0.0, 0.0, 0.0, 1.0)
principled.inputs["Metallic"].default_value = 0.0
principled.inputs["Roughness"].default_value = 1.0
principled.inputs["Emission Strength"].default_value = 3.0
texture = nodes.new("ShaderNodeTexImage")
texture.image = emission_image
mix = nodes.new("ShaderNodeMixRGB")
mix.blend_type = "MULTIPLY"
mix.inputs[0].default_value = 1.0
mix.inputs[2].default_value = (0.42, 0.06, 1.0, 1.0)
qa_emission.node_tree.links.new(texture.outputs["Color"], mix.inputs[1])
qa_emission.node_tree.links.new(mix.outputs["Color"], principled.inputs["Emission Color"])
qa_emission.node_tree.links.new(principled.outputs["BSDF"], output_node.inputs["Surface"])

qa_black = bpy.data.materials.new("QA_NonEmissionBlack")
qa_black.diffuse_color = (0.0, 0.0, 0.0, 1.0)
qa_black.use_nodes = True
qa_black.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.0, 0.0, 0.0, 1.0)
qa_black.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 1.0

mesh.data.materials[0] = qa_emission
if len(mesh.data.materials) > 1:
    mesh.data.materials[1] = qa_black

key.hide_render = True
fill.hide_render = True
scene.world.color = (0.0, 0.0, 0.0)
for view_name in ("left", "front", "iso"):
    camera.location = views[view_name]
    look_at(camera, center)
    if view_name == "front":
        camera.data.ortho_scale = max(size.x, size.y) * 1.7
    else:
        camera.data.ortho_scale = max(size.z * 1.28, size.y * 3.0)
    render(camera, f"emission_only_{view_name}")

report = {
    "blend": bpy.data.filepath,
    "mesh": mesh.name,
    "bounds_min": list(minimum),
    "bounds_max": list(maximum),
    "views": sorted(path.name for path in OUTPUT.glob("*.png")),
    "production_material_slots": production_material_slots,
    "basecolor_image": bpy.data.materials[f"{ITEM_ID}_PBR"].node_tree.nodes["BaseColor"].image.filepath_from_user(),
    "emission_image": bpy.data.materials[f"{ITEM_ID}_PBR"].node_tree.nodes["EmissionMask"].image.filepath_from_user(),
    "emission_strength_math": {
        "operation": bpy.data.materials[f"{ITEM_ID}_PBR"].node_tree.nodes["Math"].operation,
        "multiplier": bpy.data.materials[f"{ITEM_ID}_PBR"].node_tree.nodes["Math"].inputs[1].default_value,
    },
}
(OUTPUT / "render_report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
