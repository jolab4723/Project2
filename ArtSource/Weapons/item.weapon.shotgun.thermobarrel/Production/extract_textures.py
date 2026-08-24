from pathlib import Path

import bpy


ITEM_DIR = Path(__file__).resolve().parents[1]
RAW_GLB = ITEM_DIR / "Tripo" / "Downloaded" / "item.weapon.shotgun.thermobarrel_raw.glb"
OUT_DIR = ITEM_DIR / "Production" / "Textures"

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(RAW_GLB))
OUT_DIR.mkdir(parents=True, exist_ok=True)

name_map = {
    "Color": "item.weapon.shotgun.thermobarrel_BaseColor.png",
    "NormalGL": "item.weapon.shotgun.thermobarrel_NormalGL.png",
    "ORM": "item.weapon.shotgun.thermobarrel_ORM.png",
}

for image in bpy.data.images:
    target_name = next((output for prefix, output in name_map.items() if image.name.startswith(prefix)), None)
    if target_name is None:
        continue
    target = OUT_DIR / target_name
    image.filepath_raw = str(target)
    image.file_format = "PNG"
    image.save()
    print(f"Saved {image.name} -> {target}")
