from __future__ import annotations

import sys
from collections import deque
from pathlib import Path

from PIL import Image


TARGET_SIZE = (2048, 1024)


def remove_connected_light_neutral_background(image: Image.Image) -> Image.Image:
    rgb = image.convert("RGB")
    width, height = rgb.size
    pixels = rgb.load()

    def is_background(x: int, y: int) -> bool:
        red, green, blue = pixels[x, y]
        return min(red, green, blue) >= 220 and max(red, green, blue) - min(red, green, blue) <= 14

    visited = bytearray(width * height)
    queue: deque[tuple[int, int]] = deque()

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
        if x > 0:
            enqueue(x - 1, y)
        if x + 1 < width:
            enqueue(x + 1, y)
        if y > 0:
            enqueue(x, y - 1)
        if y + 1 < height:
            enqueue(x, y + 1)

    rgba = rgb.convert("RGBA")
    alpha = Image.new("L", (width, height), 255)
    alpha.putdata([0 if value else 255 for value in visited])
    rgba.putalpha(alpha)
    return rgba


def clear_enclosed_background_components(
    image: Image.Image,
    minimum_area: int = 1000,
) -> Image.Image:
    """Clear large enclosed checker/white background islands without touching highlights."""
    rgba = image.copy()
    width, height = rgba.size
    pixels = rgba.load()
    visited = bytearray(width * height)

    def is_background_sample(x: int, y: int) -> bool:
        red, green, blue, alpha = pixels[x, y]
        return (
            alpha > 200
            and min(red, green, blue) >= 215
            and max(red, green, blue) - min(red, green, blue) <= 18
        )

    for y in range(height):
        for x in range(width):
            index = y * width + x
            if visited[index] or not is_background_sample(x, y):
                continue

            visited[index] = 1
            queue: deque[tuple[int, int]] = deque([(x, y)])
            component: list[tuple[int, int]] = []
            while queue:
                current_x, current_y = queue.popleft()
                component.append((current_x, current_y))
                for neighbor_x, neighbor_y in (
                    (current_x - 1, current_y),
                    (current_x + 1, current_y),
                    (current_x, current_y - 1),
                    (current_x, current_y + 1),
                ):
                    if not (0 <= neighbor_x < width and 0 <= neighbor_y < height):
                        continue
                    neighbor_index = neighbor_y * width + neighbor_x
                    if not visited[neighbor_index] and is_background_sample(neighbor_x, neighbor_y):
                        visited[neighbor_index] = 1
                        queue.append((neighbor_x, neighbor_y))

            if len(component) >= minimum_area:
                for component_x, component_y in component:
                    red, green, blue, _ = pixels[component_x, component_y]
                    pixels[component_x, component_y] = (red, green, blue, 0)

                # Remove only the near-white anti-alias fringe connected to this
                # recovered hole. Dark/colored metal and silver highlights below
                # the strict near-white cutoff remain opaque.
                halo_queue: deque[tuple[int, int]] = deque(component)
                halo_seen = set(component)
                while halo_queue:
                    current_x, current_y = halo_queue.popleft()
                    for neighbor_x, neighbor_y in (
                        (current_x - 1, current_y),
                        (current_x + 1, current_y),
                        (current_x, current_y - 1),
                        (current_x, current_y + 1),
                    ):
                        if not (0 <= neighbor_x < width and 0 <= neighbor_y < height):
                            continue
                        if (neighbor_x, neighbor_y) in halo_seen:
                            continue
                        halo_seen.add((neighbor_x, neighbor_y))
                        red, green, blue, alpha = pixels[neighbor_x, neighbor_y]
                        if (
                            alpha > 0
                            and min(red, green, blue) >= 235
                            and max(red, green, blue) - min(red, green, blue) <= 12
                        ):
                            pixels[neighbor_x, neighbor_y] = (red, green, blue, 0)
                            halo_queue.append((neighbor_x, neighbor_y))

    return rgba


def build(source: Path, destination: Path) -> None:
    image = Image.open(source)
    if image.mode == "RGBA" and image.getchannel("A").getextrema()[0] == 0:
        rgba = image.copy()
    else:
        rgba = remove_connected_light_neutral_background(image)

    if rgba.size[0] * TARGET_SIZE[1] != rgba.size[1] * TARGET_SIZE[0]:
        raise ValueError(f"Non-2:1 source would require stretching or cropping: {source} {rgba.size}")

    final = rgba.resize(TARGET_SIZE, Image.Resampling.LANCZOS)
    final = clear_enclosed_background_components(final)
    destination.parent.mkdir(parents=True, exist_ok=True)
    final.save(destination, format="PNG", optimize=True)


if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit("usage: build_reference_final.py SOURCE DESTINATION")
    build(Path(sys.argv[1]), Path(sys.argv[2]))
