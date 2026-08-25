import bpy
import os
from mathutils import Vector


ROOT = os.path.dirname(os.path.abspath(__file__))
SOURCE = os.path.normpath(os.path.join(ROOT, "..", "Tripo", "Downloaded", "model.glb"))
MASK = os.path.join(ROOT, "QA", "EmissionThresholds", "rgb_r235_g70_b55_rg100_gb0.png")
OUT_DIR = os.path.join(ROOT, "QA", "EmissionCandidateG70")
os.makedirs(OUT_DIR, exist_ok=True)


def srgb_channel_to_linear(value):
    value /= 255.0
    return value / 12.92 if value <= 0.04045 else ((value + 0.055) / 1.055) ** 2.4


def look_at(obj, target):
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def bounds(objects):
    points = [obj.matrix_world @ Vector(corner) for obj in objects for corner in obj.bound_box]
    minimum = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
    maximum = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))
    return minimum, maximum, points


def fit_camera(camera, target, direction, points, margin=1.18):
    direction = Vector(direction).normalized()
    max_dimension = max(max(p[i] for p in points) - min(p[i] for p in points) for i in range(3))
    camera.location = target + direction * max_dimension * 3.0
    look_at(camera, target)
    bpy.context.view_layer.update()
    right = camera.matrix_world.to_quaternion() @ Vector((1.0, 0.0, 0.0))
    up = camera.matrix_world.to_quaternion() @ Vector((0.0, 1.0, 0.0))
    half_width = max(abs((p - target).dot(right)) for p in points)
    half_height = max(abs((p - target).dot(up)) for p in points)
    camera.data.ortho_scale = max(half_height * 2.0, half_width * 2.0) * margin


bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=SOURCE)
meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
minimum, maximum, corners = bounds(meshes)
center = (minimum + maximum) * 0.5
max_dimension = max(maximum - minimum)

material = next(material for obj in meshes for material in obj.data.materials if material and material.name.startswith("tripo_material_"))
nodes = material.node_tree.nodes
links = material.node_tree.links
principled = next(node for node in nodes if node.type == "BSDF_PRINCIPLED")
mask_image = bpy.data.images.load(MASK, check_existing=False)
mask_image.colorspace_settings.name = "Non-Color"
mask_node = nodes.new("ShaderNodeTexImage")
mask_node.name = "Emission Mask Candidate"
mask_node.image = mask_image
mask_node.interpolation = "Closest"
emission_color = tuple(srgb_channel_to_linear(v) for v in (255, 74, 26)) + (1.0,)
principled.inputs["Emission Color"].default_value = emission_color
strength = nodes.new("ShaderNodeMath")
strength.operation = "MULTIPLY"
strength.inputs[1].default_value = 6.0
links.new(mask_node.outputs["Color"], strength.inputs[0])
links.new(strength.outputs[0], principled.inputs["Emission Strength"])

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1024
scene.render.resolution_y = 1024
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.film_transparent = False
scene.view_settings.look = "AgX - Medium High Contrast"
scene.world.color = (0.012, 0.012, 0.018)

camera_data = bpy.data.cameras.new("CandidateCamera")
camera_data.type = "ORTHO"
camera = bpy.data.objects.new("CandidateCamera", camera_data)
scene.collection.objects.link(camera)
scene.camera = camera

for name, location, energy, color in (
    ("Key", center + Vector((max_dimension * 1.8, -max_dimension * 1.2, max_dimension * 1.7)), 100.0, (1.0, 0.82, 0.68)),
    ("Fill", center + Vector((-max_dimension * 1.3, -max_dimension * 1.6, max_dimension * 0.7)), 55.0, (0.55, 0.68, 1.0)),
):
    data = bpy.data.lights.new(name=name, type="AREA")
    data.energy = energy
    data.shape = "DISK"
    data.size = max_dimension * 1.4
    data.color = color
    light = bpy.data.objects.new(name, data)
    scene.collection.objects.link(light)
    light.location = location
    look_at(light, center)

views = {
    "side_left": (0.0, 1.0, 0.0),
    "side_right": (0.0, -1.0, 0.0),
    "iso_muzzle": (-1.0, 1.0, 0.72),
    "iso_stock": (1.0, -1.0, 0.72),
}
for name, direction in views.items():
    fit_camera(camera, center, direction, corners)
    scene.render.filepath = os.path.join(OUT_DIR, f"candidate_{name}.png")
    bpy.ops.render.render(write_still=True)
