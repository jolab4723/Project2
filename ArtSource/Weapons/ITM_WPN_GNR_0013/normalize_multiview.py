import json
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parent
TRANSPARENT = ROOT / "Prepared" / "Transparent"
RABBIT_ORIENTATION = ROOT / "Prepared" / "RabbitOrientation"
TRIPO_INPUT = ROOT / "Prepared" / "TripoInput"
CANVAS_SIZE = 2048
TARGET_SIDE_LENGTH = 1800


def alpha_bbox(image: Image.Image) -> tuple[int, int, int, int]:
    bbox = image.getchannel("A").getbbox()
    if bbox is None:
        raise ValueError("Reference contains no visible pixels")
    return bbox


def main() -> None:
    TRIPO_INPUT.mkdir(parents=True, exist_ok=True)
    images = {}
    source_bboxes = {}
    for view_name in ("left", "front", "back", "right"):
        source_path = (
            RABBIT_ORIENTATION / f"{view_name}.png"
            if view_name in {"left", "right"}
            else TRANSPARENT / f"{view_name}.png"
        )
        image = Image.open(source_path).convert("RGBA")
        images[view_name] = image
        source_bboxes[view_name] = alpha_bbox(image)

    side_widths = [
        source_bboxes[name][2] - source_bboxes[name][0]
        for name in ("left", "right")
    ]
    side_heights = [
        source_bboxes[name][3] - source_bboxes[name][1]
        for name in ("left", "right")
    ]
    side_scale = TARGET_SIDE_LENGTH / (sum(side_widths) / len(side_widths))
    target_vertical_extent = (sum(side_heights) / len(side_heights)) * side_scale

    report = {
        "canvas_size": CANVAS_SIZE,
        "target_side_length": TARGET_SIDE_LENGTH,
        "target_vertical_extent": target_vertical_extent,
        "views": {},
    }

    for view_name in ("front", "left", "back", "right"):
        image = images[view_name]
        bbox = source_bboxes[view_name]
        cropped = image.crop(bbox)
        if view_name in ("left", "right"):
            scale = side_scale
        else:
            scale = target_vertical_extent / cropped.height

        width = max(1, round(cropped.width * scale))
        height = max(1, round(cropped.height * scale))
        resized = cropped.resize((width, height), Image.Resampling.LANCZOS)

        if width > CANVAS_SIZE or height > CANVAS_SIZE:
            raise ValueError(f"{view_name} does not fit {CANVAS_SIZE}px canvas")

        canvas = Image.new("RGBA", (CANVAS_SIZE, CANVAS_SIZE), (0, 0, 0, 0))
        x = (CANVAS_SIZE - width) // 2
        y = (CANVAS_SIZE - height) // 2
        canvas.alpha_composite(resized, (x, y))
        output_path = TRIPO_INPUT / f"{view_name}.png"
        canvas.save(output_path)

        output_bbox = alpha_bbox(canvas)
        report["views"][view_name] = {
            "source_bbox": list(bbox),
            "scale": scale,
            "output_bbox": list(output_bbox),
            "output_size": [width, height],
            "margins": [
                output_bbox[0],
                output_bbox[1],
                CANVAS_SIZE - output_bbox[2],
                CANVAS_SIZE - output_bbox[3],
            ],
        }

    report_path = ROOT / "Prepared" / "multiview_validation.json"
    report_path.write_text(
        json.dumps(report, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    print(report_path)
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
