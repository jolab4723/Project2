from __future__ import annotations

import math
from pathlib import Path

from PIL import Image, ImageDraw


SOURCE = Path(
    r"C:\Users\user\Project2\Assets\WBHTest\Asset\sTargeting\Textures\ring target 8.png"
)
SCALE = 4


def scaled(points: list[tuple[float, float]]) -> list[tuple[int, int]]:
    return [(round(x * SCALE), round(y * SCALE)) for x, y in points]


def rotate(
    points: list[tuple[float, float]], angle_degrees: float
) -> list[tuple[float, float]]:
    angle = math.radians(angle_degrees)
    cosine = math.cos(angle)
    sine = math.sin(angle)
    result: list[tuple[float, float]] = []
    for x, y in points:
        dx = x - 256
        dy = y - 256
        result.append(
            (
                256 + dx * cosine - dy * sine,
                256 + dx * sine + dy * cosine,
            )
        )
    return result


def draw_sword(draw: ImageDraw.ImageDraw, angle: float) -> None:
    # Base sword points to the right. Rotations place it at all four cardinals.
    blade = [
        (510, 256),
        (461, 242),
        (451, 248),
        (451, 264),
        (461, 270),
    ]
    guard = [
        (446, 224),
        (456, 230),
        (456, 244),
        (466, 249),
        (466, 263),
        (456, 268),
        (456, 282),
        (446, 288),
        (446, 268),
        (438, 263),
        (438, 249),
        (446, 244),
    ]
    grip = [(414, 249), (446, 249), (446, 263), (414, 263)]
    pommel = [(401, 256), (409, 247), (418, 256), (409, 265)]

    for shape in (blade, guard, grip, pommel):
        draw.polygon(scaled(rotate(shape, angle)), fill=255)

    # Hilt grooves keep the silhouette readable at floor-indicator scale.
    for x in (422, 432):
        groove = [(x, 249), (x + 3, 249), (x + 3, 263), (x, 263)]
        draw.polygon(scaled(rotate(groove, angle)), fill=0)


def main() -> None:
    with Image.open(SOURCE) as original:
        if original.size != (512, 512):
            raise ValueError(f"Unexpected source size: {original.size}")
        luminance = original.convert("L").getextrema()[1]

    mask = Image.new("L", (512 * SCALE, 512 * SCALE), 0)
    draw = ImageDraw.Draw(mask)
    ring_box = scaled([(42, 42), (470, 470)])

    # Four arc sections leave clean slots for the sword hilts.
    for start, end in ((12, 78), (102, 168), (192, 258), (282, 348)):
        draw.arc(ring_box, start=start, end=end, fill=255, width=15 * SCALE)

    for angle in (0, 90, 180, 270):
        draw_sword(draw, angle)

    mask = mask.resize((512, 512), Image.Resampling.LANCZOS)
    light = Image.new("L", (512, 512), luminance)
    result = Image.merge("LA", (light, mask))

    temporary = SOURCE.with_name(f"{SOURCE.stem}.codex_tmp.png")
    result.save(temporary, "PNG", optimize=True)

    with Image.open(temporary) as check:
        if check.size != (512, 512) or check.mode != "LA":
            raise ValueError(f"Invalid output: {check.size}, {check.mode}")

    temporary.replace(SOURCE)


if __name__ == "__main__":
    main()
