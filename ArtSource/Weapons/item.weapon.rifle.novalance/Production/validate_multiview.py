from __future__ import annotations

import hashlib
import json
from pathlib import Path

import cv2
import numpy as np
from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
INPUT_DIR = ROOT / "Prepared" / "TripoInput"
OUTPUT = ROOT / "Production" / "reference_validation.generated.json"


def validate_view(name: str) -> dict:
    path = INPUT_DIR / f"{name}.png"
    raw = path.read_bytes()
    with Image.open(path) as image:
        image.load()
        rgba = np.asarray(image)
        alpha = rgba[:, :, 3]
        red = rgba[:, :, 0].astype(np.float32)
        green = rgba[:, :, 1].astype(np.float32)
        blue = rgba[:, :, 2].astype(np.float32)

        outside = (alpha < 128).astype(np.uint8)
        distance = cv2.distanceTransform(outside, cv2.DIST_L2, 5)
        saturated_blue = (blue >= 64) & (blue > red * 1.25) & (blue > green * 1.10)
        low_alpha_fringe = saturated_blue & (alpha > 0) & (alpha <= 64) & (distance > 3.0)
        count, labels, stats, _ = cv2.connectedComponentsWithStats(low_alpha_fringe.astype(np.uint8), 8)
        largest_component = int(stats[1:, cv2.CC_STAT_AREA].max()) if count > 1 else 0
        max_distance = float(distance[low_alpha_fringe].max()) if low_alpha_fringe.any() else 0.0

        return {
            "path": str(path.relative_to(ROOT)),
            "mode": image.mode,
            "size": list(image.size),
            "alpha_extrema": list(image.getchannel("A").getextrema()),
            "genuine_transparency": bool(alpha.min() == 0 and alpha.max() == 255),
            "sha256": hashlib.sha256(raw).hexdigest(),
            "size_bytes": len(raw),
            "blue_low_alpha_fringe": {
                "pixels": int(low_alpha_fringe.sum()),
                "largest_connected_component_px": largest_component,
                "max_distance_px": round(max_distance, 2),
            },
        }


views = {name: validate_view(name) for name in ("front", "left", "back", "right")}
checks = {
    "all_rgba": all(view["mode"] == "RGBA" for view in views.values()),
    "all_2048x1024": all(view["size"] == [2048, 1024] for view in views.values()),
    "all_genuine_transparency": all(view["genuine_transparency"] for view in views.values()),
    "no_coherent_blue_halo": all(
        view["blue_low_alpha_fringe"]["largest_connected_component_px"] < 64
        for view in views.values()
    ),
}
report = {"views": views, "checks": checks, "pass": all(checks.values())}
OUTPUT.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(report, ensure_ascii=False, indent=2))
if not report["pass"]:
    raise SystemExit(1)
