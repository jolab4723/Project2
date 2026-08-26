from __future__ import annotations

import json
from pathlib import Path

import numpy as np
from PIL import Image


ITEM_ID = "item.weapon.shotgun.embercoil"
QA_ROOT = Path(__file__).resolve().parent
BASE_COLOR = QA_ROOT.parent / "Textures" / f"{ITEM_ID}_BaseColor.png"
OUT_DIR = QA_ROOT / "GlobalEmissionCandidates"
REPORT_PATH = OUT_DIR / "candidate_rules.json"

rgb = np.asarray(Image.open(BASE_COLOR).convert("RGB"), dtype=np.uint8)
ri = rgb[:, :, 0].astype(np.int16)
gi = rgb[:, :, 1].astype(np.int16)
bi = rgb[:, :, 2].astype(np.int16)
rf = ri.astype(np.float32) / 255.0
gf = gi.astype(np.float32) / 255.0
bf = bi.astype(np.float32) / 255.0
maximum = np.maximum(np.maximum(rf, gf), bf)
minimum = np.minimum(np.minimum(rf, gf), bf)
delta = maximum - minimum
hue = np.zeros_like(maximum)
valid = delta > 1e-6
red_max = (maximum == rf) & valid
hue[red_max] = ((gf[red_max] - bf[red_max]) / delta[red_max]) % 6.0
hue_degrees = hue * 60.0
saturation = np.zeros_like(maximum)
saturation[maximum > 1e-6] = delta[maximum > 1e-6] / maximum[maximum > 1e-6]

rules = {
    "g90_120_b45_rg115": (ri >= 235) & (gi >= 90) & (gi <= 120) & (bi <= 45) & ((ri - gi) >= 115) & (gi >= bi),
    "g90_115_b40_rg120": (ri >= 235) & (gi >= 90) & (gi <= 115) & (bi <= 40) & ((ri - gi) >= 120) & (gi >= bi),
    "g95_125_b45_rg110": (ri >= 235) & (gi >= 95) & (gi <= 125) & (bi <= 45) & ((ri - gi) >= 110) & (gi >= bi),
    "g100_130_b50_rg105": (ri >= 235) & (gi >= 100) & (gi <= 130) & (bi <= 50) & ((ri - gi) >= 105) & (gi >= bi),
    "h8_20_s75_r235": (hue_degrees >= 8) & (hue_degrees <= 20) & (saturation >= 0.75) & (ri >= 235),
    "h10_20_s80_r235": (hue_degrees >= 10) & (hue_degrees <= 20) & (saturation >= 0.80) & (ri >= 235),
    "h10_18_s80_r235": (hue_degrees >= 10) & (hue_degrees <= 18) & (saturation >= 0.80) & (ri >= 235),
}

OUT_DIR.mkdir(parents=True, exist_ok=True)
report = {"item_id": ITEM_ID, "global_only": True, "candidates": {}}
for name, selected in rules.items():
    mask = np.zeros((*selected.shape, 3), dtype=np.uint8)
    mask[selected] = 255
    path = OUT_DIR / f"{name}.png"
    Image.fromarray(mask, mode="RGB").save(path, optimize=True)
    values = rgb[selected]
    report["candidates"][name] = {
        "path": str(path.resolve()),
        "white_pixels": int(selected.sum()),
        "white_percent": float(selected.mean() * 100.0),
        "mean_selected_srgb": [float(value) for value in values.mean(axis=0)] if len(values) else None,
    }

REPORT_PATH.write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps(report, indent=2))
