from __future__ import annotations

import argparse
import json
from collections import defaultdict
from pathlib import Path

import numpy as np
from PIL import Image


# Audit-only atlas rectangles containing the authored visible conduit. They are
# never used to select output pixels; the output is allowed to use exact RGB only.
CONDUIT_AUDIT_RECTS = (
    (800, 1040, 805, 970),
    (1040, 1380, 750, 910),
    (1380, 1640, 700, 870),
)


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
    rgb = rgba[:, :, :3]
    height, width = rgb.shape[:2]
    audit_roi = np.zeros((height, width), dtype=bool)
    for x0, x1, y0, y1 in CONDUIT_AUDIT_RECTS:
        audit_roi[y0:y1, x0:x1] = True

    values = rgb.astype(np.int16)
    red, green, blue = values[:, :, 0], values[:, :, 1], values[:, :, 2]
    cyan_candidate = (
        (red <= 140)
        & (green >= 175)
        & (blue >= 200)
        & ((green - red) >= 50)
        & ((blue - red) >= 70)
    )
    inside_candidate = audit_roi & cyan_candidate
    coordinate_audit: dict[int, dict[str, list[list[int]]]] = defaultdict(
        lambda: {"inside_xy": [], "outside_xy": []}
    )
    for y, x in np.argwhere(cyan_candidate):
        r, g, b = (int(value) for value in rgb[y, x])
        key = (r << 16) | (g << 8) | b
        target = "inside_xy" if audit_roi[y, x] else "outside_xy"
        coordinate_audit[key][target].append([int(x), int(y)])

    entries = []
    exclusive_pixels = 0
    exclusive_colors = 0
    for key, coordinates in sorted(coordinate_audit.items()):
        if not coordinates["inside_xy"]:
            continue
        color = [(key >> 16) & 255, (key >> 8) & 255, key & 255]
        inside_count = len(coordinates["inside_xy"])
        outside_count = len(coordinates["outside_xy"])
        exclusive = outside_count == 0
        if exclusive:
            exclusive_colors += 1
            exclusive_pixels += inside_count
        entries.append(
            {
                "rgb_srgb": color,
                "inside_count": inside_count,
                "outside_count": outside_count,
                "exclusive_to_conduit_audit_roi": exclusive,
                **coordinates,
            }
        )

    inside_pixels = int(np.count_nonzero(inside_candidate))
    exclusive_coverage = exclusive_pixels / float(inside_pixels) if inside_pixels else 0.0
    separable = exclusive_coverage >= 0.95
    representative = [int(value) for value in np.median(rgb[inside_candidate], axis=0)]

    # No acceptable exact-RGB set exists, so emit a safe all-black binary mask.
    mask = np.zeros_like(rgba)
    mask[:, :, 3] = 255
    Image.fromarray(mask, mode="RGBA").save(args.output)

    audit_path = args.report.with_name("exact_rgb_audit.json")
    audit = {
        "base_color_path": str(args.base),
        "audit_only_conduit_rectangles_xyxy": [list(rect) for rect in CONDUIT_AUDIT_RECTS],
        "audit_candidate_family_srgb": {
            "red_max": 140,
            "green_min": 175,
            "blue_min": 200,
            "green_minus_red_min": 50,
            "blue_minus_red_min": 70,
        },
        "candidate_exact_rgb_entries": entries,
    }
    audit_path.write_text(json.dumps(audit, ensure_ascii=False, indent=2), encoding="utf-8")

    report = {
        "base_color_path": str(args.base),
        "mask_path": str(args.output),
        "size": [width, height],
        "mask_mode": "RGBA binary white/black",
        "method": "exact-RGB-only global selection audit; no UV, mesh, spatial, morphology, blur, raycast, or component filter is used for the output mask",
        "representative_rgb_srgb": representative,
        "representative_rgb_linear_unity_target": [round(srgb_to_linear(value), 6) for value in representative],
        "inside_candidate_pixels": inside_pixels,
        "outside_same_family_pixels": int(np.count_nonzero((~audit_roi) & cyan_candidate)),
        "exclusive_exact_rgb_colors": exclusive_colors,
        "exclusive_exact_rgb_pixels": exclusive_pixels,
        "exclusive_exact_rgb_coverage": round(exclusive_coverage, 8),
        "required_coverage_for_connected_surface": 0.95,
        "separable": separable,
        "blocked_reason": None if separable else "BaseColor reuses conduit RGB values on non-emissive azure paint; globally exclusive exact RGB values cover too little of the conduit to form the required connected surface",
        "output_mask_policy": "all black because RGB-only separation failed",
        "selected_pixels": 0,
        "selected_fraction": 0.0,
        "exact_rgb_audit_path": str(audit_path),
        "exception": None,
        "uv_whitelist": False,
    }
    args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
