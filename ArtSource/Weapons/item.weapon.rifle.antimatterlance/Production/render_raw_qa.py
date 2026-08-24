from pathlib import Path

import bpy
from mathutils import Matrix, Vector


ITEM_ID = "item.weapon.rifle.antimatterlance"
ITEM_DIR = Path(__file__).resolve().parents[1]
RAW_GLB = ITEM_DIR / "Tripo" / "Downloaded" / f"{ITEM_ID}_raw.glb"
OUT_DIR = ITEM_DIR / "Production" / "QA" / "raw_views"


def point_at(obj, target, world_up):
    forward = (Vector(target) - obj.location).normalized()
    right = forward.cross(Vector(world_up)).normalized()
    corrected_up = right.cross(forward).normalized()
    obj.rotation_euler = Matrix((right, corrected_up, -forward)).transposed().to_euler()


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(RAW_GLB))
scene = bpy.context.scene
OUT_DIR.mkdir(parents=True, exist_ok=True)
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1024
scene.render.resolution_y = 512
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.film_transparent = True
scene.view_settings.look = "AgX - Medium High Contrast"

camera = bpy.data.objects.new("QA_Camera", bpy.data.cameras.new("QA_Camera"))
scene.collection.objects.link(camera)
scene.camera = camera
camera.data.type = "ORTHO"

for name, location, energy, size in (
    ("QA_Key", (0.0, -1.7, 1.6), 180.0, 2.5),
    ("QA_Fill", (-1.3, 1.4, 0.7), 95.0, 2.0),
    ("QA_Rim", (1.2, 0.8, 1.3), 130.0, 1.5),
):
    light_data = bpy.data.lights.new(name, type="AREA")
    light_data.energy = energy
    light_data.size = size
    light = bpy.data.objects.new(name, light_data)
    scene.collection.objects.link(light)
    light.location = location
    point_at(light, (0.0, 0.0, 0.0), (0.0, 0.0, 1.0))

views = {
    "side_minus_y": ((0.0, -2.2, 0.0), 1.12, (0.0, 0.0, 1.0)),
    "side_plus_y": ((0.0, 2.2, 0.0), 1.12, (0.0, 0.0, 1.0)),
    "axial_plus_x": ((2.2, 0.0, 0.0), 0.42, (0.0, 0.0, 1.0)),
    "axial_minus_x": ((-2.2, 0.0, 0.0), 0.42, (0.0, 0.0, 1.0)),
    "top_plus_z": ((0.0, 0.0, 2.2), 1.12, (0.0, 1.0, 0.0)),
}
for name, (location, scale, world_up) in views.items():
    camera.location = location
    camera.data.ortho_scale = scale
    point_at(camera, (0.0, 0.0, 0.0), world_up)
    scene.render.filepath = str(OUT_DIR / f"{name}.png")
    bpy.ops.render.render(write_still=True)
