from pathlib import Path

import bpy
from mathutils import Vector


ITEM_DIR = Path(__file__).resolve().parents[1]
RAW_GLB = ITEM_DIR / "Tripo" / "Downloaded" / "item.weapon.shotgun.thermobarrel_raw.glb"
OUT_DIR = ITEM_DIR / "Production" / "QA" / "raw_views"


def point_at(obj, target, up_axis="Y"):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", up_axis).to_euler()


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
scene.render.image_settings.color_depth = "8"
scene.view_settings.look = "AgX - Medium High Contrast"

camera_data = bpy.data.cameras.new("QA_Camera")
camera = bpy.data.objects.new("QA_Camera", camera_data)
scene.collection.objects.link(camera)
scene.camera = camera
camera.data.type = "ORTHO"

key_data = bpy.data.lights.new("QA_Key", type="AREA")
key_data.energy = 900.0
key_data.shape = "DISK"
key_data.size = 2.5
key = bpy.data.objects.new("QA_Key", key_data)
scene.collection.objects.link(key)
key.location = (0.0, -1.7, 1.6)
point_at(key, (0.0, 0.0, 0.0))

fill_data = bpy.data.lights.new("QA_Fill", type="AREA")
fill_data.energy = 500.0
fill_data.size = 2.0
fill = bpy.data.objects.new("QA_Fill", fill_data)
scene.collection.objects.link(fill)
fill.location = (-1.3, 1.4, 0.7)
point_at(fill, (0.0, 0.0, 0.0))

rim_data = bpy.data.lights.new("QA_Rim", type="AREA")
rim_data.energy = 650.0
rim_data.size = 1.5
rim = bpy.data.objects.new("QA_Rim", rim_data)
scene.collection.objects.link(rim)
rim.location = (1.2, 0.8, 1.3)
point_at(rim, (0.0, 0.0, 0.0))

views = {
    "side_minus_y": ((0.0, -2.2, 0.0), 1.12, "Y"),
    "side_plus_y": ((0.0, 2.2, 0.0), 1.12, "Y"),
    "axial_plus_x": ((2.2, 0.0, 0.0), 0.42, "Y"),
    "axial_minus_x": ((-2.2, 0.0, 0.0), 0.42, "Y"),
    "top_plus_z": ((0.0, 0.0, 2.2), 1.12, "Y"),
}

for name, (location, ortho_scale, track_up) in views.items():
    camera.location = location
    camera.data.ortho_scale = ortho_scale
    point_at(camera, (0.0, 0.0, 0.0), track_up)
    scene.render.filepath = str(OUT_DIR / f"{name}.png")
    bpy.ops.render.render(write_still=True)
    print(f"Rendered {scene.render.filepath}")
