from __future__ import annotations

import colorsys
import json
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
TEXTURES = ROOT / "Production" / "Textures"
QA = ROOT / "Production" / "QA"
ITEM_ID = "item.weapon.rifle.smilesignal"

BASE_PATH = TEXTURES / f"{ITEM_ID}_BaseColor.png"
ORM_PATH = TEXTURES / f"{ITEM_ID}_ORM.png"
OCCLUSION_PATH = TEXTURES / f"{ITEM_ID}_Occlusion.png"
METALLIC_SMOOTHNESS_PATH = TEXTURES / f"{ITEM_ID}_MetallicSmoothness.png"
EMISSION_PATH = TEXTURES / f"{ITEM_ID}_Emission.png"
EMISSION_OVERLAY_PATH = QA / "emission_mask_overlay.png"
REPORT_PATH = QA / "emission_mask_report.json"


base = Image.open(BASE_PATH).convert("RGBA")
orm = Image.open(ORM_PATH).convert("RGB")
if base.size != orm.size:
    raise RuntimeError(f"Texture size mismatch: base={base.size}, orm={orm.size}")

orm_pixels = list(orm.getdata())
occlusion = Image.new("RGB", orm.size)
occlusion.putdata([(red, red, red) for red, _green, _blue in orm_pixels])
occlusion.save(OCCLUSION_PATH)

metallic_smoothness = Image.new("RGBA", orm.size)
metallic_smoothness.putdata(
    [(blue, 0, 0, 255 - green) for _red, green, blue in orm_pixels]
)
metallic_smoothness.save(METALLIC_SMOOTHNESS_PATH)

# Global RGB-family mask only: select saturated vivid red texels everywhere in
# the atlas.  No spatial, UV-island, dilation, or geometry-based exception is
# used.  White ceramic and the sticker's white hair can never satisfy it.
selected = []
base_pixels = list(base.getdata())
for red, green, blue, alpha in base_pixels:
    maximum = max(red, green, blue)
    minimum = min(red, green, blue)
    saturation = 0.0 if maximum == 0 else (maximum - minimum) / maximum
    is_emission_family = (
        alpha > 0
        and 90 <= red <= 175
        and red >= green * 1.75
        and red >= blue * 1.55
        and saturation >= 0.48
        and green <= 65
        and blue <= 65
    )
    selected.append(is_emission_family)

emission_pixels = [(255, 30, 48, 255) if flag else (0, 0, 0, 255) for flag in selected]
emission = Image.new("RGBA", base.size)
emission.putdata(emission_pixels)
emission.save(EMISSION_PATH)

overlay_pixels = []
for (red, green, blue, alpha), flag in zip(base_pixels, selected):
    if flag:
        overlay_pixels.append((255, 0, 255, 255))
    else:
        overlay_pixels.append((red // 3, green // 3, blue // 3, alpha))
overlay = Image.new("RGBA", base.size)
overlay.putdata(overlay_pixels)
overlay.save(EMISSION_OVERLAY_PATH)

selected_count = sum(selected)
total = len(selected)
report = {
    "status": "PASS" if 0 < selected_count / total <= 0.08 else "REVIEW",
    "method": "global RGB-family binary mask",
    "spatial_or_uv_restriction": False,
    "dilation_or_expansion": False,
    "emission_rgb": [255, 30, 48],
    "threshold": {
        "red_min": 90,
        "red_max": 175,
        "red_vs_green_min_ratio": 1.75,
        "red_vs_blue_min_ratio": 1.55,
        "saturation_min": 0.48,
        "green_max": 65,
        "blue_max": 65
    },
    "selected_pixels": selected_count,
    "total_pixels": total,
    "selected_ratio": round(selected_count / total, 8),
    "white_body_can_match": False,
    "white_sticker_hair_can_match": False,
    "base_color": str(BASE_PATH.relative_to(ROOT)),
    "emission_map": str(EMISSION_PATH.relative_to(ROOT)),
    "overlay_qa": str(EMISSION_OVERLAY_PATH.relative_to(ROOT)),
    "occlusion_map": str(OCCLUSION_PATH.relative_to(ROOT)),
    "metallic_smoothness_map": str(METALLIC_SMOOTHNESS_PATH.relative_to(ROOT))
}
REPORT_PATH.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(report, ensure_ascii=False, indent=2))
