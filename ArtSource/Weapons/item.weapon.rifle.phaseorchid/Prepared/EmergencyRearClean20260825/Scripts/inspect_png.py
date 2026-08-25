from __future__ import annotations

import json
import sys
from pathlib import Path

from PIL import Image


def inspect(path: Path) -> dict[str, object]:
    with Image.open(path) as source:
        rgba = source.convert("RGBA")
        alpha = rgba.getchannel("A")
        corners = [
            rgba.getpixel((0, 0)),
            rgba.getpixel((rgba.width - 1, 0)),
            rgba.getpixel((0, rgba.height - 1)),
            rgba.getpixel((rgba.width - 1, rgba.height - 1)),
        ]
        bbox = alpha.getbbox()
        return {
            "path": str(path),
            "source_mode": source.mode,
            "size": list(rgba.size),
            "alpha_extrema": list(alpha.getextrema()),
            "corners_rgba": [list(pixel) for pixel in corners],
            "alpha_bbox": list(bbox) if bbox else None,
        }


if __name__ == "__main__":
    print(json.dumps([inspect(Path(raw)) for raw in sys.argv[1:]], indent=2))
