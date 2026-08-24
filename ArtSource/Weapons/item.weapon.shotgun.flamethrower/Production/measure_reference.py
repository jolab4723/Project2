"""Read-only alpha/color QA helpers for approved weapon references."""

from __future__ import annotations

import argparse
from pathlib import Path

import numpy as np
from PIL import Image


def alpha_bbox(image: np.ndarray, roi: tuple[int, int, int, int]) -> tuple[int, int, int, int] | None:
    x0, y0, x1, y1 = roi
    ys, xs = np.where(image[y0:y1, x0:x1, 3] > 128)
    if len(xs) == 0:
        return None
    return int(xs.min() + x0), int(ys.min() + y0), int(xs.max() + x0 + 1), int(ys.max() + y0 + 1)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("image", type=Path)
    parser.add_argument("--roi", action="append", required=True, help="x0,y0,x1,y1")
    parser.add_argument("--row", action="append", type=int, default=[])
    args = parser.parse_args()
    image = np.array(Image.open(args.image).convert("RGBA"))
    for text in args.roi:
        roi = tuple(int(value) for value in text.split(","))
        if len(roi) != 4:
            raise ValueError(f"Invalid ROI: {text}")
        print(f"{text}: {alpha_bbox(image, roi)}")
    for row in args.row:
        occupied = image[row, :, 3] > 128
        starts = np.where(occupied & ~np.r_[False, occupied[:-1]])[0]
        ends = np.where(occupied & ~np.r_[occupied[1:], False])[0] + 1
        print(f"row {row}: {list(zip(starts.tolist(), ends.tolist()))}")


if __name__ == "__main__":
    main()
