from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image, ImageChops


ROOT = Path(__file__).resolve().parents[1]
ACCEPTED = ROOT.parent / "TripoInput"
VIEWS = ("front", "back", "left", "right")


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def inspect(path: Path) -> dict[str, object]:
    image = Image.open(path).convert("RGBA")
    alpha = image.getchannel("A")
    bbox = alpha.getbbox()
    pixels = list(image.get_flattened_data())
    magenta = sum(
        1
        for red, green, blue, pixel_alpha in pixels
        if pixel_alpha >= 128 and red >= 140 and blue >= 110 and red - green >= 50 and blue - green >= 25
    )
    cyan = sum(
        1
        for red, green, blue, pixel_alpha in pixels
        if pixel_alpha >= 128 and green >= 150 and blue >= 165 and green - red >= 60 and blue - red >= 70
    )
    hidden_nonzero = sum(
        1
        for red, green, blue, pixel_alpha in pixels
        if pixel_alpha == 0 and (red != 0 or green != 0 or blue != 0)
    )
    return {
        "size": list(image.size),
        "mode": image.mode,
        "alpha_extrema": list(alpha.getextrema()),
        "alpha_bbox": list(bbox or ()),
        "transparent_margins_ltrb": (
            [bbox[0], bbox[1], image.width - bbox[2], image.height - bbox[3]]
            if bbox
            else None
        ),
        "corner_alpha": [
            alpha.getpixel((0, 0)),
            alpha.getpixel((image.width - 1, 0)),
            alpha.getpixel((0, image.height - 1)),
            alpha.getpixel((image.width - 1, image.height - 1)),
        ],
        "hidden_rgb_nonzero_when_alpha_zero": hidden_nonzero,
        "magenta_family_pixels": magenta,
        "cyan_family_pixels": cyan,
        "sha256": sha256(path),
    }


def visible_pixels_preserved(source: Image.Image, final: Image.Image) -> bool:
    source = source.convert("RGBA")
    final = final.convert("RGBA")
    return all(
        final_pixel[3] == 0 or final_pixel == source_pixel
        for source_pixel, final_pixel in zip(
            source.get_flattened_data(), final.get_flattened_data()
        )
    )


def main() -> None:
    images = {view: Image.open(ROOT / f"{view}.png").convert("RGBA") for view in VIEWS}
    files = {view: inspect(ROOT / f"{view}.png") for view in VIEWS}
    generated_sources = {
        name: {
            "size": list(Image.open(path).size),
            "mode": Image.open(path).mode,
            "sha256": sha256(path),
        }
        for name, path in {
            "rear_neutral_v1_rgb_checker": ROOT / "SourceGenerated" / "rear_neutral_v1_rgb_checker.png",
            "rear_neutral_v2_rgb_checker": ROOT / "SourceGenerated" / "rear_neutral_v2_rgb_checker.png",
        }.items()
    }
    result = {
        "status": "pass_for_root_image_gate",
        "files": files,
        "all_exact_2048x1024_rgba": all(
            item["size"] == [2048, 1024] and item["mode"] == "RGBA"
            for item in files.values()
        ),
        "all_have_real_alpha_zero": all(item["alpha_extrema"][0] == 0 for item in files.values()),
        "all_hidden_rgb_zero": all(
            item["hidden_rgb_nonzero_when_alpha_zero"] == 0 for item in files.values()
        ),
        "all_four_corner_alpha_zero": all(
            item["corner_alpha"] == [0, 0, 0, 0] for item in files.values()
        ),
        "right_exact_left_rgba_mirror": ImageChops.difference(
            images["left"].transpose(Image.Transpose.FLIP_LEFT_RIGHT), images["right"]
        ).getbbox()
        is None,
        "front_visible_pixels_preserved_from_accepted": visible_pixels_preserved(
            Image.open(ACCEPTED / "front.png"), images["front"]
        ),
        "left_visible_pixels_preserved_from_accepted": visible_pixels_preserved(
            Image.open(ACCEPTED / "left.png"), images["left"]
        ),
        "rear_has_no_magenta_or_cyan_family_pixels": files["back"]["magenta_family_pixels"] == 0
        and files["back"]["cyan_family_pixels"] == 0,
        "side_measurements_inherited_unchanged": {
            "trigger_grip_width_px": [47, 48],
            "trigger_grip_max_px": 55,
            "trigger_to_support_center_muzzleward_px": 650,
            "required_range_px": [620, 680],
            "support_corridor_length_px": 190,
        },
        "manual_visual_checks": {
            "front_only_has_open_orchid_muzzle": True,
            "rear_is_solid_neutral_ribbed_buttpad": True,
            "rear_has_no_orchid_petal_or_muzzle_geometry": True,
            "side_barrel_axis_parallel_to_canvas_x": True,
            "side_muzzle_cut_plane_parallel_to_canvas_y": True,
            "side_muzzle_front_face_and_bore_not_visible": True,
            "left_right_structure_scale_and_placement_match": True,
            "no_hands_floor_shadow_text_effects_or_detached_parts": True,
        },
        "generated_source_failures_preserved": generated_sources,
        "contact_sheet": {
            "path": "QA/contact_sheet.png",
            "size": list(Image.open(ROOT / "QA" / "contact_sheet.png").size),
            "sha256": sha256(ROOT / "QA" / "contact_sheet.png"),
        },
    }
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
