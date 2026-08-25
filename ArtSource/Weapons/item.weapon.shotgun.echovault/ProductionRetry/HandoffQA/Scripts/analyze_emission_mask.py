from __future__ import annotations

import hashlib
import json
from collections import Counter
from pathlib import Path

import cv2
import numpy as np
from PIL import Image


HANDOFF = Path(__file__).resolve().parents[1]
PRODUCTION = HANDOFF.parent
TEXTURES = PRODUCTION / "Textures"
OUTPUT = HANDOFF / "Emission"
BASE_COLOR = TEXTURES / "BaseColor.png"
MASK_PATH = TEXTURES / "EmissionMask.png"
TARGET_SRGB = np.array([62, 217, 235], dtype=np.uint8)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    rgb = np.asarray(Image.open(BASE_COLOR).convert("RGB"), dtype=np.uint8)
    mask = np.asarray(Image.open(MASK_PATH).convert("L"), dtype=np.uint8)
    red = rgb[:, :, 0].astype(np.int16)
    green = rgb[:, :, 1].astype(np.int16)
    blue = rgb[:, :, 2].astype(np.int16)
    hsv = cv2.cvtColor(rgb, cv2.COLOR_RGB2HSV)

    threshold = (
        (green >= 175)
        & (blue >= 195)
        & (red <= 125)
        & ((green - red) >= 75)
        & ((blue - red) >= 90)
        & (hsv[:, :, 0] >= 82)
        & (hsv[:, :, 0] <= 100)
        & (hsv[:, :, 1] >= 100)
    )
    selected = mask == 255
    unique_values = np.unique(mask).tolist()
    mask_selected_not_threshold = int(np.logical_and(selected, ~threshold).sum())
    threshold_missing_from_mask = int(np.logical_and(threshold, ~selected).sum())

    component_count, _labels, stats, _centroids = cv2.connectedComponentsWithStats(
        selected.astype(np.uint8), connectivity=8
    )
    component_areas = sorted(
        (int(area) for area in stats[1:, cv2.CC_STAT_AREA]), reverse=True
    )

    selected_rgb = rgb[selected]
    top_colors = [
        {"rgb": list(color), "pixels": count}
        for color, count in Counter(map(tuple, selected_rgb.tolist())).most_common(20)
    ]
    channel_min = selected_rgb.min(axis=0).tolist()
    channel_max = selected_rgb.max(axis=0).tolist()
    channel_mean = [round(float(value), 6) for value in selected_rgb.mean(axis=0)]

    dimmed = (rgb.astype(np.float32) * 0.18).round().astype(np.uint8)
    overlay = dimmed.copy()
    overlay[selected] = TARGET_SRGB
    selected_only = np.zeros_like(rgb)
    selected_only[selected] = TARGET_SRGB
    Image.fromarray(overlay, mode="RGB").save(
        OUTPUT / "emission_rgb_overlay.png", format="PNG", optimize=True
    )
    Image.fromarray(selected_only, mode="RGB").save(
        OUTPUT / "emission_selected_only.png", format="PNG", optimize=True
    )

    result = {
        "source": {
            "base_color_path": "Textures/BaseColor.png",
            "base_color_sha256": sha256(BASE_COLOR),
            "mask_path": "Textures/EmissionMask.png",
            "mask_sha256": sha256(MASK_PATH),
            "size": list(mask.shape[::-1]),
        },
        "algorithm": {
            "space": "global BaseColor sRGB texels",
            "threshold": "G>=175; B>=195; R<=125; G-R>=75; B-R>=90; OpenCV HSV H=82..100; S>=100",
            "spatial_or_uv_restriction": False,
            "component_filter_used_to_build_mask": False,
            "morphology_blur_dilation_erosion_or_raycast": False,
        },
        "exactness": {
            "unique_mask_values": unique_values,
            "binary_0_255_only": unique_values == [0, 255],
            "selected_pixels": int(selected.sum()),
            "selected_percent": round(float(selected.mean() * 100.0), 8),
            "mask_selected_not_matching_rgb_threshold": mask_selected_not_threshold,
            "rgb_threshold_pixels_missing_from_mask": threshold_missing_from_mask,
            "exact_global_rgb_threshold_reproduction": mask_selected_not_threshold == 0
            and threshold_missing_from_mask == 0,
        },
        "selected_rgb_stats": {
            "min": channel_min,
            "max": channel_max,
            "mean": channel_mean,
            "unique_rgb_count": len(Counter(map(tuple, selected_rgb.tolist()))),
            "top_20": top_colors,
        },
        "analysis_only_connected_components": {
            "note": "Connected components are measured only for QA; they were not used to alter the mask.",
            "count": component_count - 1,
            "largest_20_areas": component_areas[:20],
            "components_area_le_4": sum(1 for area in component_areas if area <= 4),
        },
        "contamination_policy": {
            "accepted": "All authored BaseColor texels in the exact bright-cyan family, including small receiver-ring and grip-foot accents.",
            "excluded": "Gray, white, and weaker teal reflections outside the exact RGB/HSV thresholds.",
            "visual_confirmation": "Use five-view emission-only renders to confirm the main resonance strip plus only small same-family secondary accents.",
        },
        "previews": {
            "emission_rgb_overlay.png": sha256(OUTPUT / "emission_rgb_overlay.png"),
            "emission_selected_only.png": sha256(OUTPUT / "emission_selected_only.png"),
        },
    }
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
