from __future__ import annotations

import json
import os
from pathlib import Path

import bpy
from mathutils import Vector


ITEM_ID = "item.weapon.shotgun.embercoil"
QA_ROOT = Path(__file__).resolve().parent
FBX_PATH = QA_ROOT.parent / f"{ITEM_ID}.fbx"
CANDIDATE_ROOT = QA_ROOT / "GlobalEmissionCandidates"
RULES_PATH = CANDIDATE_ROOT / "candidate_rules.json"
RENDER_ROOT = CANDIDATE_ROOT / "Renders"
CONTACT_PATH = CANDIDATE_ROOT / "contact_sheet.png"
DESIGNATED_LINEAR = (1.0, 0.06847816984440017, 0.010329823029626936, 1.0)


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def fit(camera: bpy.types.Object, target: Vector, direction: Vector, points: list[Vector]) -> None:
    direction.normalize()
    camera.location = target + direction * 3.5
    look_at(camera, target)
    bpy.context.view_layer.update()
    rotation = camera.matrix_world.to_quaternion()
    right = rotation @ Vector((1, 0, 0))
    up = rotation @ Vector((0, 1, 0))
    half_width = max(abs((point - target).dot(right)) for point in points)
    half_height = max(abs((point - target).dot(up)) for point in points)
    camera.data.ortho_scale = max(half_width * 2, half_height * 2) * 1.16


def material_for_mask(path: Path) -> bpy.types.Material:
    material = bpy.data.materials.new(f"Candidate_{path.stem}")
    material.use_nodes = True
    material.use_backface_culling = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    emission = nodes.new("ShaderNodeEmission")
    emission.inputs["Color"].default_value = DESIGNATED_LINEAR
    emission.inputs["Strength"].default_value = 2.0
    transparent = nodes.new("ShaderNodeBsdfTransparent")
    mix = nodes.new("ShaderNodeMixShader")
    image = bpy.data.images.load(os.fspath(path), check_existing=False)
    image.colorspace_settings.name = "Non-Color"
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = image
    texture.interpolation = "Closest"
    links.new(texture.outputs["Color"], mix.inputs[0])
    links.new(transparent.outputs["BSDF"], mix.inputs[1])
    links.new(emission.outputs["Emission"], mix.inputs[2])
    links.new(mix.outputs["Shader"], output.inputs["Surface"])
    return material


bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=os.fspath(FBX_PATH), use_anim=False)
model = next(obj for obj in bpy.context.scene.objects if obj.type == "MESH")
points = [model.matrix_world @ Vector(corner) for corner in model.bound_box]
minimum = Vector(tuple(min(point[index] for point in points) for index in range(3)))
maximum = Vector(tuple(max(point[index] for point in points) for index in range(3)))
center = (minimum + maximum) * 0.5

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 512
scene.render.resolution_y = 512
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.film_transparent = True
scene.view_settings.look = "AgX - Medium High Contrast"

camera_data = bpy.data.cameras.new("CandidateCamera")
camera_data.type = "ORTHO"
camera = bpy.data.objects.new("CandidateCamera", camera_data)
scene.collection.objects.link(camera)
scene.camera = camera

views = {
    "left": Vector((1, 0, 0)),
    "muzzle": Vector((0, 0, 1)),
    "iso": Vector((1, 0.8, 1)),
}
rules = json.loads(RULES_PATH.read_text(encoding="utf-8"))["candidates"]
for rule_name, entry in rules.items():
    material = material_for_mask(Path(entry["path"]))
    model.data.materials.clear()
    model.data.materials.append(material)
    output_dir = RENDER_ROOT / rule_name
    output_dir.mkdir(parents=True, exist_ok=True)
    for view_name, direction in views.items():
        fit(camera, center, direction, points)
        scene.render.filepath = os.fspath(output_dir / f"{view_name}.png")
        bpy.ops.render.render(write_still=True)
