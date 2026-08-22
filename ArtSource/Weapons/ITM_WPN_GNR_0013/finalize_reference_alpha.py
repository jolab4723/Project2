import json
from pathlib import Path

import numpy as np
from PIL import Image


ROOT = Path(__file__).resolve().parent
REFERENCES = ROOT / "References"
SEGMENTED = ROOT / "Prepared" / "BiRefNet"
TRANSPARENT = ROOT / "Prepared" / "Transparent"
QA = ROOT / "QA" / "ReferenceAlpha"

ALPHA_BACKGROUND_MAX = 16
ALPHA_FOREGROUND_MIN = 192


def make_checkerboard(width: int, height: int, cell: int = 48) -> Image.Image:
    y, x = np.indices((height, width))
    selector = ((x // cell) + (y // cell)) % 2
    dark = np.array([34, 41, 48, 255], dtype=np.uint8)
    light = np.array([91, 102, 112, 255], dtype=np.uint8)
    pixels = np.where(selector[..., None] == 0, dark, light)
    return Image.fromarray(pixels, "RGBA")


def main() -> None:
    TRANSPARENT.mkdir(parents=True, exist_ok=True)
    QA.mkdir(parents=True, exist_ok=True)
    report = {
        "alpha_background_max": ALPHA_BACKGROUND_MAX,
        "alpha_foreground_min": ALPHA_FOREGROUND_MIN,
        "views": {},
    }

    for view_name in ("left", "front", "back", "right"):
        with Image.open(REFERENCES / f"{view_name}.png") as source_image:
            source = np.asarray(source_image.convert("RGB"), dtype=np.uint8)
        with Image.open(SEGMENTED / f"{view_name}.png") as segmented_image:
            segmented = np.asarray(segmented_image.convert("RGBA"), dtype=np.uint8)

        source_alpha = segmented[..., 3].astype(np.float32)
        normalized_alpha = np.clip(
            (source_alpha - ALPHA_BACKGROUND_MAX)
            * (255.0 / (ALPHA_FOREGROUND_MIN - ALPHA_BACKGROUND_MAX)),
            0,
            255,
        ).astype(np.uint8)

        colors = source.copy()
        edge_pixels = (normalized_alpha > 0) & (normalized_alpha < 255)
        colors[edge_pixels] = segmented[..., :3][edge_pixels]
        colors[normalized_alpha == 0] = 0

        rgba = np.dstack((colors, normalized_alpha))
        transparent_image = Image.fromarray(rgba, "RGBA")
        output_path = TRANSPARENT / f"{view_name}.png"
        transparent_image.save(output_path)

        checker = make_checkerboard(transparent_image.width, transparent_image.height)
        checker.alpha_composite(transparent_image)
        checker.save(QA / f"{view_name}_checker.png")

        alpha_image = Image.fromarray(normalized_alpha, "L")
        report["views"][view_name] = {
            "size": [transparent_image.width, transparent_image.height],
            "alpha_extrema": list(alpha_image.getextrema()),
            "alpha_bbox": list(alpha_image.getbbox() or (0, 0, 0, 0)),
            "transparent_pixels": int(np.count_nonzero(normalized_alpha == 0)),
            "edge_pixels": int(np.count_nonzero(edge_pixels)),
            "opaque_pixels": int(np.count_nonzero(normalized_alpha == 255)),
        }

    report_path = ROOT / "Prepared" / "reference_alpha_validation.json"
    report_path.write_text(
        json.dumps(report, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    print(report_path)
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
