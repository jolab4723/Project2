from __future__ import annotations

import argparse
import json
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter


def keep_components(mask: np.ndarray, minimum_area: int) -> tuple[np.ndarray, list[int]]:
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
    parser.add_argument("--mode", choices=("cyan", "sealed_red"), required=True)
    args = parser.parse_args()

    rgba = np.asarray(Image.open(args.base).convert("RGBA"), dtype=np.uint8)
    height, width = rgba.shape[:2]
    rgb = rgba[:, :, :3].astype(np.float32) / 255.0
    red, green, blue = rgb[:, :, 0], rgb[:, :, 1], rgb[:, :, 2]
    maximum = np.max(rgb, axis=2)
    minimum = np.min(rgb, axis=2)
    saturation = (maximum - minimum) / np.maximum(maximum, 1.0e-6)
    if args.mode == "cyan":
        chroma = (
            (blue > 0.48)
            & (green > 0.42)
            & (red < 0.26)
            & (blue > red * 2.25)
            & (green > red * 1.85)
            & (saturation > 0.48)
        )
        boost = np.array([0.30, 1.35, 1.55], dtype=np.float32)
        minimum_area = 12
        opening = 1
        thresholds = "B>0.48, G>0.42, R<0.26, B>2.25R, G>1.85R, saturation>0.48"
    else:
        chroma = (
            (red > 0.62)
            & (green < 0.24)
            & (blue < 0.18)
            & (red > green * 3.4)
            & (red > blue * 4.1)
            & (saturation > 0.74)
        )
        boost = np.array([1.55, 0.55, 0.35], dtype=np.float32)
        minimum_area = 300
        opening = 3
        thresholds = "R>0.62, G<0.24, B<0.18, R>3.4G, R>4.1B, saturation>0.74"

    region_image = Image.new("L", (width, height), 0)
    draw = ImageDraw.Draw(region_image)
    regions = json.loads(args.regions.read_text(encoding="utf-8"))
    for polygon in regions["uv_polygons"]:
        draw.polygon([(float(x), float(y)) for x, y in polygon], fill=255)
    region = np.asarray(region_image, dtype=np.uint8) > 0
    candidate = Image.fromarray(((chroma & region) * 255).astype(np.uint8), mode="L")
    if opening > 1:
        candidate = candidate.filter(ImageFilter.MinFilter(opening)).filter(ImageFilter.MaxFilter(opening))
    cleaned, component_areas = keep_components(
        np.asarray(candidate, dtype=np.uint8) > 0,
        minimum_area,
    )

    result = np.zeros_like(rgba)
    result[:, :, 3] = 255
    boosted = np.clip(rgb * boost, 0.0, 1.0)
    result[:, :, :3][cleaned] = np.round(boosted[cleaned] * 255.0).astype(np.uint8)
    Image.fromarray(result, mode="RGBA").save(args.output)
    selected = int(np.count_nonzero(cleaned))
    report = {
        "path": str(args.output),
        "size": [width, height],
        "colorspace": "sRGB",
        "mode": args.mode,
        "method": "object-space functional envelope -> original UV polygons -> hard color threshold -> connected components",
        "functional_envelope": regions["functional_envelope"],
        "uv_polygon_count": len(regions["uv_polygons"]),
        "selected_pixels": selected,
        "selected_fraction": round(selected / float(width * height), 8),
        "component_count": len(component_areas),
        "component_areas": component_areas,
        "thresholds_srgb": thresholds,
        "minimum_component_area": minimum_area,
        "opening_pixels": opening,
    }
    args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    if selected == 0:
        raise SystemExit("emission mask selected no pixels")


if __name__ == "__main__":
    main()
