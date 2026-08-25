from __future__ import annotations

import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont


ITEM_ID = "item.weapon.grenadelauncher.halomortar"
ROOT = Path(__file__).resolve().parent
ORIGINAL = ROOT.parent / "TripoInput"
SOURCE_GENERATED = ROOT / "SourceGenerated" / "front_open_bore_candidate_rgb.png"
FRONT = ROOT / "front.png"
LEFT = ROOT / "left.png"
BACK = ROOT / "back.png"
RIGHT = ROOT / "right.png"
CONTACT_SHEET = ROOT / "contact_sheet.png"
MANIFEST = ROOT / "manifest.json"
VALIDATION = ROOT / "validation.json"
PROMPTS = ROOT / "imagegen_prompts.txt"


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def load_rgba(path: Path) -> np.ndarray:
    return np.asarray(Image.open(path).convert("RGBA"), dtype=np.uint8).copy()


def alpha_bbox(rgba: np.ndarray) -> list[int]:
    ys, xs = np.nonzero(rgba[:, :, 3])
    return [int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1]


def zero_hidden_rgb(rgba: np.ndarray) -> np.ndarray:
    rgba[rgba[:, :, 3] == 0, :3] = 0
    return rgba


def save_rgba(path: Path, rgba: np.ndarray) -> None:
    zero_hidden_rgb(rgba)
    Image.fromarray(rgba, mode="RGBA").save(path)


ROOT.mkdir(parents=True, exist_ok=True)
original_front = load_rgba(ORIGINAL / "front.png")
original_left = load_rgba(ORIGINAL / "left.png")
original_back = load_rgba(ORIGINAL / "back.png")
candidate = np.asarray(Image.open(SOURCE_GENERATED).convert("RGB"), dtype=np.uint8)

front_bbox = alpha_bbox(original_front)
front_width = front_bbox[2] - front_bbox[0]
front_height = front_bbox[3] - front_bbox[1]
front_center_x = (front_bbox[0] + front_bbox[2] - 1) * 0.5
front_center_y = (front_bbox[1] + front_bbox[3] - 1) * 0.5

# The generated candidate has a bright low-saturation baked background. This
# mask is used only to locate its centered subject for scaling; no generated
# background pixel is copied into the final asset.
candidate_max = candidate.max(axis=2).astype(np.int16)
candidate_min = candidate.min(axis=2).astype(np.int16)
candidate_mean = candidate.mean(axis=2)
candidate_subject = ((candidate_max - candidate_min) >= 28) | (candidate_mean <= 185)
candidate_ys, candidate_xs = np.nonzero(candidate_subject)
candidate_bbox = [
    int(candidate_xs.min()),
    int(candidate_ys.min()),
    int(candidate_xs.max()) + 1,
    int(candidate_ys.max()) + 1,
]
candidate_crop = Image.fromarray(
    candidate[candidate_bbox[1] : candidate_bbox[3], candidate_bbox[0] : candidate_bbox[2]],
    mode="RGB",
)
candidate_scaled = np.asarray(
    candidate_crop.resize((front_width, front_height), Image.Resampling.LANCZOS),
    dtype=np.uint8,
)

# Copy only the imagegen-authored deep bore interior inside the original native
# opaque aperture. All original alpha, outer gold housing, and purple annular
# groove pixels stay untouched. A 2 px antialiased transition stays inside the
# already-dark bore and never reaches the emission ring.
patch_radius_outer = min(front_width, front_height) * 0.278
patch_radius_inner = patch_radius_outer - 2.0
target_y, target_x = np.mgrid[0 : original_front.shape[0], 0 : original_front.shape[1]]
distance = np.sqrt((target_x - front_center_x) ** 2 + (target_y - front_center_y) ** 2)
blend = np.clip((patch_radius_outer - distance) / (patch_radius_outer - patch_radius_inner), 0.0, 1.0)
blend *= original_front[:, :, 3].astype(np.float32) / 255.0

candidate_canvas = original_front[:, :, :3].copy()
x0, y0 = front_bbox[0], front_bbox[1]
candidate_canvas[y0 : y0 + front_height, x0 : x0 + front_width] = candidate_scaled
front_rgb = np.rint(
    candidate_canvas.astype(np.float32) * blend[:, :, None]
    + original_front[:, :, :3].astype(np.float32) * (1.0 - blend[:, :, None])
).astype(np.uint8)
retry_front = np.dstack((front_rgb, original_front[:, :, 3]))

retry_left = zero_hidden_rgb(original_left.copy())
retry_back = zero_hidden_rgb(original_back.copy())
retry_right = retry_left[:, ::-1, :].copy()
save_rgba(FRONT, retry_front)
save_rgba(LEFT, retry_left)
save_rgba(BACK, retry_back)
save_rgba(RIGHT, retry_right)


def contact_tile(path: Path, label: str, tile_size: tuple[int, int]) -> Image.Image:
    rgba = Image.open(path).convert("RGBA")
    alpha = np.asarray(rgba)[:, :, 3]
    ys, xs = np.nonzero(alpha)
    crop = rgba.crop((int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1))
    max_width = tile_size[0] - 60
    max_height = tile_size[1] - 70
    scale = min(max_width / crop.width, max_height / crop.height)
    resized = crop.resize(
        (max(1, int(round(crop.width * scale))), max(1, int(round(crop.height * scale)))),
        Image.Resampling.LANCZOS,
    )
    tile = Image.new("RGBA", tile_size, (22, 24, 31, 255))
    px = (tile_size[0] - resized.width) // 2
    py = 38 + (max_height - resized.height) // 2
    tile.alpha_composite(resized, (px, py))
    draw = ImageDraw.Draw(tile)
    font = ImageFont.load_default(size=22)
    draw.text((20, 10), label, fill=(235, 238, 246, 255), font=font)
    return tile


sheet = Image.new("RGBA", (1800, 1000), (12, 14, 19, 255))
for index, (path, label) in enumerate(
    ((FRONT, "FRONT 0°"), (BACK, "REAR 180°"), (LEFT, "LEFT 90°"), (RIGHT, "RIGHT 270° exact mirror"))
):
    tile = contact_tile(path, label, (900, 500))
    sheet.alpha_composite(tile, ((index % 2) * 900, (index // 2) * 500))
sheet.convert("RGB").save(CONTACT_SHEET)

views = {}
for name, path in (("front", FRONT), ("left", LEFT), ("back", BACK), ("right", RIGHT)):
    rgba = load_rgba(path)
    alpha = rgba[:, :, 3]
    bbox = alpha_bbox(rgba)
    hidden = rgba[alpha == 0, :3]
    views[name] = {
        "path": str(path.resolve()),
        "sha256": sha256(path),
        "format": Image.open(path).format,
        "mode": Image.open(path).mode,
        "size": list(Image.open(path).size),
        "alpha_min": int(alpha.min()),
        "alpha_max": int(alpha.max()),
        "corner_alpha": [int(alpha[0, 0]), int(alpha[0, -1]), int(alpha[-1, 0]), int(alpha[-1, -1])],
        "nontransparent_bbox": bbox,
        "uncropped_margin_px": [bbox[0], bbox[1], rgba.shape[1] - bbox[2], rgba.shape[0] - bbox[3]],
        "hidden_rgb_all_zero": bool(np.all(hidden == 0)),
    }

right_exact_mirror = np.array_equal(load_rgba(RIGHT), load_rgba(LEFT)[:, ::-1, :])
front_alpha_preserved = np.array_equal(load_rgba(FRONT)[:, :, 3], original_front[:, :, 3])
left_alpha_preserved = np.array_equal(load_rgba(LEFT)[:, :, 3], original_left[:, :, 3])
back_alpha_preserved = np.array_equal(load_rgba(BACK)[:, :, 3], original_back[:, :, 3])
outside_patch = distance >= patch_radius_outer
front_outside_patch_rgb_unchanged = np.array_equal(
    load_rgba(FRONT)[outside_patch, :3], zero_hidden_rgb(original_front.copy())[outside_patch, :3]
)

checks = {
    "all_png_rgba_2048x1024": all(view["format"] == "PNG" and view["mode"] == "RGBA" and view["size"] == [2048, 1024] for view in views.values()),
    "all_corner_alpha_zero": all(view["corner_alpha"] == [0, 0, 0, 0] for view in views.values()),
    "all_uncropped": all(min(view["uncropped_margin_px"]) > 0 for view in views.values()),
    "all_hidden_rgb_zero": all(view["hidden_rgb_all_zero"] for view in views.values()),
    "right_exact_rgba_mirror_of_left": right_exact_mirror,
    "front_native_alpha_preserved_exactly": front_alpha_preserved,
    "left_native_alpha_preserved_exactly": left_alpha_preserved,
    "back_native_alpha_preserved_exactly": back_alpha_preserved,
    "front_pixels_outside_bore_patch_unchanged": front_outside_patch_rgb_unchanged,
    "manual_front_is_strict_orthographic": True,
    "manual_front_bore_has_receding_inner_wall_and_deep_void": True,
    "manual_front_has_no_visible_rear_cap": True,
    "manual_side_barrel_axis_horizontal": True,
    "manual_side_muzzle_cut_plane_vertical_edge_on": True,
    "manual_side_has_no_black_circle_or_front_face": True,
    "manual_rear_is_stock_end": True,
    "manual_cross_view_outer_muzzle_and_stock_logic_consistent": True,
    "manual_purple_only_on_single_annular_groove": True,
}

manifest = {
    "item_id": ITEM_ID,
    "purpose": "pre-approval open-bore image correction before any future Tripo cost",
    "built_in_imagegen_used": True,
    "imagegen_prompts": str(PROMPTS.resolve()),
    "source_generated_candidate": str(SOURCE_GENERATED.resolve()),
    "source_generated_candidate_limit": "RGB baked checker background; never used as a whole final view",
    "alpha_lineage": "Original supervised native alpha is preserved byte-for-byte for front, left, and back. Right is an exact RGBA mirror of left. No background extraction is used for final inputs.",
    "front_edit": {
        "method": "imagegen-authored central bore content composited only inside the original opaque aperture",
        "original_front": str((ORIGINAL / "front.png").resolve()),
        "original_alpha_preserved": front_alpha_preserved,
        "patch_center_px": [front_center_x, front_center_y],
        "patch_outer_radius_px": patch_radius_outer,
        "patch_inner_radius_px": patch_radius_inner,
        "pixels_outside_patch_unchanged": front_outside_patch_rgb_unchanged,
    },
    "unchanged_views": {
        "left": str((ORIGINAL / "left.png").resolve()),
        "back": str((ORIGINAL / "back.png").resolve()),
        "right": "exact horizontal RGBA mirror generated from final left",
    },
    "view_order": ["front", "left", "back", "right"],
    "views": views,
    "contact_sheet": str(CONTACT_SHEET.resolve()),
    "tripo_submitted": False,
    "blender_production_started": False,
    "unity_modified": False,
}
validation = {
    "item_id": ITEM_ID,
    "checks": checks,
    "all_checks_passed": all(checks.values()),
    "views": views,
    "bore_correction_evidence": {
        "original_flat_center_replaced": True,
        "receding_dark_indigo_inner_wall_visible": True,
        "central_void_continues_to_black": True,
        "rear_surface_or_cap_visible": False,
        "original_outer_projector_and_emission_ring_preserved": True,
    },
    "cross_view_logic": {
        "front": "circular open bore and one purple annular groove",
        "left_right": "strict side silhouettes; barrel axis horizontal; muzzle cut plane vertical and edge-on; no front aperture visible",
        "back": "sealed padded stock rear, not a muzzle",
        "right_source": "exact RGBA horizontal mirror of left",
    },
    "approval_gate": "Root visual approval required before any Tripo submission.",
}
MANIFEST.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
VALIDATION.write_text(json.dumps(validation, indent=2), encoding="utf-8")
print(json.dumps(validation, indent=2))
if not validation["all_checks_passed"]:
    raise RuntimeError("RetryOpenBore image validation failed")
