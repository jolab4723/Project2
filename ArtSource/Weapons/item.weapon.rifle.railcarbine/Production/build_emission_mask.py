from __future__ import annotations

import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image


# User-confirmed cyan glow material sampled from the supplied 13 x 27 PNG.
# Representative color: RGB(100, 231, 239). The wider limits include the
# shaded pixels of that same cyan material, not a second color family.
MAX_RED = 140
MIN_GREEN = 150
MIN_BLUE = 170
MIN_GREEN_OVER_RED = 45
MIN_BLUE_OVER_RED = 55


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--base", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--report", type=Path, required=True)
    args = parser.parse_args()

    rgba = np.asarray(Image.open(args.base).convert("RGBA"), dtype=np.uint8)
    rgb = rgba[:, :, :3].astype(np.int16)
    red, green, blue = rgb[:, :, 0], rgb[:, :, 1], rgb[:, :, 2]
    selected = (
        (red <= MAX_RED)
        & (green >= MIN_GREEN)
        & (blue >= MIN_BLUE)
        & ((green - red) >= MIN_GREEN_OVER_RED)
        & ((blue - red) >= MIN_BLUE_OVER_RED)
    )

    # Binary emission mask: confirmed glow color is white; everything else is black.
    result = np.zeros_like(rgba)
    result[:, :, 3] = 255
    result[:, :, :3][selected] = 255
    Image.fromarray(result, mode="RGBA").save(args.output)

    selected_pixels = int(np.count_nonzero(selected))
    height, width = selected.shape
    report = {
        "path": str(args.output),
        "size": [width, height],
        "colorspace": "sRGB",
        "method": "global user-confirmed cyan material RGB selection to binary white emission mask; includes shaded pixels of the same material; no Blender geometry, UV envelope, morphology, blur, or component filtering",
        "representative_rgb_srgb": [100, 231, 239],
        "selection_rule_srgb": {
            "red_max": MAX_RED,
            "green_min": MIN_GREEN,
            "blue_min": MIN_BLUE,
            "green_minus_red_min": MIN_GREEN_OVER_RED,
            "blue_minus_red_min": MIN_BLUE_OVER_RED,
        },
        "mask_on_rgb_srgb": [255, 255, 255],
        "mask_off_rgb_srgb": [0, 0, 0],
        "selected_pixels": selected_pixels,
        "selected_fraction": round(selected_pixels / float(width * height), 8),
    }
    args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
