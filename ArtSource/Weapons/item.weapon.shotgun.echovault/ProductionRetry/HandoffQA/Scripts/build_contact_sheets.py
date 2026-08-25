from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


HANDOFF = Path(__file__).resolve().parents[1]
PRODUCTION = HANDOFF.parent
ITEM_ROOT = PRODUCTION.parent
RENDERS = HANDOFF / "Renders"
WEB = ITEM_ROOT / "References" / "WebPreferred"
FONT = ImageFont.load_default(size=24)


def panel(image_path: Path, size: tuple[int, int], label: str) -> Image.Image:
    width, height = size
    result = Image.new("RGB", size, (10, 12, 14))
    image = Image.open(image_path).convert("RGBA")
    image.thumbnail((width - 24, height - 64), Image.Resampling.LANCZOS)
    result.paste(
        image.convert("RGB"),
        ((width - image.width) // 2, 52 + (height - 64 - image.height) // 2),
    )
    draw = ImageDraw.Draw(result)
    draw.rectangle((0, 0, width - 1, 44), fill=(24, 28, 32))
    draw.text((16, 10), label, font=FONT, fill=(242, 245, 248))
    return result


def main() -> None:
    views = ("muzzle", "stock", "left", "right", "top")
    five_view = Image.new("RGB", (2500, 900), (6, 8, 10))
    for column, view in enumerate(views):
        five_view.paste(
            panel(RENDERS / f"pbr_{view}.png", (500, 450), f"PBR / {view.upper()}"),
            (column * 500, 0),
        )
        five_view.paste(
            panel(
                RENDERS / f"emission_{view}.png",
                (500, 450),
                f"EMISSION-ONLY / {view.upper()}",
            ),
            (column * 500, 450),
        )
    five_view.save(
        HANDOFF / "contact_sheet_pbr_emission_5view.png", format="PNG", optimize=True
    )

    mappings = (
        ("front", "muzzle", "FRONT / MUZZLE"),
        ("back", "stock", "REAR / STOCK"),
        ("left", "left", "LEFT"),
        ("right", "right", "RIGHT"),
    )
    comparison = Image.new("RGB", (2400, 1200), (6, 8, 10))
    for column, (web_view, render_view, label) in enumerate(mappings):
        comparison.paste(
            panel(WEB / f"{web_view}.png", (600, 600), f"WEBPREFERRED / {label}"),
            (column * 600, 0),
        )
        comparison.paste(
            panel(
                RENDERS / f"pbr_{render_view}.png",
                (600, 600),
                f"PRODUCTIONRETRY / {label}",
            ),
            (column * 600, 600),
        )
    comparison.save(
        HANDOFF / "contact_sheet_webpreferred_comparison.png",
        format="PNG",
        optimize=True,
    )


if __name__ == "__main__":
    main()
