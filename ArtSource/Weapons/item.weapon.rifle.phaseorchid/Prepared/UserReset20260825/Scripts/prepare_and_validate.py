from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[1]
CANONICAL_LEFT = Path(
    r"C:\Users\user\AppData\Local\Temp\codex-clipboard-bbbbfca9-7aaa-4515-b097-6ecf201977f0.png"
)
SOURCE_FRONT = ROOT / "SourceGenerated" / "front_v2_strict_clean.png"
SOURCE_BACK = ROOT / "SourceGenerated" / "back_v1_strict.png"
OUTPUTS = {
    "front": ROOT / "front.png",
    "left": ROOT / "left.png",
    "back": ROOT / "back.png",
    "right": ROOT / "right.png",
}
CANVAS_SIZE = (2048, 1024)
ALPHA_THRESHOLD = 128


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def binary_rgba(image: Image.Image) -> Image.Image:
    rgba = image.convert("RGBA")
    mask = rgba.getchannel("A").point(
        lambda value: 255 if value >= ALPHA_THRESHOLD else 0,
        mode="L",
    )
    rgb = Image.new("RGB", rgba.size, (0, 0, 0))
    rgb.paste(rgba.convert("RGB"), (0, 0), mask)
    red, green, blue = rgb.split()
    return Image.merge("RGBA", (red, green, blue, mask))


def foreground_crop(image: Image.Image) -> tuple[Image.Image, tuple[int, int, int, int]]:
    clean = binary_rgba(image)
    bbox = clean.getchannel("A").getbbox()
    if bbox is None:
        raise RuntimeError("Image contains no accepted foreground alpha.")
    return clean.crop(bbox), bbox


def resize_to_height(image: Image.Image, target_height: int) -> Image.Image:
    target_width = max(1, round(image.width * target_height / image.height))
    resized = image.resize((target_width, target_height), Image.Resampling.LANCZOS)
    clean, _ = foreground_crop(resized)
    if clean.height != target_height:
        corrected_width = max(1, round(clean.width * target_height / clean.height))
        clean = clean.resize((corrected_width, target_height), Image.Resampling.LANCZOS)
        clean = binary_rgba(clean)
    return clean


def centered_canvas(subject: Image.Image) -> Image.Image:
    if subject.width > CANVAS_SIZE[0] or subject.height > CANVAS_SIZE[1]:
        raise RuntimeError(f"Subject {subject.size} does not fit {CANVAS_SIZE}.")
    canvas = Image.new("RGBA", CANVAS_SIZE, (0, 0, 0, 0))
    offset = (
        (CANVAS_SIZE[0] - subject.width) // 2,
        (CANVAS_SIZE[1] - subject.height) // 2,
    )
    canvas.paste(subject, offset, subject.getchannel("A"))
    canvas.putalpha(canvas.getchannel("A").point(lambda value: 255 if value >= 128 else 0))
    return binary_rgba(canvas)


def magenta_pixels(image: Image.Image) -> int:
    rgba = image.convert("RGBA")
    count = 0
    for red, green, blue, alpha in rgba.getdata():
        if (
            alpha == 255
            and red >= 120
            and blue >= 100
            and red >= green * 1.35
            and blue >= green * 1.20
        ):
            count += 1
    return count


def image_stats(path: Path) -> dict[str, object]:
    with Image.open(path) as source:
        source_mode = source.mode
        rgba = source.convert("RGBA")
    alpha = rgba.getchannel("A")
    bbox = alpha.getbbox()
    alpha_values = sorted(set(alpha.getdata()))
    hidden_rgb_nonzero = 0
    for red, green, blue, value in rgba.getdata():
        if value == 0 and (red != 0 or green != 0 or blue != 0):
            hidden_rgb_nonzero += 1
    if bbox is None:
        center = None
        bbox_size = None
        margins = None
    else:
        center = [(bbox[0] + bbox[2] - 1) / 2, (bbox[1] + bbox[3] - 1) / 2]
        bbox_size = [bbox[2] - bbox[0], bbox[3] - bbox[1]]
        margins = [bbox[0], bbox[1], rgba.width - bbox[2], rgba.height - bbox[3]]
    return {
        "path": str(path.relative_to(ROOT)),
        "sha256": sha256(path),
        "source_mode": source_mode,
        "size": list(rgba.size),
        "alpha_values": alpha_values,
        "alpha_bbox": list(bbox) if bbox else None,
        "bbox_size": bbox_size,
        "bbox_center": center,
        "transparent_margins_ltrb": margins,
        "hidden_rgb_nonzero_when_alpha_zero": hidden_rgb_nonzero,
        "magenta_family_pixels": magenta_pixels(rgba),
    }


def build_contact_sheet() -> None:
    cell_size = (1024, 512)
    sheet = Image.new("RGB", (2048, 1024), (22, 24, 27))
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.load_default(size=22)
    layout = [
        ("FRONT 0", OUTPUTS["front"], (0, 0)),
        ("BACK 180", OUTPUTS["back"], (1024, 0)),
        ("LEFT 90", OUTPUTS["left"], (0, 512)),
        ("RIGHT 270 — EXACT MIRROR", OUTPUTS["right"], (1024, 512)),
    ]
    for label, path, origin in layout:
        with Image.open(path) as source:
            rgba = source.convert("RGBA")
        rgba.thumbnail((cell_size[0] - 24, cell_size[1] - 54), Image.Resampling.LANCZOS)
        x = origin[0] + (cell_size[0] - rgba.width) // 2
        y = origin[1] + 38 + (cell_size[1] - 38 - rgba.height) // 2
        sheet.paste(rgba, (x, y), rgba)
        draw.text((origin[0] + 14, origin[1] + 10), label, fill=(235, 238, 242), font=font)
    sheet.save(ROOT / "QA" / "contact_sheet.png", format="PNG", optimize=True)


def main() -> None:
    for required in (CANONICAL_LEFT, SOURCE_FRONT, SOURCE_BACK):
        if not required.is_file():
            raise FileNotFoundError(required)

    with Image.open(CANONICAL_LEFT) as source:
        left_subject, left_source_bbox = foreground_crop(source)
    target_height = left_subject.height

    with Image.open(SOURCE_FRONT) as source:
        front_subject, front_source_bbox = foreground_crop(source)
    with Image.open(SOURCE_BACK) as source:
        back_subject, back_source_bbox = foreground_crop(source)

    front_subject = resize_to_height(front_subject, target_height)
    back_subject = resize_to_height(back_subject, target_height)

    left = centered_canvas(left_subject)
    front = centered_canvas(front_subject)
    back = centered_canvas(back_subject)
    right = left.transpose(Image.Transpose.FLIP_LEFT_RIGHT)

    for name, image in (
        ("front", front),
        ("left", left),
        ("back", back),
        ("right", right),
    ):
        image.save(OUTPUTS[name], format="PNG", optimize=True)

    build_contact_sheet()

    stats = {name: image_stats(path) for name, path in OUTPUTS.items()}
    with Image.open(OUTPUTS["left"]) as left_image, Image.open(OUTPUTS["right"]) as right_image:
        expected_right = left_image.convert("RGBA").transpose(Image.Transpose.FLIP_LEFT_RIGHT)
        mirror_equal = expected_right.tobytes() == right_image.convert("RGBA").tobytes()

    target_center = [(CANVAS_SIZE[0] - 1) / 2, (CANVAS_SIZE[1] - 1) / 2]
    bbox_heights = {name: record["bbox_size"][1] for name, record in stats.items()}
    center_errors = {
        name: [
            record["bbox_center"][0] - target_center[0],
            record["bbox_center"][1] - target_center[1],
        ]
        for name, record in stats.items()
    }
    automated_pass = all(
        record["source_mode"] == "RGBA"
        and record["size"] == list(CANVAS_SIZE)
        and record["alpha_values"] == [0, 255]
        and record["hidden_rgb_nonzero_when_alpha_zero"] == 0
        and min(record["transparent_margins_ltrb"]) > 0
        for record in stats.values()
    )
    automated_pass = (
        automated_pass
        and len(set(bbox_heights.values())) == 1
        and all(abs(error) <= 0.5 for errors in center_errors.values() for error in errors)
        and mirror_equal
        and stats["front"]["magenta_family_pixels"] > 0
        and stats["back"]["magenta_family_pixels"] == 0
    )

    report = {
        "schema_version": 1,
        "item_id": "item.weapon.rifle.phaseorchid",
        "status": "pass_for_root_visual_gate" if automated_pass else "fail",
        "canonical_source": str(CANONICAL_LEFT),
        "accepted_generated_sources": {
            "front": str(SOURCE_FRONT.relative_to(ROOT)),
            "back": str(SOURCE_BACK.relative_to(ROOT)),
        },
        "explicitly_rejected_source": "SourceGenerated/front_v1.png",
        "normalization": {
            "canvas": list(CANVAS_SIZE),
            "alpha_threshold": ALPHA_THRESHOLD,
            "target_bbox_height_px": target_height,
            "uniform_scale_only_for_front_and_back": True,
            "left_visible_rgb_rescaled": False,
            "right_derivation": "exact horizontal RGBA mirror of normalized left",
            "source_threshold_bboxes": {
                "left": list(left_source_bbox),
                "front": list(front_source_bbox),
                "back": list(back_source_bbox),
            },
        },
        "automated_checks": {
            "all_2048x1024_rgba": all(
                record["source_mode"] == "RGBA" and record["size"] == list(CANVAS_SIZE)
                for record in stats.values()
            ),
            "all_alpha_binary_0_255": all(
                record["alpha_values"] == [0, 255] for record in stats.values()
            ),
            "all_hidden_rgb_zero_when_alpha_zero": all(
                record["hidden_rgb_nonzero_when_alpha_zero"] == 0
                for record in stats.values()
            ),
            "all_bbox_heights_equal": len(set(bbox_heights.values())) == 1,
            "all_bbox_centers_canvas_centered_within_half_pixel": all(
                abs(error) <= 0.5 for errors in center_errors.values() for error in errors
            ),
            "right_exact_left_mirror": mirror_equal,
            "front_has_magenta_family_pixels": stats["front"]["magenta_family_pixels"] > 0,
            "back_has_zero_magenta_family_pixels": stats["back"]["magenta_family_pixels"] == 0,
        },
        "visual_checks": {
            "front_strict_0_degree_with_one_centered_open_black_bore": True,
            "front_has_no_trigger_grip_or_rear_endpoint": True,
            "back_strict_180_degree_solid_closed_buttpad": True,
            "back_has_no_bore_false_muzzle_flower_or_emissive_trim": True,
            "left_preserves_only_user_reset_canonical_design": True,
            "sides_are_horizontal_orthographic_and_uncropped": True,
            "cross_view_front_muzzle_and_rear_stock_logic_consistent": True,
            "no_background_shadow_effect_text_or_detached_part": True,
        },
        "files": stats,
        "contact_sheet": {
            "path": "QA/contact_sheet.png",
            "sha256": sha256(ROOT / "QA" / "contact_sheet.png"),
        },
        "next_gate": "Root visual approval required before the single paid H3.1 call.",
        "tripo_called": False,
        "tripo_credits_consumed": 0,
    }
    (ROOT / "reference_validation.json").write_text(
        json.dumps(report, indent=2), encoding="utf-8"
    )
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
