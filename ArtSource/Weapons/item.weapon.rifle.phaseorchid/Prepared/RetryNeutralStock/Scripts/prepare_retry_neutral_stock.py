from __future__ import annotations

import hashlib
import json
from collections import deque
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ITEM_ROOT = Path(__file__).resolve().parents[3]
ACCEPTED = ITEM_ROOT / "Prepared" / "TripoInput"
OUTPUT = ITEM_ROOT / "Prepared" / "RetryNeutralStock"
GENERATED_REAR = OUTPUT / "SourceGenerated" / "rear_neutral_v2_rgb_checker.png"
TARGET_SIZE = (2048, 1024)
TARGET_REAR_HEIGHT = 323


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def zero_hidden_rgb(image: Image.Image) -> Image.Image:
    rgba = image.convert("RGBA")
    pixels = rgba.load()
    for y in range(rgba.height):
        for x in range(rgba.width):
            red, green, blue, alpha = pixels[x, y]
            if alpha == 0 and (red or green or blue):
                pixels[x, y] = (0, 0, 0, 0)
    return rgba


def keep_largest_visible_component(image: Image.Image) -> tuple[Image.Image, int]:
    rgba = image.convert("RGBA")
    alpha = rgba.getchannel("A")
    alpha_pixels = alpha.load()
    width, height = rgba.size
    visited = bytearray(width * height)
    components: list[list[tuple[int, int]]] = []

    for y in range(height):
        for x in range(width):
            index = y * width + x
            if visited[index] or alpha_pixels[x, y] == 0:
                continue
            visited[index] = 1
            queue: deque[tuple[int, int]] = deque([(x, y)])
            component: list[tuple[int, int]] = []
            while queue:
                current_x, current_y = queue.popleft()
                component.append((current_x, current_y))
                for neighbor_x, neighbor_y in (
                    (current_x - 1, current_y - 1),
                    (current_x, current_y - 1),
                    (current_x + 1, current_y - 1),
                    (current_x - 1, current_y),
                    (current_x + 1, current_y),
                    (current_x - 1, current_y + 1),
                    (current_x, current_y + 1),
                    (current_x + 1, current_y + 1),
                ):
                    if not (0 <= neighbor_x < width and 0 <= neighbor_y < height):
                        continue
                    neighbor_index = neighbor_y * width + neighbor_x
                    if visited[neighbor_index] or alpha_pixels[neighbor_x, neighbor_y] == 0:
                        continue
                    visited[neighbor_index] = 1
                    queue.append((neighbor_x, neighbor_y))
            components.append(component)

    components.sort(key=len, reverse=True)
    if not components:
        raise ValueError("No visible component")
    keep = set(components[0])
    pixels = rgba.load()
    cleared = 0
    for y in range(height):
        for x in range(width):
            if pixels[x, y][3] != 0 and (x, y) not in keep:
                pixels[x, y] = (0, 0, 0, 0)
                cleared += 1
    return zero_hidden_rgb(rgba), cleared


def extract_baked_checker_background(image: Image.Image) -> tuple[Image.Image, int, int]:
    rgb = image.convert("RGB")
    width, height = rgb.size
    source = rgb.load()
    visited = bytearray(width * height)
    queue: deque[tuple[int, int]] = deque()

    def is_background(x: int, y: int) -> bool:
        red, green, blue = source[x, y]
        return min(red, green, blue) >= 198 and max(red, green, blue) - min(red, green, blue) <= 26

    def enqueue(x: int, y: int) -> None:
        index = y * width + x
        if not visited[index] and is_background(x, y):
            visited[index] = 1
            queue.append((x, y))

    for x in range(width):
        enqueue(x, 0)
        enqueue(x, height - 1)
    for y in range(height):
        enqueue(0, y)
        enqueue(width - 1, y)

    while queue:
        x, y = queue.popleft()
        for neighbor_x, neighbor_y in (
            (x - 1, y),
            (x + 1, y),
            (x, y - 1),
            (x, y + 1),
        ):
            if 0 <= neighbor_x < width and 0 <= neighbor_y < height:
                enqueue(neighbor_x, neighbor_y)

    rgba = Image.new("RGBA", rgb.size, (0, 0, 0, 0))
    destination = rgba.load()
    cleared = 0
    for y in range(height):
        for x in range(width):
            if visited[y * width + x]:
                cleared += 1
            else:
                red, green, blue = source[x, y]
                destination[x, y] = (red, green, blue, 255)

    # Remove only bright, near-neutral baked-checker fringe that directly
    # touches transparency. Dark graphite and colored jade boundary pixels are
    # retained; enclosed silver highlights are never reached by this process.
    halo_cleared = 0
    for _iteration in range(16):
        pixels = rgba.load()
        boundary_halo: list[tuple[int, int]] = []
        for y in range(1, height - 1):
            for x in range(1, width - 1):
                red, green, blue, alpha = pixels[x, y]
                if alpha == 0:
                    continue
                if min(red, green, blue) < 145 or max(red, green, blue) - min(red, green, blue) > 30:
                    continue
                if any(
                    pixels[x + offset_x, y + offset_y][3] == 0
                    for offset_x, offset_y in (
                        (-1, -1),
                        (0, -1),
                        (1, -1),
                        (-1, 0),
                        (1, 0),
                        (-1, 1),
                        (0, 1),
                        (1, 1),
                    )
                ):
                    boundary_halo.append((x, y))
        if not boundary_halo:
            break
        for x, y in boundary_halo:
            pixels[x, y] = (0, 0, 0, 0)
        halo_cleared += len(boundary_halo)

    return zero_hidden_rgb(rgba), cleared, halo_cleared


def normalize_rear(image: Image.Image) -> tuple[Image.Image, dict[str, object]]:
    extracted, background_pixels, halo_pixels = extract_baked_checker_background(image)
    extracted, detached_pixels = keep_largest_visible_component(extracted)
    bbox = extracted.getchannel("A").getbbox()
    if bbox is None:
        raise ValueError("Rear extraction produced no subject")
    subject = extracted.crop(bbox)
    scale = TARGET_REAR_HEIGHT / subject.height
    resized_size = (
        max(1, round(subject.width * scale)),
        TARGET_REAR_HEIGHT,
    )
    subject = subject.resize(resized_size, Image.Resampling.LANCZOS)
    subject = zero_hidden_rgb(subject)
    canvas = Image.new("RGBA", TARGET_SIZE, (0, 0, 0, 0))
    origin = (
        (TARGET_SIZE[0] - subject.width) // 2,
        (TARGET_SIZE[1] - subject.height) // 2,
    )
    canvas.alpha_composite(subject, origin)
    canvas = zero_hidden_rgb(canvas)
    return canvas, {
        "source_size": list(image.size),
        "source_mode": image.mode,
        "background_pixels_cleared": background_pixels,
        "edge_connected_baked_halo_pixels_cleared": halo_pixels,
        "detached_pixels_cleared": detached_pixels,
        "source_foreground_bbox": list(bbox),
        "uniform_scale": scale,
        "normalized_subject_size": list(resized_size),
        "normalized_origin": list(origin),
    }


def save_contact_sheet(paths: dict[str, Path], destination: Path) -> None:
    sheet = Image.new("RGB", (2048, 2048), (28, 30, 32))
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.load_default(size=28)
    cells = {
        "FRONT 0": (0, 0),
        "REAR 180": (1024, 0),
        "LEFT 90": (0, 1024),
        "RIGHT 270": (1024, 1024),
    }
    checker = Image.new("RGB", (1024, 1024), (172, 176, 180))
    checker_draw = ImageDraw.Draw(checker)
    tile = 32
    for y in range(0, 1024, tile):
        for x in range(0, 1024, tile):
            if ((x // tile) + (y // tile)) % 2:
                checker_draw.rectangle((x, y, x + tile - 1, y + tile - 1), fill=(126, 132, 138))

    for (label, origin), view in zip(cells.items(), ("front", "back", "left", "right")):
        panel = checker.copy().convert("RGBA")
        image = Image.open(paths[view]).convert("RGBA")
        preview = image.copy()
        preview.thumbnail((960, 900), Image.Resampling.LANCZOS)
        panel.alpha_composite(
            preview,
            ((1024 - preview.width) // 2, (1024 - preview.height) // 2 + 24),
        )
        panel_draw = ImageDraw.Draw(panel)
        panel_draw.rectangle((0, 0, 1023, 56), fill=(18, 20, 22, 240))
        panel_draw.text((24, 14), label, fill=(255, 255, 255, 255), font=font)
        sheet.paste(panel.convert("RGB"), origin)
    destination.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(destination, format="PNG", optimize=True)


def inspect(path: Path) -> dict[str, object]:
    image = Image.open(path).convert("RGBA")
    alpha = image.getchannel("A")
    hidden_rgb_nonzero = sum(
        1
        for red, green, blue, pixel_alpha in image.get_flattened_data()
        if pixel_alpha == 0 and (red != 0 or green != 0 or blue != 0)
    )
    return {
        "size": list(image.size),
        "mode": image.mode,
        "alpha_extrema": list(alpha.getextrema()),
        "alpha_bbox": list(alpha.getbbox() or ()),
        "corner_alpha": [
            alpha.getpixel((0, 0)),
            alpha.getpixel((image.width - 1, 0)),
            alpha.getpixel((0, image.height - 1)),
            alpha.getpixel((image.width - 1, image.height - 1)),
        ],
        "hidden_rgb_nonzero_when_alpha_zero": hidden_rgb_nonzero,
        "sha256": sha256(path),
    }


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)

    front = zero_hidden_rgb(Image.open(ACCEPTED / "front.png"))
    left, left_detached_cleared = keep_largest_visible_component(
        zero_hidden_rgb(Image.open(ACCEPTED / "left.png"))
    )
    right = left.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    rear, rear_processing = normalize_rear(Image.open(GENERATED_REAR))

    images = {"front": front, "back": rear, "left": left, "right": right}
    paths = {view: OUTPUT / f"{view}.png" for view in images}
    for view, image in images.items():
        if image.size != TARGET_SIZE:
            raise ValueError(f"{view} unexpected size {image.size}")
        zero_hidden_rgb(image).save(paths[view], format="PNG", optimize=True)

    save_contact_sheet(paths, OUTPUT / "QA" / "contact_sheet.png")

    validation = {
        "left_detached_alpha_pixels_cleared": left_detached_cleared,
        "rear_processing": rear_processing,
        "right_exact_left_rgba_mirror": right.tobytes()
        == left.transpose(Image.Transpose.FLIP_LEFT_RIGHT).tobytes(),
        "files": {view: inspect(path) for view, path in paths.items()},
    }
    print(json.dumps(validation, indent=2))


if __name__ == "__main__":
    main()
