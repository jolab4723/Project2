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
    chroma = (
        (maximum > 0.28)
        & (saturation > 0.42)
        & (blue > green * 1.30)
        & (red > green * 1.05)
    )

    region_image = Image.new("L", (width, height), 0)
    draw = ImageDraw.Draw(region_image)
    regions = json.loads(args.regions.read_text(encoding="utf-8"))
    for polygon in regions["uv_polygons"]:
        draw.polygon([(float(x), float(y)) for x, y in polygon], fill=255)
    region = np.asarray(region_image, dtype=np.uint8) > 0

    candidate = Image.fromarray(((chroma & region) * 255).astype(np.uint8), mode="L")
    opened = candidate.filter(ImageFilter.MinFilter(5)).filter(ImageFilter.MaxFilter(5))
    cleaned, component_areas = keep_large_components(
        np.asarray(opened, dtype=np.uint8) > 0,
        minimum_area=1800,
    )

    result = np.zeros_like(rgba)
    result[:, :, 3] = 255
    boosted = np.clip(
        rgb * np.array([1.45, 1.05, 1.80], dtype=np.float32),
        0.0,
        1.0,
    )
    result[:, :, :3][cleaned] = np.round(boosted[cleaned] * 255.0).astype(np.uint8)
    Image.fromarray(result, mode="RGBA").save(args.output)

    selected = int(np.count_nonzero(cleaned))
    report = {
        "path": str(args.output),
        "size": [width, height],
        "colorspace": "sRGB",
        "method": "manual functional core envelope in object space -> UV polygons; violet hard threshold; 5px opening; connected components >=1800px",
        "functional_envelope": regions["functional_envelope"],
        "uv_polygon_count": len(regions["uv_polygons"]),
        "selected_pixels": selected,
        "selected_fraction": round(selected / float(width * height), 6),
        "component_count": len(component_areas),
        "component_areas": component_areas,
        "thresholds_srgb": "max>0.28, saturation>0.42, B>1.30G, R>1.05G",
        "minimum_component_area": 1800,
    }
    args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
