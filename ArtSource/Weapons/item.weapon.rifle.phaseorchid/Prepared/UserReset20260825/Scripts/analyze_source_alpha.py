from __future__ import annotations

import json
import sys
from collections import Counter
from pathlib import Path

from PIL import Image


def threshold_bbox(alpha: Image.Image, threshold: int) -> tuple[int, int, int, int] | None:
    mask = alpha.point(lambda value: 255 if value >= threshold else 0, mode="1")
    return mask.getbbox()


def analyze(path: Path) -> dict[str, object]:
    with Image.open(path) as source:
        rgba = source.convert("RGBA")
    alpha = rgba.getchannel("A")
    counts = Counter(alpha.getdata())
    return {
        "path": str(path),
        "mode": source.mode,
        "size": list(rgba.size),
        "alpha_unique_count": len(counts),
        "alpha_zero_pixels": counts[0],
        "alpha_255_pixels": counts[255],
        "alpha_partial_pixels": sum(count for value, count in counts.items() if 0 < value < 255),
        "bbox_alpha_gt_0": list(alpha.getbbox() or ()),
        "bbox_alpha_ge_64": list(threshold_bbox(alpha, 64) or ()),
        "bbox_alpha_ge_128": list(threshold_bbox(alpha, 128) or ()),
        "bbox_alpha_ge_192": list(threshold_bbox(alpha, 192) or ()),
    }


if __name__ == "__main__":
    print(json.dumps([analyze(Path(raw)) for raw in sys.argv[1:]], indent=2))
