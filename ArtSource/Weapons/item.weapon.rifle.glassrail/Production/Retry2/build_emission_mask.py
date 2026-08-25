from __future__ import annotations

import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image


def srgb_to_linear(value: int) -> float:
    channel = value / 255.0
    return channel / 12.92 if channel <= 0.04045 else ((channel + 0.055) / 1.055) ** 2.4


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
        (red <= 65)
        & (green >= 210)
        & (blue >= 240)
        & ((green - red) >= 150)
        & ((blue - red) >= 175)
        & ((blue - green) >= 0)
        & ((blue - green) <= 50)
    )
    selected_rgb = rgb[selected]
    if selected_rgb.size == 0:
        raise RuntimeError("No authored cyan conduit pixels matched the global RGB family")

    mask = np.zeros_like(rgba)
    mask[:, :, :3] = np.where(selected[:, :, None], 255, 0)
    mask[:, :, 3] = 255
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.report.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(mask, mode="RGBA").save(args.output)
    representative = [int(value) for value in np.median(selected_rgb, axis=0)]
    binary_values = np.unique(mask[:, :, 0]).tolist()
    report = {
        "base_color_path": str(args.base.resolve()),
        "mask_path": str(args.output.resolve()),
        "size": [int(rgba.shape[1]), int(rgba.shape[0])],
        "mask_mode": "RGBA binary white/black",
        "method": "global BaseColor RGB-family selection only; no UV, mesh, spatial, morphology, blur, raycast, connected-component, or temporary emission geometry",
        "channel_thresholds_srgb": {
            "red_max": 65,
            "green_min": 210,
            "blue_min": 240,
            "green_minus_red_min": 150,
            "blue_minus_red_min": 175,
            "blue_minus_green_min": 0,
            "blue_minus_green_max": 50
        },
        "representative_rgb_srgb": representative,
        "representative_rgb_linear_unity_target": [round(srgb_to_linear(value), 6) for value in representative],
        "selected_pixels": int(np.count_nonzero(selected)),
        "selected_fraction": round(float(np.count_nonzero(selected)) / float(selected.size), 8),
        "selected_rgb_min": [int(value) for value in selected_rgb.min(axis=0)],
        "selected_rgb_max": [int(value) for value in selected_rgb.max(axis=0)],
        "binary_values": binary_values,
        "binary_mask": binary_values == [0, 255],
        "separable": True,
        "designated_surface": "single cyan-blue energy conduit beneath its transparent cover",
        "non_emissive_azure_exclusion": "enforced globally by green minimum and cyan channel-difference limits",
        "exception": None,
        "uv_whitelist": False
    }
    args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
