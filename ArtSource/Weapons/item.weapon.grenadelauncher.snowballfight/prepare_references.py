from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image, ImageChops


ROOT = Path(__file__).resolve().parent
REFERENCES = ROOT / "References"
TRIPO_INPUT = ROOT / "Prepared" / "TripoInput"
CANVAS = (2048, 1024)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def normalize(source: Path, destination: Path, size: tuple[int, int]) -> None:
    image = Image.open(source).convert("RGBA")
    resized = image.resize(size, Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
    significant_alpha = resized.getchannel("A").point(lambda value: 255 if value > 16 else 0)
    bbox = significant_alpha.getbbox()
    if bbox is None:
        raise ValueError(f"No visible pixels in {source}")
    content_center = ((bbox[0] + bbox[2]) // 2, (bbox[1] + bbox[3]) // 2)
    offset = (CANVAS[0] // 2 - content_center[0], CANVAS[1] // 2 - content_center[1])
    canvas.alpha_composite(resized, offset)
    canvas.save(destination, format="PNG", optimize=True)


def analyze(path: Path) -> dict:
    image = Image.open(path).convert("RGBA")
    alpha = image.getchannel("A")
    bbox = alpha.getbbox()
    corners = [alpha.getpixel(point) for point in ((0, 0), (2047, 0), (0, 1023), (2047, 1023))]
    return {
        "path": path.relative_to(ROOT).as_posix(),
        "mode": image.mode,
        "size": list(image.size),
        "alpha_bbox": list(bbox) if bbox else None,
        "corner_alpha": corners,
        "transparent_pixel_count": sum(1 for value in alpha.get_flattened_data() if value == 0),
        "sha256": sha256(path),
        "pass": image.mode == "RGBA" and image.size == CANVAS and all(value == 0 for value in corners) and bbox is not None and bbox[0] > 0 and bbox[1] > 0 and bbox[2] < CANVAS[0] and bbox[3] < CANVAS[1],
    }


def main() -> None:
    TRIPO_INPUT.mkdir(parents=True, exist_ok=True)
    normalize(REFERENCES / "left.raw.png", REFERENCES / "left.png", (1600, 800))
    normalize(REFERENCES / "front.raw.png", REFERENCES / "front.png", (1116, 558))
    normalize(REFERENCES / "back.raw.png", REFERENCES / "back.png", (1116, 558))

    left = Image.open(REFERENCES / "left.png").convert("RGBA")
    right = left.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    right.save(REFERENCES / "right.png", format="PNG", optimize=True)

    for name in ("front.png", "left.png", "back.png", "right.png"):
        Image.open(REFERENCES / name).convert("RGBA").save(TRIPO_INPUT / name, format="PNG", optimize=True)

    mirror_diff = ImageChops.difference(
        Image.open(REFERENCES / "right.png").convert("RGBA"),
        Image.open(REFERENCES / "left.png").convert("RGBA").transpose(Image.Transpose.FLIP_LEFT_RIGHT),
    )
    report = {
        "item_id": "item.weapon.grenadelauncher.snowballfight",
        "canvas": list(CANVAS),
        "normalization": {
            "allowed_operations_only": ["uniform_resize", "transparent_padding"],
            "side_source_canvas_resized_to": [1600, 800],
            "end_source_canvas_resized_to": [1116, 558],
            "content_alignment": "alpha bounds centered by transparent translation",
            "background_extraction_or_thresholding": False,
        },
        "right_view": {
            "source": "pixel-exact horizontal mirror of final left.png",
            "pixel_exact_mirror": mirror_diff.getbbox() is None,
        },
        "files": {name: analyze(REFERENCES / name) for name in ("front.png", "left.png", "back.png", "right.png")},
        "prepared_copies": {
            name: {
                "path": (TRIPO_INPUT / name).relative_to(ROOT).as_posix(),
                "sha256": sha256(TRIPO_INPUT / name),
                "matches_reference": sha256(TRIPO_INPUT / name) == sha256(REFERENCES / name),
            }
            for name in ("front.png", "left.png", "back.png", "right.png")
        },
    }
    report["pass"] = (
        all(entry["pass"] for entry in report["files"].values())
        and all(entry["matches_reference"] for entry in report["prepared_copies"].values())
        and report["right_view"]["pixel_exact_mirror"]
    )
    (ROOT / "Prepared" / "visual_validation.json").write_text(json.dumps(report, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
