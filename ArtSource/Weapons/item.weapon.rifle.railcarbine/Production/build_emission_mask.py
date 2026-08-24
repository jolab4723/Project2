from __future__ import annotations

import argparse
import json
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter


def keep_large_components(mask: np.ndarray, minimum_area: int) -> tuple[np.ndarray, list[int]]:
    height, width = mask.shape
    pending = bytearray(mask.astype(np.uint8).ravel())
    kept = np.zeros(height * width, dtype=np.uint8)
    areas: list[int] = []
    for start in range(height * width):
        if not pending[start]:
            continue
        pending[start] = 0
        queue = deque([start])
        component = [start]
        while queue:
            index = queue.popleft()
            y, x = divmod(index, width)
            for neighbor in (
                index - 1 if x else -1,
                index + 1 if x + 1 < width else -1,
                index - width if y else -1,
                index + width if y + 1 < height else -1,
            ):
                if neighbor >= 0 and pending[neighbor]:
                    pending[neighbor] = 0
                    queue.append(neighbor)
                    component.append(neighbor)
        if len(component) >= minimum_area:
            kept[component] = 1
            areas.append(len(component))
    return kept.reshape((height, width)).astype(bool), sorted(areas, reverse=True)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--base", type=Path, required=True)
    parser.add_argument("--regions", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--report", type=Path, required=True)
    args = parser.parse_args()

    rgba = np.asarray(Image.open(args.base).convert("RGBA"), dtype=np.uint8)
    height, width = rgba.shape[:2]
    rgb = rgba[:, :, :3].astype(np.float32) / 255.0
    red, green, blue = rgb[:, :, 0], rgb[:, :, 1], rgb[:, :, 2]
    maximum = np.max(rgb, axis=2)
    minimum = np.min(rgb, axis=2)
    saturation = (maximum - minimum) / np.maximum(maximum, 1.0e-6)
    cyan = (
        (maximum > 0.12)
        & (saturation > 0.30)
        & (green > red * 1.32)
        & (blue > red * 1.38)
        & (green > blue * 0.70)
        & (blue > green * 0.80)
    )

    region_image = Image.new("L", (width, height), 0)
    draw = ImageDraw.Draw(region_image)
    regions = json.loads(args.regions.read_text(encoding="utf-8"))
    for polygon in regions["uv_polygons"]:
        draw.polygon([(float(x), float(y)) for x, y in polygon], fill=255)
    region = np.asarray(region_image, dtype=np.uint8) > 0

    candidate = Image.fromarray(((cyan & region) * 255).astype(np.uint8), mode="L")
    # Preserve the small UV islands that form the rail tips. The previous
    # erosion-first opening removed those islands and made an otherwise
    # continuous physical light strip fade into ordinary Base Color near its
    # ends. A small closing fills texel gaps while the functional-face region
    # still prevents unrelated cyan paint from entering the mask.
    closed = candidate.filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.MinFilter(5))
    closed_region = (np.asarray(closed, dtype=np.uint8) > 0) & region
    # The physical face envelope is already the exclusion boundary. Dropping
    # small UV islands here removed legitimate rail-tip and seam fragments, so
    # preserve every cyan component inside that envelope.
    cleaned, component_areas = keep_large_components(closed_region, minimum_area=1)
    # Reserve a guaranteed black border for non-functional faces collapsed by
    # the dedicated emission UV set. This prevents shared/mirrored atlas pixels
    # from lighting stock paint, bolts or the activation grip.
    cleaned[:8, :] = False
    cleaned[-8:, :] = False
    cleaned[:, :8] = False
    cleaned[:, -8:] = False

    result = np.zeros_like(rgba)
    result[:, :, 3] = 255
    glow = np.zeros_like(rgb)
    glow[:, :, 0] = 0.06
    glow[:, :, 1] = 0.92
    glow[:, :, 2] = 1.00
    result[:, :, :3][cleaned] = np.round(glow[cleaned] * 255.0).astype(np.uint8)
    Image.fromarray(result, mode="RGBA").save(args.output)

    selected = int(np.count_nonzero(cleaned))
    report = {
        "path": str(args.output),
        "size": [width, height],
        "colorspace": "sRGB",
        "method": "manual physical rail/core envelopes in root-local space -> dedicated emission UV; cyan hard threshold; 5px closing to bridge texel gaps; preserve all cyan UV islands inside the physical envelope; guaranteed black 8px atlas border",
        "functional_envelopes": regions["functional_envelopes"],
        "uv_polygon_count": len(regions["uv_polygons"]),
        "selected_pixels": selected,
        "selected_fraction": round(selected / float(width * height), 6),
        "component_count": len(component_areas),
        "component_areas": component_areas,
        "thresholds_srgb": "max>0.12, saturation>0.30, G>1.32R, B>1.38R, G>0.70B, B>0.80G",
        "minimum_component_area": 1,
    }
    args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
