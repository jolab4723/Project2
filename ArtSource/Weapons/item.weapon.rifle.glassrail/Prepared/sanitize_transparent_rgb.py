from __future__ import annotations

import argparse
from pathlib import Path

import numpy as np
from PIL import Image


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()

    image = Image.open(args.input).convert("RGBA")
    pixels = np.asarray(image, dtype=np.uint8).copy()
    transparent = pixels[:, :, 3] == 0
    hidden_rgb = transparent & np.any(pixels[:, :, :3] != 0, axis=2)
    changed = int(np.count_nonzero(hidden_rgb))
    pixels[transparent, :3] = 0
    Image.fromarray(pixels, mode="RGBA").save(args.output)
    print(f"sanitized_hidden_rgb_pixels={changed}")


if __name__ == "__main__":
    main()
