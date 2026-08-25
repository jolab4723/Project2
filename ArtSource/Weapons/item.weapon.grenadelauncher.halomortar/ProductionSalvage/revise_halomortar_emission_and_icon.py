from __future__ import annotations

import hashlib
import json
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw


PROJECT = Path(__file__).resolve().parents[4]
ITEM_ID = "item.weapon.grenadelauncher.halomortar"
SALVAGE = PROJECT / "ArtSource" / "Weapons" / ITEM_ID / "ProductionSalvage"
TEXTURES = SALVAGE / "Textures"
QA = SALVAGE / "QA" / "EmissionRevision"
ICON = PROJECT / "Assets" / "Resources" / "Images" / "Item" / "OriginalImage" / f"{ITEM_ID}.source.png"
BASE_COLOR = TEXTURES / f"{ITEM_ID}_BaseColor.png"
EMISSION = TEXTURES / f"{ITEM_ID}_Emission.png"
BATCH10 = PROJECT / "Assets" / "SW" / "Models" / "Gunner_Weapon" / "Batch10" / ITEM_ID
BATCH10_BASE_COLOR = BATCH10 / f"{ITEM_ID}_BaseColor.png"
BATCH10_EMISSION = BATCH10 / f"{ITEM_ID}_Emission.png"

BLUE_SAMPLE_OLD = Path(r"C:\Users\user\AppData\Local\Temp\codex-clipboard-4884e230-8873-439b-81a7-4d9cdafff209.png")
BLUE_SAMPLE_NEW = Path(r"C:\Users\user\AppData\Local\Temp\codex-clipboard-ca31c761-6379-4878-b13a-8f616582845a.png")
PURPLE_SAMPLE = Path(r"C:\Users\user\AppData\Local\Temp\codex-clipboard-06fc833e-9a75-4386-b07c-cee95023668c.png")
ICON_RING_IMAGEGEN = QA / "icon_ring_imagegen_full.png"

BASE_COLOR_BEFORE = QA / "basecolor_before_blue_to_purple.png"
EMISSION_BEFORE = QA / "emission_before_blue_to_purple.png"
ICON_BEFORE = QA / "icon_before_ring_imagegen.png"


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def gray_composite(rgba: np.ndarray, gray: int = 128) -> np.ndarray:
    alpha = rgba[:, :, 3:4].astype(np.float32) / 255.0
    rgb = rgba[:, :, :3].astype(np.float32)
    return np.rint(rgb * alpha + gray * (1.0 - alpha)).astype(np.uint8)


def rgba(path: Path) -> np.ndarray:
    return np.asarray(Image.open(path).convert("RGBA"), dtype=np.uint8).copy()


def save_rgba(path: Path, pixels: np.ndarray) -> None:
    Image.fromarray(pixels.astype(np.uint8), "RGBA").save(path)


def save_rgb(path: Path, pixels: np.ndarray) -> None:
    Image.fromarray(pixels.astype(np.uint8), "RGB").save(path)


def component_stats(mask: np.ndarray) -> list[dict[str, object]]:
    count, _, stats, centroids = cv2.connectedComponentsWithStats(mask.astype(np.uint8), 8)
    rows = []
    for component_id in range(1, count):
        x, y, width, height, area = stats[component_id].tolist()
        rows.append(
            {
                "area": int(area),
                "bbox_xywh": [int(x), int(y), int(width), int(height)],
                "centroid_xy": [round(float(v), 3) for v in centroids[component_id]],
            }
        )
    return sorted(rows, key=lambda row: int(row["area"]), reverse=True)


def purple_pixels(rgb: np.ndarray) -> np.ndarray:
    values = rgb.astype(np.int16)
    return (
        (values[:, :, 0] >= 80)
        & (values[:, :, 2] >= 120)
        & (values[:, :, 0] > values[:, :, 1] + 20)
        & (values[:, :, 2] > values[:, :, 1] + 40)
    )


def pack_rgb(rgb: np.ndarray) -> np.ndarray:
    return (
        (rgb[:, :, 0].astype(np.uint32) << 16)
        | (rgb[:, :, 1].astype(np.uint32) << 8)
        | rgb[:, :, 2].astype(np.uint32)
    )


def exact_mask(rgb: np.ndarray, whitelist: set[tuple[int, int, int]]) -> np.ndarray:
    keys = np.fromiter(((r << 16) | (g << 8) | b for r, g, b in sorted(whitelist)), dtype=np.uint32)
    return np.isin(pack_rgb(rgb), keys)


def rgb_set(rgb: np.ndarray, mask: np.ndarray) -> set[tuple[int, int, int]]:
    return set(map(tuple, np.unique(rgb[mask], axis=0).tolist()))


def mask_rgba(mask: np.ndarray) -> np.ndarray:
    out = np.zeros((*mask.shape, 4), dtype=np.uint8)
    out[:, :, :3] = np.where(mask[:, :, None], 255, 0)
    out[:, :, 3] = 255
    return out


def basecolor_emission_overlay(base_rgb: np.ndarray, existing: np.ndarray, recolored: np.ndarray) -> np.ndarray:
    overlay = np.rint(base_rgb.astype(np.float32) * 0.28).astype(np.uint8)
    overlay[existing] = np.array([255, 54, 231], dtype=np.uint8)
    overlay[recolored] = np.array([187, 95, 255], dtype=np.uint8)
    return overlay


def make_icon_ring(before: np.ndarray) -> tuple[np.ndarray, dict[str, object]]:
    source_purple = purple_pixels(before[:, :, :3]) & (before[:, :, 3] > 0)
    source_component = component_stats(source_purple)[0]
    x, y, width, height = source_component["bbox_xywh"]

    generated_rgb = np.asarray(Image.open(ICON_RING_IMAGEGEN).convert("RGB"), dtype=np.uint8)
    generated_component = component_stats(purple_pixels(generated_rgb))[0]
    gx, gy, gwidth, gheight = generated_component["bbox_xywh"]
    generated_crop = generated_rgb[gy : gy + gheight, gx : gx + gwidth]
    # Keep the imagegen-authored highlight/falloff while excluding the adjacent
    # gold/black seams that entered the automatically detected bounding box.
    inset_crop = generated_crop[8 : gheight - 8, 3 : gwidth - 3]
    resized = np.asarray(
        Image.fromarray(inset_crop, "RGB").resize((width, height), Image.Resampling.LANCZOS),
        dtype=np.uint8,
    )
    generated_hsv = cv2.cvtColor(resized, cv2.COLOR_RGB2HSV)
    generated_hsv[:, :, 0] = 134
    generated_hsv[:, :, 1] = np.clip(generated_hsv[:, :, 1], 140, 220)
    resized = cv2.cvtColor(generated_hsv, cv2.COLOR_HSV2RGB)

    after = before.copy()
    roi = after[y : y + height, x : x + width]
    source_ring_mask = before[y : y + height, x : x + width, 3] > 0
    roi[:, :, :3][source_ring_mask] = resized[source_ring_mask]
    # Alpha is immutable for the inventory source. The imagegen candidate only
    # supplies cylindrical RGB shading inside the original purple-band pixels.
    roi[:, :, 3] = before[y : y + height, x : x + width, 3]
    after[after[:, :, 3] == 0, :3] = 0

    outside = np.ones(before.shape[:2], dtype=bool)
    outside[y : y + height, x : x + width] = False
    any_diff = np.any(before != after, axis=2)
    rgb_diff = np.any(before[:, :, :3] != after[:, :, :3], axis=2)
    alpha_diff = before[:, :, 3] != after[:, :, 3]
    report = {
        "imagegen_source": str(ICON_RING_IMAGEGEN),
        "imagegen_source_sha256": sha256(ICON_RING_IMAGEGEN),
        "imagegen_detected_ring_bbox_xywh": [int(gx), int(gy), int(gwidth), int(gheight)],
        "edited_source_bbox_xywh": [int(x), int(y), int(width), int(height)],
        "changed_pixels_total": int(any_diff.sum()),
        "changed_pixels_outside_ring_bbox": int((any_diff & outside).sum()),
        "rgb_changed_pixels_total": int(rgb_diff.sum()),
        "rgb_changed_pixels_outside_ring_bbox": int((rgb_diff & outside).sum()),
        "alpha_changed_pixels_total": int(alpha_diff.sum()),
        "alpha_changed_pixels_outside_ring_bbox": int((alpha_diff & outside).sum()),
        "alpha_exactly_preserved": bool(np.array_equal(before[:, :, 3], after[:, :, 3])),
        "after_alpha0_hidden_rgb_nonzero_px": int(((after[:, :, 3] == 0) & (after[:, :, :3].max(axis=2) > 0)).sum()),
        "canvas": {"width": int(after.shape[1]), "height": int(after.shape[0]), "mode": "RGBA"},
    }
    return after, report


def make_icon_qa(before: np.ndarray, after: np.ndarray, report: dict[str, object]) -> None:
    before_gray = gray_composite(before)
    after_gray = gray_composite(after)
    save_rgb(QA / "icon_before_gray.png", before_gray)
    save_rgb(QA / "icon_after_gray.png", after_gray)

    x, y, width, height = report["edited_source_bbox_xywh"]
    pad_x, pad_y = 170, 70
    crop = (
        max(0, x - pad_x),
        max(0, y - pad_y),
        min(before.shape[1], x + width + pad_x),
        min(before.shape[0], y + height + pad_y),
    )
    before_crop = Image.fromarray(before_gray, "RGB").crop(crop).resize((774, 708), Image.Resampling.NEAREST)
    after_crop = Image.fromarray(after_gray, "RGB").crop(crop).resize((774, 708), Image.Resampling.NEAREST)
    sheet = Image.new("RGB", (1548, 748), (36, 36, 40))
    sheet.paste(before_crop, (0, 40))
    sheet.paste(after_crop, (774, 40))
    draw = ImageDraw.Draw(sheet)
    draw.text((12, 12), "BEFORE: flat purple band", fill=(255, 255, 255))
    draw.text((786, 12), "AFTER: imagegen cylindrical purple energy ring", fill=(255, 255, 255))
    sheet.save(QA / "icon_ring_before_after_gray.png")

    diff = np.abs(after.astype(np.int16) - before.astype(np.int16)).max(axis=2).astype(np.uint8)
    diff_rgb = np.zeros((*diff.shape, 3), dtype=np.uint8)
    diff_rgb[:, :, 0] = diff
    diff_rgb[:, :, 1] = diff // 5
    save_rgb(QA / "icon_ring_diff.png", diff_rgb)


def recolor_and_build_emission(
    base_before: np.ndarray,
    emission_before: np.ndarray,
) -> tuple[np.ndarray, np.ndarray, dict[str, object], set[tuple[int, int, int]]]:
    rgb_before = base_before[:, :, :3]
    old_mask = emission_before[:, :, 0] == 255
    binary_before = set(np.unique(emission_before[:, :, 0]).tolist()) <= {0, 255}

    blue_seed = (
        (rgb_before[:, :, 0] >= 63)
        & (rgb_before[:, :, 0] <= 70)
        & (rgb_before[:, :, 1] == 92)
        & (rgb_before[:, :, 2] >= 191)
        & (rgb_before[:, :, 2] <= 211)
    )
    blue_seed_colors = rgb_set(rgb_before, blue_seed)

    base_after = base_before.copy()
    rgb_after = base_after[:, :, :3]
    delta_blue = rgb_before[:, :, 2].astype(np.int16) - 201
    mapped_r = np.clip(
        121 + np.rint(0.4 * delta_blue).astype(np.int16) + (rgb_before[:, :, 0].astype(np.int16) - 67),
        111,
        125,
    )
    mapped_g = np.clip(62 + np.rint(0.3 * delta_blue).astype(np.int16), 56, 65)
    mapped_b = np.clip(194 + np.rint(0.3 * delta_blue).astype(np.int16), 182, 197)
    rgb_after[:, :, 0][blue_seed] = mapped_r[blue_seed]
    rgb_after[:, :, 1][blue_seed] = mapped_g[blue_seed]
    rgb_after[:, :, 2][blue_seed] = mapped_b[blue_seed]

    residual_blue = (
        (rgb_after[:, :, 0] >= 63)
        & (rgb_after[:, :, 0] <= 70)
        & (rgb_after[:, :, 1] == 92)
        & (rgb_after[:, :, 2] >= 191)
        & (rgb_after[:, :, 2] <= 211)
    )

    existing_purple_rgb = rgb_set(rgb_before, old_mask)
    recolored_purple_rgb = rgb_set(rgb_after, blue_seed)
    final_whitelist = existing_purple_rgb | recolored_purple_rgb
    final_mask = exact_mask(rgb_after, final_whitelist)
    intended = old_mask | blue_seed
    false_positive = final_mask & ~intended
    missed = intended & ~final_mask

    if not binary_before:
        raise RuntimeError("The prior emission texture was not binary.")
    if false_positive.any() or missed.any() or residual_blue.any():
        raise RuntimeError(
            f"Exact RGB audit failed: false_positive={false_positive.sum()}, "
            f"missed={missed.sum()}, residual_blue={residual_blue.sum()}"
        )

    emission_after = mask_rgba(final_mask)
    whitelist_sorted = sorted([list(color) for color in final_whitelist])
    whitelist_payload = json.dumps(whitelist_sorted, separators=(",", ":")).encode("utf-8")
    report = {
        "selection_method": "global exact RGB whitelist equality only",
        "spatial_uv_mesh_component_morphology_blur_used_for_mask": False,
        "blue_seed_envelope_inclusive": {"r": [63, 70], "g": [92, 92], "b": [191, 211]},
        "blue_sample_paths": [str(BLUE_SAMPLE_OLD), str(BLUE_SAMPLE_NEW)],
        "blue_sample_medians": [[65, 92, 196], [67, 92, 201]],
        "purple_sample_path": str(PURPLE_SAMPLE),
        "purple_sample_median": [121, 62, 194],
        "purple_sample_envelope_inclusive": {"r": [111, 125], "g": [56, 65], "b": [182, 197]},
        "recolor_formula": {
            "delta": "source_B - 201",
            "R": "clamp(121 + round(0.4*delta) + (source_R-67), 111, 125)",
            "G": "clamp(62 + round(0.3*delta), 56, 65)",
            "B": "clamp(194 + round(0.3*delta), 182, 197)",
        },
        "before_blue_selected_px": int(blue_seed.sum()),
        "before_blue_exact_rgb_count": len(blue_seed_colors),
        "recolored_px": int(blue_seed.sum()),
        "basecolor_changed_outside_blue_seed_px": int(
            (np.any(rgb_after != rgb_before, axis=2) & ~blue_seed).sum()
        ),
        "navy_gold_black_white_recolored_px": 0,
        "after_recolored_rgb_min": rgb_after[blue_seed].min(axis=0).tolist(),
        "after_recolored_rgb_max": rgb_after[blue_seed].max(axis=0).tolist(),
        "after_residual_blue_px": int(residual_blue.sum()),
        "existing_purple_emission_px": int(old_mask.sum()),
        "existing_purple_exact_rgb_count": len(existing_purple_rgb),
        "recolored_purple_exact_rgb_count": len(recolored_purple_rgb),
        "final_exact_rgb_whitelist_count": len(final_whitelist),
        "final_exact_rgb_whitelist_sha256": hashlib.sha256(whitelist_payload).hexdigest(),
        "final_purple_emission_selected_px": int(final_mask.sum()),
        "recolored_purple_target_selected_px": int((final_mask & blue_seed).sum()),
        "recolored_purple_target_missed_px": int((blue_seed & ~final_mask).sum()),
        "existing_purple_target_selected_px": int((final_mask & old_mask).sum()),
        "existing_purple_target_missed_px": int((old_mask & ~final_mask).sum()),
        "final_selected_residual_blue_px": int((final_mask & residual_blue).sum()),
        "existing_mask_missed_target_px": int((intended & ~old_mask).sum()),
        "final_non_target_false_positive_px": int(false_positive.sum()),
        "final_target_missed_px": int(missed.sum()),
        "final_binary_values": sorted(np.unique(emission_after[:, :, 0]).tolist()),
        "gold_black_white_selected_px": int(
            (
                final_mask
                & (
                    ((rgb_after[:, :, 0] > 145) & (rgb_after[:, :, 1] > 90) & (rgb_after[:, :, 2] < 100))
                    | (rgb_after.max(axis=2) <= 24)
                    | (rgb_after.min(axis=2) >= 205)
                )
            ).sum()
        ),
    }
    return base_after, emission_after, report, final_whitelist


def make_revision_contact_sheet() -> None:
    render_dir = QA / "Renders"
    required = [
        QA / "icon_ring_before_after_gray.png",
        BASE_COLOR_BEFORE,
        QA / "basecolor_after_blue_to_purple.png",
        QA / "basecolor_to_emission_overlay.png",
        QA / "emission_after_exact_rgb_binary.png",
        render_dir / "pbr_emission_front.png",
        render_dir / "pbr_emission_iso.png",
        render_dir / "emission_only_front.png",
        render_dir / "emission_only_iso.png",
    ]
    if not all(path.exists() for path in required):
        return

    sheet = Image.new("RGB", (1600, 1420), (24, 24, 28))
    draw = ImageDraw.Draw(sheet)
    draw.text((20, 14), "HALO MORTAR — ICON / EXACT RGB RECOLOR / EMISSION QA", fill=(255, 255, 255))

    icon = Image.open(required[0]).convert("RGB")
    icon.thumbnail((1560, 700), Image.Resampling.LANCZOS)
    sheet.paste(icon, ((1600 - icon.width) // 2, 44))

    texture_titles = ["BASE BEFORE", "BASE AFTER", "BASE→MASK OVERLAY", "BINARY EMISSION"]
    texture_paths = required[1:5]
    for index, (title, path) in enumerate(zip(texture_titles, texture_paths)):
        x = 20 + index * 395
        draw.text((x, 798), title, fill=(230, 230, 236))
        tile = Image.open(path).convert("RGB")
        tile.thumbnail((375, 375), Image.Resampling.LANCZOS)
        sheet.paste(tile, (x, 822))

    render_titles = ["PBR FRONT", "PBR ISO", "EMISSION FRONT", "EMISSION ISO"]
    render_paths = required[5:]
    for index, (title, path) in enumerate(zip(render_titles, render_paths)):
        x = 20 + index * 395
        draw.text((x, 1212), title, fill=(230, 230, 236))
        tile = Image.open(path).convert("RGB")
        tile.thumbnail((375, 175), Image.Resampling.LANCZOS)
        sheet.paste(tile, (x, 1236))
    sheet.save(QA / "revision_contact_sheet.png")


def main() -> None:
    QA.mkdir(parents=True, exist_ok=True)

    if not BASE_COLOR_BEFORE.exists():
        save_rgba(BASE_COLOR_BEFORE, rgba(BASE_COLOR))
    if not EMISSION_BEFORE.exists():
        save_rgba(EMISSION_BEFORE, rgba(EMISSION))
    if not ICON_BEFORE.exists():
        save_rgba(ICON_BEFORE, rgba(ICON))

    icon_before = rgba(ICON_BEFORE)
    icon_after, icon_report = make_icon_ring(icon_before)
    save_rgba(ICON, icon_after)
    make_icon_qa(icon_before, icon_after, icon_report)
    (QA / "icon_ring_revision_report.json").write_text(json.dumps(icon_report, indent=2), encoding="utf-8")

    base_before = rgba(BASE_COLOR_BEFORE)
    emission_before = rgba(EMISSION_BEFORE)
    base_after, emission_after, emission_report, whitelist = recolor_and_build_emission(base_before, emission_before)
    save_rgba(BASE_COLOR, base_after)
    save_rgba(EMISSION, emission_after)
    save_rgba(BATCH10_BASE_COLOR, base_after)
    save_rgba(BATCH10_EMISSION, emission_after)

    old_mask = emission_before[:, :, 0] == 255
    recolored = np.any(base_after[:, :, :3] != base_before[:, :, :3], axis=2)
    final_mask = emission_after[:, :, 0] == 255
    save_rgba(QA / "basecolor_after_blue_to_purple.png", base_after)
    save_rgba(QA / "emission_after_exact_rgb_binary.png", emission_after)
    save_rgb(
        QA / "basecolor_to_emission_overlay.png",
        basecolor_emission_overlay(base_after[:, :, :3], old_mask, recolored),
    )

    change = np.abs(base_after[:, :, :3].astype(np.int16) - base_before[:, :, :3].astype(np.int16)).max(axis=2)
    change_rgb = np.zeros((*change.shape, 3), dtype=np.uint8)
    change_rgb[:, :, 0] = change
    change_rgb[:, :, 2] = np.where(recolored, 255, 0)
    save_rgb(QA / "basecolor_blue_to_purple_diff.png", change_rgb)

    exact_list = sorted([list(color) for color in whitelist])
    (QA / "emission_exact_rgb_whitelist.json").write_text(
        json.dumps({"rgb_values": exact_list}, indent=2), encoding="utf-8"
    )

    emission_report["artsource_basecolor_sha256"] = sha256(BASE_COLOR)
    emission_report["artsource_emission_sha256"] = sha256(EMISSION)
    emission_report["batch10_basecolor_sha256"] = sha256(BATCH10_BASE_COLOR)
    emission_report["batch10_emission_sha256"] = sha256(BATCH10_EMISSION)
    emission_report["artsource_batch10_basecolor_identical"] = sha256(BASE_COLOR) == sha256(BATCH10_BASE_COLOR)
    emission_report["artsource_batch10_emission_identical"] = sha256(EMISSION) == sha256(BATCH10_EMISSION)
    emission_report["final_mask_component_count_for_audit_only"] = len(component_stats(final_mask))
    (QA / "emission_revision_report.json").write_text(json.dumps(emission_report, indent=2), encoding="utf-8")
    make_revision_contact_sheet()


if __name__ == "__main__":
    main()
