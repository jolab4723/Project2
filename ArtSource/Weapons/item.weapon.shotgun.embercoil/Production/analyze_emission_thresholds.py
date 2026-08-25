from PIL import Image
import colorsys
import json
import numpy as np
from pathlib import Path


ROOT = Path(__file__).resolve().parent
BASE_COLOR = ROOT / "Textures" / "item.weapon.shotgun.embercoil_BaseColor.png"
QA_DIR = ROOT / "QA" / "EmissionThresholds"
QA_DIR.mkdir(parents=True, exist_ok=True)

rgb = np.array(Image.open(BASE_COLOR).convert("RGB"), dtype=np.uint8)
f = rgb.astype(np.float32) / 255.0
r, g, b = f[:, :, 0], f[:, :, 1], f[:, :, 2]
maximum = f.max(axis=2)
minimum = f.min(axis=2)
delta = maximum - minimum
hue = np.zeros_like(maximum)
valid = delta > 1e-6
sel = (maximum == r) & valid
hue[sel] = ((g[sel] - b[sel]) / delta[sel]) % 6.0
sel = (maximum == g) & valid
hue[sel] = (b[sel] - r[sel]) / delta[sel] + 2.0
sel = (maximum == b) & valid
hue[sel] = (r[sel] - g[sel]) / delta[sel] + 4.0
hue /= 6.0
saturation = np.zeros_like(maximum)
saturation[maximum > 1e-6] = delta[maximum > 1e-6] / maximum[maximum > 1e-6]

ri = rgb[:, :, 0].astype(np.int32)
gi = rgb[:, :, 1].astype(np.int32)
bi = rgb[:, :, 2].astype(np.int32)

candidates = {
    "hsv_h0_45_s75_v25": (hue <= 45.0 / 360.0) & (saturation >= 0.75) & (maximum >= 0.25) & (ri > gi) & (gi >= bi),
    "hsv_h0_35_s80_v25": (hue <= 35.0 / 360.0) & (saturation >= 0.80) & (maximum >= 0.25) & (ri > gi) & (gi >= bi),
    "rgb_r120_b65_rg55_gb0": (ri >= 120) & (bi <= 65) & ((ri - gi) >= 55) & (gi >= bi),
    "rgb_r140_b55_rg75_gb0": (ri >= 140) & (bi <= 55) & ((ri - gi) >= 75) & (gi >= bi),
    "rgb_r80_b55_rg50_gb0": (ri >= 80) & (bi <= 55) & ((ri - gi) >= 50) & (gi >= bi),
    "rgb_r200_b50_rg100_gb0": (ri >= 200) & (bi <= 50) & ((ri - gi) >= 100) & (gi >= bi),
    "rgb_r220_b45_rg115_gb0": (ri >= 220) & (bi <= 45) & ((ri - gi) >= 115) & (gi >= bi),
    "rgb_r235_b55_rg100_gb0": (ri >= 235) & (bi <= 55) & ((ri - gi) >= 100) & (gi >= bi),
    "rgb_r235_g60_b55_rg100_gb0": (ri >= 235) & (gi >= 60) & (bi <= 55) & ((ri - gi) >= 100) & (gi >= bi),
    "rgb_r235_g70_b55_rg100_gb0": (ri >= 235) & (gi >= 70) & (bi <= 55) & ((ri - gi) >= 100) & (gi >= bi),
    "rgb_r235_g80_b55_rg100_gb0": (ri >= 235) & (gi >= 80) & (bi <= 55) & ((ri - gi) >= 100) & (gi >= bi),
    "hsv_h0_35_s85_v80": (hue <= 35.0 / 360.0) & (saturation >= 0.85) & (maximum >= 0.80) & (ri > gi) & (gi >= bi),
    "distance_ff4a1a_60": (
        (ri - 255) ** 2 + (gi - 74) ** 2 + (bi - 26) ** 2 <= 60 ** 2
    ),
}

report = {"base_color": str(BASE_COLOR), "candidates": {}}
for name, mask in candidates.items():
    out = np.zeros((*mask.shape, 3), dtype=np.uint8)
    out[mask] = 255
    path = QA_DIR / f"{name}.png"
    Image.fromarray(out, mode="RGB").save(path)
    values = rgb[mask]
    report["candidates"][name] = {
        "path": str(path),
        "white_pixels": int(mask.sum()),
        "white_percent": float(mask.mean() * 100.0),
        "mean_selected_srgb": [float(value) for value in values.mean(axis=0)] if len(values) else None,
    }

with open(QA_DIR / "threshold_analysis.json", "w", encoding="utf-8") as handle:
    json.dump(report, handle, indent=2)
print(json.dumps(report, indent=2))
