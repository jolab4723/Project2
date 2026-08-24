from __future__ import annotations

import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def cli_arg(name: str) -> str:
    args = sys.argv[sys.argv.index("--") + 1 :]
    return args[args.index(name) + 1]


def point_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def flat_material(name: str, color: tuple[float, float, float, float], strength: float = 1.0):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    emission = nodes.new("ShaderNodeEmission")
    emission.inputs["Color"].default_value = color
    emission.inputs["Strength"].default_value = strength
    material.node_tree.links.new(emission.outputs["Emission"], output.inputs["Surface"])
    return material


def marker_sphere(location: Vector, material: bpy.types.Material) -> None:
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=3, radius=0.021, location=location)
    marker = bpy.context.object
    marker.name = f"QA_{material.name}_Contact"
    marker.data.materials.append(material)


def marker_axis(origin: Vector, direction: Vector, material: bpy.types.Material) -> None:
    direction = direction.normalized()
    length = 0.10
    midpoint = origin + direction * (length * 0.5)
    bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=0.0045, depth=length, location=midpoint)
    shaft = bpy.context.object
    shaft.name = f"QA_{material.name}_Axis"
    shaft.rotation_euler = direction.to_track_quat("Z", "Y").to_euler()
    shaft.data.materials.append(material)
    bpy.ops.mesh.primitive_cone_add(
        vertices=24,
        radius1=0.012,
        radius2=0.0,
        depth=0.027,
        location=origin + direction * (length + 0.0135),
    )
    arrow = bpy.context.object
    arrow.name = f"QA_{material.name}_Arrow"
    arrow.rotation_euler = direction.to_track_quat("Z", "Y").to_euler()
    arrow.data.materials.append(material)


source = Path(cli_arg("--source")).resolve()
output_dir = Path(cli_arg("--output-dir")).resolve()
output_dir.mkdir(parents=True, exist_ok=True)

bpy.ops.wm.open_mainfile(filepath=str(source))
scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1400
scene.render.resolution_y = 900
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.film_transparent = False
scene.view_settings.look = "AgX - Medium High Contrast"

world = scene.world or bpy.data.worlds.new("QAWorld")
scene.world = world
world.use_nodes = True
background = world.node_tree.nodes.get("Background")
background.inputs["Color"].default_value = (0.025, 0.03, 0.04, 1.0)
background.inputs["Strength"].default_value = 0.25

target = Vector((0.0, 0.055, 0.31))
camera_data = bpy.data.cameras.new("QA_Camera")
camera = bpy.data.objects.new("QA_Camera", camera_data)
scene.collection.objects.link(camera)
scene.camera = camera
camera.location = (1.25, -1.55, 0.65)
camera.data.type = "ORTHO"
camera.data.ortho_scale = 1.15
point_at(camera, target)
camera.rotation_euler.rotate_axis("Z", math.radians(90.0))

lights = []
for name, location, energy, size in (
    ("QA_Key", (-0.55, -1.15, 1.15), 8.0, 1.8),
    ("QA_Fill", (0.85, -0.55, 0.45), 4.0, 1.4),
    ("QA_Rim", (-0.2, 0.75, 0.95), 6.0, 1.2),
):
    light_data = bpy.data.lights.new(name, "AREA")
    light_data.energy = energy
    light_data.shape = "DISK"
    light_data.size = size
    light = bpy.data.objects.new(name, light_data)
    scene.collection.objects.link(light)
    light.location = location
    point_at(light, target)
    lights.append(light)

mesh_object = next(obj for obj in scene.objects if obj.type == "MESH")
material = mesh_object.data.materials[0]
principled = material.node_tree.nodes.get("Principled BSDF")
if principled is None:
    principled = next(node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED")

# Neutral PBR: authored base/normal/ORM under neutral lighting, emission disabled.
principled.inputs["Emission Strength"].default_value = 0.0
scene.render.filepath = str(output_dir / "neutral_pbr.png")
bpy.ops.render.render(write_still=True)

# Contact and axis overlay. Red=RH, cyan=LH, yellow=Muzzle; helpers are never saved.
principled.inputs["Emission Strength"].default_value = 1.8
rh = scene.objects["RightHandGrip"]
lh = scene.objects["LeftHandGrip"]
muzzle = scene.objects["Muzzle"]
rh_material = flat_material("RH_Red", (1.0, 0.02, 0.01, 1.0), 2.5)
lh_material = flat_material("LH_Cyan", (0.0, 0.8, 1.0, 1.0), 2.5)
muzzle_material = flat_material("Muzzle_Yellow", (1.0, 0.55, 0.0, 1.0), 2.5)
for marker, marker_material in ((rh, rh_material), (lh, lh_material), (muzzle, muzzle_material)):
    origin = marker.matrix_world.translation
    direction = marker.matrix_world.to_3x3() @ Vector((0.0, 0.0, 1.0))
    marker_sphere(origin, marker_material)
    marker_axis(origin, direction, marker_material)
scene.render.filepath = str(output_dir / "grip_overlay.png")
bpy.ops.render.render(write_still=True)

# Emission-only: hide lights and suppress every non-emissive PBR input.
for obj in list(scene.objects):
    if obj.name.startswith("QA_") and obj.type == "MESH":
        bpy.data.objects.remove(obj, do_unlink=True)
for light in lights:
    light.hide_render = True
background.inputs["Color"].default_value = (0.0, 0.0, 0.0, 1.0)
background.inputs["Strength"].default_value = 0.0
for input_name in ("Base Color", "Metallic", "Roughness", "Normal"):
    input_socket = principled.inputs[input_name]
    for link in list(input_socket.links):
        material.node_tree.links.remove(link)
principled.inputs["Base Color"].default_value = (0.0, 0.0, 0.0, 1.0)
principled.inputs["Metallic"].default_value = 0.0
principled.inputs["Roughness"].default_value = 1.0
principled.inputs["Emission Strength"].default_value = 6.0
scene.render.filepath = str(output_dir / "emission_only.png")
bpy.ops.render.render(write_still=True)

print(output_dir)
