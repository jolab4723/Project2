import bpy
import json
import math
from pathlib import Path
from mathutils import Vector


ROOT = Path(__file__).resolve().parent
GLB = ROOT / "Tripo" / "Downloaded" / "model.glb"
OUT = ROOT / "Production"
TEX = OUT / "Textures"
QA = OUT / "RawQA"


def look_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


OUT.mkdir(parents=True, exist_ok=True)
TEX.mkdir(parents=True, exist_ok=True)
QA.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(GLB))
mesh = next(obj for obj in bpy.context.scene.objects if obj.type == "MESH")

for image in bpy.data.images:
    prefix = image.name.split("_")[0]
    suffix = {"Color": "BaseColor", "NormalGL": "Normal", "ORM": "ORM"}.get(prefix)
    if not suffix:
        continue
    target = TEX / f"item.weapon.grenadelauncher.sunfallengine_{suffix}.png"
    image.filepath_raw = str(target)
    image.file_format = "PNG"
    image.save()

world = bpy.data.worlds.new("RawQAWorld")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.025, 0.025, 0.025, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.4
bpy.context.scene.world = world

for name, location, energy, size in [
    ("Key", (-1.2, -1.4, 1.2), 850, 2.0),
    ("Fill", (1.3, -0.8, 0.5), 550, 1.5),
    ("Rim", (0.0, 1.4, 1.0), 700, 1.2),
]:
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.shape = "DISK"
    data.size = size
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    look_at(obj, (0, 0, 0))

camera_data = bpy.data.cameras.new("RawQACamera")
camera_data.type = "ORTHO"
camera_data.ortho_scale = 1.15
camera = bpy.data.objects.new("RawQACamera", camera_data)
bpy.context.collection.objects.link(camera)
bpy.context.scene.camera = camera

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1024
scene.render.resolution_y = 512
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.film_transparent = True

views = {
    "side_from_negative_y": (0, -1.8, 0),
    "side_from_positive_y": (0, 1.8, 0),
    "end_from_positive_x": (1.8, 0, 0),
    "end_from_negative_x": (-1.8, 0, 0),
    "top": (0, 0, 1.8),
    "bottom": (0, 0, -1.8),
    "iso": (1.3, -1.3, 0.8),
    "iso_reverse": (-1.3, 1.3, 0.8),
}
for name, location in views.items():
    camera.location = location
    look_at(camera, (0, 0, 0))
    scene.render.filepath = str(QA / f"{name}.png")
    bpy.ops.render.render(write_still=True)

print(json.dumps({"glb": str(GLB), "qa": str(QA), "textures": str(TEX)}, indent=2))
