from __future__ import annotations

import json
from pathlib import Path

import bpy


ROOT = Path(__file__).resolve().parents[1]
GLB = ROOT / "Tripo" / "RetryAfterExpired20260825" / "Downloaded" / "item.weapon.rifle.smilesignal_raw.glb"
OUT = ROOT / "Production" / "Textures"
REPORT = ROOT / "Production" / "QA" / "texture_extraction.json"
ITEM_ID = "item.weapon.rifle.smilesignal"


def find_image(prefix: str):
    image = next((candidate for candidate in bpy.data.images if candidate.name.startswith(prefix)), None)
    if image is None:
        raise RuntimeError(f"Missing embedded texture: {prefix}")
    return image


def save(image, suffix: str, colorspace: str):
    destination = OUT / f"{ITEM_ID}_{suffix}.png"
    image.colorspace_settings.name = colorspace
    image.filepath_raw = str(destination)
    image.file_format = "PNG"
    image.save()
    return {
        "source": image.name,
        "path": str(destination.relative_to(ROOT)),
        "size": list(image.size),
        "colorspace": colorspace,
    }


OUT.mkdir(parents=True, exist_ok=True)
REPORT.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(GLB))
records = {
    "base_color": save(find_image("Color_"), "BaseColor", "sRGB"),
    "orm": save(find_image("ORM_"), "ORM", "Non-Color"),
    "normal": save(find_image("NormalGL_"), "Normal", "Non-Color"),
}
REPORT.write_text(json.dumps(records, ensure_ascii=False, indent=2), encoding="utf-8")
print(REPORT)
