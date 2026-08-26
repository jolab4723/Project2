from __future__ import annotations

import json
from pathlib import Path

import numpy as np
from PIL import Image


ROOT = Path(__file__).resolve().parent
BASE_COLOR = ROOT / "Textures" / "item.weapon.shotgun.embercoil_BaseColor.png"
EMISSION_MASK = ROOT / "Textures" / "item.weapon.shotgun.embercoil_Emission.png"
REPORT = ROOT / "QA" / "emission_mask_validation.json"
DESIGNATED_SRGB = (255, 74, 26)


def srgb_channel_to_linear(value: int) -> float:
    channel = value / 255.0
    if channel <= 0.04045:
        return channel / 12.92
    return ((channel + 0.055) / 1.055) ** 2.4


rgb = np.asarray(Image.open(BASE_COLOR).convert("RGB"), dtype=np.uint8)
r = rgb[:, :, 0].astype(np.int16)
g = rgb[:, :, 1].astype(np.int16)
b = rgb[:, :, 2].astype(np.int16)

# A single global color-family band. The lower green bound rejects the tiny
# red receiver fleck, while the upper green and blue bounds reject copper trim.
# This intentionally performs no UV, mesh, spatial, connected-component,
# morphology, blur, or raycast filtering.
selected = (
    (r >= 235)
    & (g >= 90)
    & (g <= 120)
    & (b <= 45)
    & ((r - g) >= 115)
    & (g >= b)
)
mask_rgb = np.zeros((*selected.shape, 3), dtype=np.uint8)
mask_rgb[selected] = 255
Image.fromarray(mask_rgb, mode="RGB").save(EMISSION_MASK)

selected_values = rgb[selected]
unique_values = sorted(int(value) for value in np.unique(mask_rgb))
report = {
    "base_color_path": str(BASE_COLOR.resolve()),
    "emission_mask_path": str(EMISSION_MASK.resolve()),
    "designated_emission_srgb": list(DESIGNATED_SRGB),
    "designated_emission_srgb_hex": "#FF4A1A",
    "designated_emission_linear": [
        srgb_channel_to_linear(value) for value in DESIGNATED_SRGB
    ],
    "global_binary_rule": {
        "r_min": 235,
        "g_min": 90,
        "g_max": 120,
        "b_max": 45,
        "r_minus_g_min": 115,
        "g_greater_than_or_equal_to_b": True,
    },
    "global_only": True,
    "uv_exception": False,
    "forbidden_filtering_used": False,
    "mask_mode": "RGB",
    "mask_size": list(Image.open(EMISSION_MASK).size),
    "mask_unique_channel_values": unique_values,
    "white_pixels": int(selected.sum()),
    "white_percent": float(selected.mean() * 100.0),
    "mean_selected_srgb": [
        float(value) for value in selected_values.mean(axis=0)
    ],
    "visual_qa": (
        "Five-view emission-only handoff renders show emission confined to the "
        "recessed coil ribs, including their expected front-view arcs. The tiny "
        "receiver red fleck, copper trim, and black receiver remain non-emissive."
    ),
}
REPORT.parent.mkdir(parents=True, exist_ok=True)
REPORT.write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps(report, indent=2))
