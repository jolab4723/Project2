"""Prepare built-in ImageGen outputs for the exact Project2 reference canvas."""

from __future__ import annotations

import argparse
from pathlib import Path

import cv2
import numpy as np
from PIL import Image


def extract_background(rgb: np.ndarray) -> np.ndarray:
    """Extract one connected weapon from a generated flat/checker background."""
    rgb_i = rgb.astype(np.int16)
    border = np.concatenate(
        (rgb_i[0], rgb_i[-1], rgb_i[:, 0], rgb_i[:, -1]), axis=0
    )
    if float(np.median(border)) < 20.0:
        rough = (rgb_i.max(axis=2) > 8).astype(np.uint8)
    else:
        low = rgb_i.min(axis=2)
        chroma = rgb_i.max(axis=2) - low
        score = np.maximum(0, 243 - low) + (2 * chroma)
        rough = (score > 10).astype(np.uint8)

    count, labels, stats, _ = cv2.connectedComponentsWithStats(rough, 8)
    if count < 2:
        raise RuntimeError("No foreground component found")
    largest = 1 + int(np.argmax(stats[1:, cv2.CC_STAT_AREA]))
    solid = (labels == largest).astype(np.uint8)

    # ImageGen may paint a one-pixel white keyline just inside a light backdrop.
    # Remove only near-white pixels touching the extracted outer boundary.
    initial_inside = cv2.distanceTransform(solid, cv2.DIST_L2, 3)
    white_keyline = (rgb.min(axis=2) > 235) & (initial_inside <= 4.0)
    solid[white_keyline] = 0

    inside = cv2.distanceTransform(solid, cv2.DIST_L2, 3)
    outside = cv2.distanceTransform(1 - solid, cv2.DIST_L2, 3)
    signed = inside - outside
    return np.clip((signed.astype(np.float64) + 0.5) * 255.0, 0, 255).astype(
        np.uint8
    )


def defringe_rgb(rgb: np.ndarray, alpha: np.ndarray) -> np.ndarray:
    """Bleed opaque surface colors into antialiased edge pixels."""
    surface = (alpha >= 250) & (rgb.min(axis=2) <= 235)
    if not np.any(surface):
        return rgb
    _, nearest = cv2.distanceTransformWithLabels(
        (~surface).astype(np.uint8),
        cv2.DIST_L2,
        3,
        labelType=cv2.DIST_LABEL_PIXEL,
    )
    surface_colors = rgb[surface]
    result = rgb.copy()
    inside = cv2.distanceTransform((alpha > 8).astype(np.uint8), cv2.DIST_L2, 3)
    edge = (alpha < 250) | ((rgb.min(axis=2) > 235) & (inside <= 2.0))
    result[edge] = surface_colors[nearest[edge] - 1]
    return result


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("destination", type=Path)
    parser.add_argument("--target-alpha-width", type=int)
    parser.add_argument("--mirror", action="store_true")
    args = parser.parse_args()

    image = Image.open(args.source)
    rgb = np.asarray(image.convert("RGB"), dtype=np.uint8)
    if image.mode == "RGBA":
        alpha = np.asarray(image.getchannel("A"), dtype=np.uint8)
        if not args.mirror:
            count, labels, stats, _ = cv2.connectedComponentsWithStats(
                (alpha > 8).astype(np.uint8), 8
            )
            if count > 1:
                largest = 1 + int(np.argmax(stats[1:, cv2.CC_STAT_AREA]))
                alpha = np.where(labels == largest, alpha, 0).astype(np.uint8)
    else:
        alpha = extract_background(rgb)
        rgb = defringe_rgb(rgb, alpha)

    rgba = Image.fromarray(np.dstack((rgb, alpha)), "RGBA")
    if args.mirror:
        rgba = rgba.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    if args.target_alpha_width:
        bbox = rgba.getchannel("A").getbbox()
        if bbox is None:
            raise RuntimeError("No foreground alpha found")
        crop = rgba.crop(bbox)
        scale = args.target_alpha_width / crop.width
        resized = crop.resize(
            (args.target_alpha_width, round(crop.height * scale)), Image.Resampling.LANCZOS
        )
        rgba = resized
    if rgba.width > 2048 or rgba.height > 1024:
        raise RuntimeError(f"Source exceeds target canvas: {rgba.size}")

    canvas = Image.new("RGBA", (2048, 1024), (0, 0, 0, 0))
    offset = ((2048 - rgba.width) // 2, (1024 - rgba.height) // 2)
    canvas.alpha_composite(rgba, offset)
    args.destination.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(args.destination, format="PNG")

    final_alpha = np.asarray(canvas.getchannel("A"), dtype=np.uint8)
    print(
        {
            "source": str(args.source),
            "destination": str(args.destination),
            "source_size": image.size,
            "source_mode": image.mode,
            "final_size": canvas.size,
            "final_mode": canvas.mode,
            "offset": offset,
            "alpha_extrema": (int(final_alpha.min()), int(final_alpha.max())),
            "alpha_bbox": canvas.getchannel("A").getbbox(),
        }
    )


if __name__ == "__main__":
    main()
