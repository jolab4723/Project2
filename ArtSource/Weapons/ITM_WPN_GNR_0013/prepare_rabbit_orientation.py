import json
from collections import deque
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parent
SOURCE_DIR = ROOT / "Prepared" / "Transparent"
OUTPUT_DIR = ROOT / "Prepared" / "RabbitOrientation"
PROFILE_PATH = ROOT / "References" / "rabbit_side_left.png"
TARGET_HEIGHT = 238


def largest_alpha_component(image: Image.Image, threshold: int = 96) -> Image.Image:
    alpha = image.getchannel("A")
    width, height = alpha.size
    alpha_bytes = alpha.tobytes()
    foreground = bytearray(value >= threshold for value in alpha_bytes)
    visited = bytearray(width * height)
    largest: list[int] = []

    for start in range(width * height):
        if not foreground[start] or visited[start]:
            continue
        component: list[int] = []
        queue = deque([start])
        visited[start] = 1
        while queue:
            index = queue.popleft()
            component.append(index)
            x = index % width
            y = index // width
            if x > 0:
                neighbor = index - 1
                if foreground[neighbor] and not visited[neighbor]:
                    visited[neighbor] = 1
                    queue.append(neighbor)
            if x + 1 < width:
                neighbor = index + 1
                if foreground[neighbor] and not visited[neighbor]:
                    visited[neighbor] = 1
                    queue.append(neighbor)
            if y > 0:
                neighbor = index - width
                if foreground[neighbor] and not visited[neighbor]:
                    visited[neighbor] = 1
                    queue.append(neighbor)
            if y + 1 < height:
                neighbor = index + width
                if foreground[neighbor] and not visited[neighbor]:
                    visited[neighbor] = 1
                    queue.append(neighbor)
        if len(component) > len(largest):
            largest = component

    if not largest:
        raise ValueError("No rabbit foreground found")

    keep = bytearray(width * height)
    for index in largest:
        keep[index] = alpha_bytes[index]
    cleaned = image.copy()
    cleaned.putalpha(Image.frombytes("L", (width, height), bytes(keep)))
    bbox = cleaned.getchannel("A").getbbox()
    if bbox is None:
        raise ValueError("Cleaned rabbit has no visible bounds")
    return cleaned.crop(bbox)


def bunny_erase_mask(size: tuple[int, int], shift_x: int) -> Image.Image:
    mask = Image.new("L", size, 0)
    draw = ImageDraw.Draw(mask)
    draw.rounded_rectangle(
        (378 + shift_x, 276, 596 + shift_x, 550),
        radius=42,
        fill=255,
    )
    ellipses = (
        (420, 277, 474, 369),
        (493, 277, 550, 369),
        (386, 322, 589, 481),
        (408, 424, 570, 540),
        (390, 438, 451, 505),
        (531, 438, 591, 505),
        (405, 492, 481, 548),
        (497, 492, 573, 548),
    )
    for left, top, right, bottom in ellipses:
        draw.ellipse((left + shift_x, top, right + shift_x, bottom), fill=255)
    return mask.filter(ImageFilter.GaussianBlur(8))


def glass_fill(image: Image.Image, shift_x: int) -> Image.Image:
    result = image.copy()
    pixels = result.load()
    source = image.load()
    left = 372 + shift_x
    right = 602 + shift_x
    top = 276
    bottom = 550

    for y in range(top, bottom + 1):
        left_samples = [
            source[x, y]
            for x in range(left - 24, left - 8)
            if source[x, y][3] >= 224 and sum(source[x, y][:3]) >= 180
        ]
        right_samples = [
            source[x, y]
            for x in range(right + 8, right + 24)
            if source[x, y][3] >= 224 and sum(source[x, y][:3]) >= 180
        ]
        if not left_samples:
            left_samples = [(178, 211, 229, 255)]
        if not right_samples:
            right_samples = left_samples
        left_color = tuple(sum(pixel[channel] for pixel in left_samples) // len(left_samples) for channel in range(4))
        right_color = tuple(sum(pixel[channel] for pixel in right_samples) // len(right_samples) for channel in range(4))
        for x in range(left, right + 1):
            t = (x - left) / (right - left)
            rgb = tuple(round(left_color[channel] * (1.0 - t) + right_color[channel] * t) for channel in range(3))
            pixels[x, y] = (*rgb, source[x, y][3])
    return result.filter(ImageFilter.GaussianBlur(2.0))


def place_profile(image: Image.Image, profile: Image.Image, center: tuple[int, int], mirror: bool) -> Image.Image:
    oriented = profile.transpose(Image.Transpose.FLIP_LEFT_RIGHT) if mirror else profile
    target_width = round(oriented.width * TARGET_HEIGHT / oriented.height)
    oriented = oriented.resize((target_width, TARGET_HEIGHT), Image.Resampling.LANCZOS)

    # The chamber glass cools and slightly softens the mascot color.
    red, green, blue, alpha = oriented.split()
    red = red.point(lambda value: round(value * 0.94))
    green = green.point(lambda value: round(value * 0.98))
    alpha = alpha.point(lambda value: round(value * 0.92))
    oriented = Image.merge("RGBA", (red, green, blue, alpha))

    x = center[0] - oriented.width // 2
    y = center[1] - oriented.height // 2
    result = image.copy()
    result.alpha_composite(oriented, (x, y))
    return result


def prepare_view(view_name: str, shift_x: int, center: tuple[int, int], mirror: bool, profile: Image.Image) -> dict:
    source_path = SOURCE_DIR / f"{view_name}.png"
    source = Image.open(source_path).convert("RGBA")
    mask = bunny_erase_mask(source.size, shift_x)
    cleared = Image.composite(glass_fill(source, shift_x), source, mask)
    result = place_profile(cleared, profile, center, mirror)
    output_path = OUTPUT_DIR / f"{view_name}.png"
    result.save(output_path)
    return {
        "source": str(source_path.relative_to(ROOT)),
        "output": str(output_path.relative_to(ROOT)),
        "size": list(result.size),
        "rabbit_forward": "+Z",
        "view": view_name,
        "rabbit_profile_direction": "left" if not mirror else "right",
    }


def main() -> None:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    profile = largest_alpha_component(Image.open(PROFILE_PATH).convert("RGBA"))
    profile_clean_path = OUTPUT_DIR / "rabbit_side_left_clean.png"
    profile.save(profile_clean_path)

    records = [
        prepare_view("left", 0, (488, 413), False, profile),
        prepare_view("right", 622, (1110, 413), True, profile),
    ]
    manifest = {
        "orientation_convention": {
            "weapon_forward": "+Z (muzzle)",
            "weapon_up": "+Y",
            "rabbit_forward": "+Z (face toward muzzle)",
            "front_view": "rabbit face",
            "left_view": "rabbit left-facing profile",
            "right_view": "rabbit right-facing profile",
            "back_view": "rabbit back of head",
        },
        "records": records,
    }
    (OUTPUT_DIR / "orientation_manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8"
    )


if __name__ == "__main__":
    main()
