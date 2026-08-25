from __future__ import annotations

import argparse
import json
import shutil
from pathlib import Path

import cv2
import numpy as np
from PIL import Image


OUT = Path(__file__).resolve().parent
TEXTURES = OUT / "Textures"
QA = OUT / "QA" / "EmissionExpansion"
ITEM_ID = "item.weapon.rifle.railcarbine"
BASE_PATH = TEXTURES / f"{ITEM_ID}_BaseColor.png"
EMISSION_PATH = TEXTURES / f"{ITEM_ID}_Emission.png"
UNITY_EMISSION = (
    OUT.parents[4]
    / "Assets"
    / "SW"
    / "Models"
    / "Gunner_Weapon"
    / "Batch10"
    / ITEM_ID
    / f"{ITEM_ID}_Emission.png"
)
SEED_COLORS = (
    (30, 233, 248),
    (27, 234, 250),
    (30, 233, 250),
    (29, 232, 247),
)
REPRESENTATIVE = (29, 233, 249)

# These components are not selected merely because they are cyan-ish.  Each
# bbox was reviewed against the source BaseColor and the three user-provided
# Rail Carbine captures.  They are disconnected in raster space because the
# authored UV charts split the shaded portions from their bright seed pixels.
# The allowlist keeps the expansion limited to the identified emissive surface
# charts and prevents the same color family elsewhere from being selected.
USER_CAPTURE_COMPONENT_BBOXES = {
    "capture_01_horizontal_teal_patch": ((48, 368, 108, 31),),
    "capture_02_three_teal_patches": (
        (963, 786, 87, 44),
        (999, 972, 106, 72),
        (1217, 1034, 78, 47),
    ),
    "capture_03_vertical_teal_strip": ((1424, 1270, 32, 331),),
}
DESIGNATED_SHADE_COMPONENT_BBOXES = (
    (1377, 1877, 111, 67),
    (468, 1156, 62, 42),
    (1834, 906, 29, 84),
    (873, 1809, 21, 35),
    (1931, 1620, 19, 61),
    (1992, 1612, 16, 58),
    (1832, 879, 12, 16),
)

# This is not applied globally.  It defines the allowed step colors for a
# connected UV flood beginning only at exact user seed pixels.  Navy is
# rejected by V/cyan-dominance minima; gray/white and metallic highlights are
# rejected by saturation and hue.  No morphology, dilation, spatial painting,
# or component-size filtering is used.
COLOR_ENVELOPE = {
    "opencv_hue_min": 82,
    "opencv_hue_max": 98,
    "saturation_min": 90,
    "value_min": 90,
    "green_minus_red_min": 45,
    "blue_minus_red_min": 50,
    "abs_blue_minus_green_max": 65,
}
DARK_SHADE_ENVELOPE = {
    "opencv_hue_min": 82,
    "opencv_hue_max": 98,
    "saturation_min": 90,
    "value_min": 45,
    "green_minus_red_min": 20,
    "blue_minus_red_min": 25,
    "abs_blue_minus_green_max": 45,
}


def exact_seed(rgb: np.ndarray) -> np.ndarray:
    palette = np.array(SEED_COLORS, dtype=np.uint8)
    return np.any(np.all(rgb[:, :, None, :] == palette[None, None, :, :], axis=3), axis=2)


def cyan_step_envelope(rgb: np.ndarray) -> np.ndarray:
    hsv = cv2.cvtColor(rgb, cv2.COLOR_RGB2HSV)
    red = rgb[:, :, 0].astype(np.int16)
    green = rgb[:, :, 1].astype(np.int16)
    blue = rgb[:, :, 2].astype(np.int16)
    bright = (
        (hsv[:, :, 0] >= COLOR_ENVELOPE["opencv_hue_min"])
        & (hsv[:, :, 0] <= COLOR_ENVELOPE["opencv_hue_max"])
        & (hsv[:, :, 1] >= COLOR_ENVELOPE["saturation_min"])
        & (hsv[:, :, 2] >= COLOR_ENVELOPE["value_min"])
        & ((green - red) >= COLOR_ENVELOPE["green_minus_red_min"])
        & ((blue - red) >= COLOR_ENVELOPE["blue_minus_red_min"])
        & (np.abs(blue - green) <= COLOR_ENVELOPE["abs_blue_minus_green_max"])
    )
    dark_shade = (
        (hsv[:, :, 0] >= DARK_SHADE_ENVELOPE["opencv_hue_min"])
        & (hsv[:, :, 0] <= DARK_SHADE_ENVELOPE["opencv_hue_max"])
        & (hsv[:, :, 1] >= DARK_SHADE_ENVELOPE["saturation_min"])
        & (hsv[:, :, 2] >= DARK_SHADE_ENVELOPE["value_min"])
        & ((green - red) >= DARK_SHADE_ENVELOPE["green_minus_red_min"])
        & ((blue - red) >= DARK_SHADE_ENVELOPE["blue_minus_red_min"])
        & (np.abs(blue - green) <= DARK_SHADE_ENVELOPE["abs_blue_minus_green_max"])
    )
    return bright | dark_shade


def seeded_connected_expansion(rgb: np.ndarray):
    seed = exact_seed(rgb)
    envelope = cyan_step_envelope(rgb)
    if np.any(seed & ~envelope):
        raise RuntimeError("The strict connected-color envelope excludes an exact seed")
    count, labels, stats, centroids = cv2.connectedComponentsWithStats(envelope.astype(np.uint8), 8)
    retained_ids = np.unique(labels[seed])
    retained_ids = retained_ids[retained_ids > 0]
    selected = np.isin(labels, retained_ids)
    components = []
    for label_id in retained_ids:
        x, y, width, height, pixels = (int(value) for value in stats[label_id])
        component_seed_pixels = int((seed & (labels == label_id)).sum())
        components.append(
            {
                "id": int(label_id),
                "pixels": pixels,
                "seed_pixels": component_seed_pixels,
                "bbox_xywh": [x, y, width, height],
                "centroid_xy": [round(float(centroids[label_id][0]), 3), round(float(centroids[label_id][1]), 3)],
            }
        )
    components.sort(key=lambda entry: entry["pixels"], reverse=True)
    return seed, envelope, selected, components, count - 1, labels, stats, centroids, retained_ids


def authorize_reviewed_shade_components(labels: np.ndarray, stats: np.ndarray, centroids: np.ndarray):
    capture_lookup = {
        tuple(bbox): capture
        for capture, bboxes in USER_CAPTURE_COMPONENT_BBOXES.items()
        for bbox in bboxes
    }
    designated_lookup = {tuple(bbox) for bbox in DESIGNATED_SHADE_COMPONENT_BBOXES}
    required = set(capture_lookup) | designated_lookup
    found = {}
    for label_id in range(1, stats.shape[0]):
        x, y, width, height, pixels = (int(value) for value in stats[label_id])
        bbox = (x, y, width, height)
        if bbox not in required:
            continue
        found[bbox] = {
            "id": int(label_id),
            "pixels": pixels,
            "bbox_xywh": list(bbox),
            "centroid_xy": [round(float(centroids[label_id][0]), 3), round(float(centroids[label_id][1]), 3)],
            "review_basis": capture_lookup.get(bbox, "source_texture_designated_teal_surface_review"),
        }
    missing = sorted(required - set(found))
    if missing:
        raise RuntimeError(f"Reviewed dark-teal component bboxes changed or disappeared: {missing}")
    authorized_ids = np.array(sorted(record["id"] for record in found.values()), dtype=np.int32)
    mask = np.isin(labels, authorized_ids)
    records = sorted(found.values(), key=lambda entry: entry["pixels"], reverse=True)
    capture_records = {
        capture: [found[tuple(bbox)] for bbox in bboxes]
        for capture, bboxes in USER_CAPTURE_COMPONENT_BBOXES.items()
    }
    return mask, records, capture_records, authorized_ids


def union_bbox(records) -> list[int]:
    x0 = min(record["bbox_xywh"][0] for record in records)
    y0 = min(record["bbox_xywh"][1] for record in records)
    x1 = max(record["bbox_xywh"][0] + record["bbox_xywh"][2] for record in records)
    y1 = max(record["bbox_xywh"][1] + record["bbox_xywh"][3] for record in records)
    return [x0, y0, x1 - x0, y1 - y0]


def make_binary(selected: np.ndarray) -> np.ndarray:
    rgba = np.zeros((*selected.shape, 4), dtype=np.uint8)
    rgba[:, :, :3] = selected[:, :, None].astype(np.uint8) * 255
    rgba[:, :, 3] = 255
    return rgba


def make_overlay(source: np.ndarray, seed: np.ndarray, selected: np.ndarray) -> np.ndarray:
    overlay = source.copy()
    overlay[:, :, :3] = np.rint(overlay[:, :, :3].astype(np.float32) * 0.28).astype(np.uint8)
    expanded_only = selected & ~seed
    overlay[expanded_only, :3] = np.rint(
        source[expanded_only, :3].astype(np.float32) * 0.35 + np.array((0, 255, 80), dtype=np.float32) * 0.65
    ).astype(np.uint8)
    overlay[seed, :3] = np.array((255, 0, 255), dtype=np.uint8)
    overlay[:, :, 3] = 255
    return overlay


def crop_2x(image: np.ndarray, bbox, destination: Path) -> None:
    x, y, width, height = bbox
    padding = 18
    x0 = max(0, x - padding)
    y0 = max(0, y - padding)
    x1 = min(image.shape[1], x + width + padding)
    y1 = min(image.shape[0], y + height + padding)
    crop = Image.fromarray(image[y0:y1, x0:x1], mode="RGBA")
    crop.resize((crop.width * 2, crop.height * 2), Image.Resampling.NEAREST).save(destination)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--apply", action="store_true", help="Replace Production and Unity emission PNGs after QA")
    arguments = parser.parse_args()
    QA.mkdir(parents=True, exist_ok=True)
    source = np.asarray(Image.open(BASE_PATH).convert("RGBA"), dtype=np.uint8)
    rgb = source[:, :, :3]
    seed, envelope, seed_selected, components, envelope_component_count, labels, stats, centroids, retained_ids = seeded_connected_expansion(rgb)
    reviewed_shades, reviewed_components, capture_records, reviewed_ids = authorize_reviewed_shade_components(
        labels, stats, centroids
    )
    if np.any(seed_selected & reviewed_shades):
        raise RuntimeError("A reviewed seedless shade component unexpectedly overlaps the exact-seed flood")
    selected = seed_selected | reviewed_shades
    binary = make_binary(selected)
    overlay = make_overlay(source, seed, selected)
    preview_mask = QA / "expanded_binary_preview.png"
    overlay_path = QA / "basecolor_to_expanded_mask_overlay.png"
    Image.fromarray(binary, mode="RGBA").save(preview_mask)
    Image.fromarray(overlay, mode="RGBA").save(overlay_path)

    closeups = []
    for index, component in enumerate(components[:12], start=1):
        destination = QA / f"component_{index:02d}_2x.png"
        crop_2x(overlay, component["bbox_xywh"], destination)
        closeups.append(str(destination))

    retained_set = set(int(value) for value in retained_ids) | set(int(value) for value in reviewed_ids)
    excluded_components = []
    for label_id in range(1, envelope_component_count + 1):
        if label_id in retained_set or int(stats[label_id, 4]) < 100:
            continue
        x, y, width, height, pixels = (int(value) for value in stats[label_id])
        excluded_components.append(
            {
                "id": label_id,
                "pixels": pixels,
                "bbox_xywh": [x, y, width, height],
                "centroid_xy": [round(float(centroids[label_id][0]), 3), round(float(centroids[label_id][1]), 3)],
            }
        )
    excluded_components.sort(key=lambda entry: entry["pixels"], reverse=True)
    excluded_overlay = overlay.copy()
    excluded_mask = envelope & ~selected
    excluded_overlay[excluded_mask, :3] = np.array((255, 128, 0), dtype=np.uint8)
    excluded_overlay_path = QA / "excluded_seedless_dark_teal_candidates.png"
    Image.fromarray(excluded_overlay, mode="RGBA").save(excluded_overlay_path)
    excluded_closeups = []
    for index, component in enumerate(excluded_components[:12], start=1):
        destination = QA / f"excluded_candidate_{index:02d}_2x.png"
        crop_2x(excluded_overlay, component["bbox_xywh"], destination)
        excluded_closeups.append(str(destination))

    capture_closeups = {}
    before_overlay = make_overlay(source, seed, seed_selected)
    before_overlay[reviewed_shades, :3] = np.array((255, 128, 0), dtype=np.uint8)
    after_overlay = make_overlay(source, seed, selected)
    for capture, records in capture_records.items():
        bbox = union_bbox(records)
        before_path = QA / f"{capture}_before_2x.png"
        after_path = QA / f"{capture}_after_2x.png"
        crop_2x(before_overlay, bbox, before_path)
        crop_2x(after_overlay, bbox, after_path)
        capture_closeups[capture] = {
            "component_bboxes_xywh": [record["bbox_xywh"] for record in records],
            "before_missed_orange": str(before_path),
            "after_selected_green": str(after_path),
        }

    # The intended region is the exact-seed flood plus the finite allowlist of
    # shaded UV-chart components visually identified on the Rail Carbine.
    intended_component_ids = np.concatenate((retained_ids.astype(np.int32), reviewed_ids))
    intended_region = envelope & np.isin(labels, intended_component_ids)
    intended_holes = int((intended_region & ~selected).sum())
    non_cyan_false_positives = int((selected & ~envelope).sum())
    seed_missed = int((seed & ~selected).sum())
    report = {
        "source_base_color": str(BASE_PATH),
        "method": "8-connected UV color flood from the four exact seed colors plus a finite bbox-locked allowlist of visually reviewed shaded components from the same intended Rail Carbine emissive surfaces",
        "global_broad_threshold_used": False,
        "morphology_dilation_blur_spatial_paint_used": False,
        "seed_colors_srgb": [list(color) for color in SEED_COLORS],
        "representative_srgb": list(REPRESENTATIVE),
        "color_envelope": COLOR_ENVELOPE,
        "dark_shade_envelope": DARK_SHADE_ENVELOPE,
        "seed_exact_pixels": int(seed.sum()),
        "expanded_selected_pixels": int(selected.sum()),
        "expanded_only_pixels": int((selected & ~seed).sum()),
        "seed_flood_selected_pixels": int(seed_selected.sum()),
        "user_capture_and_reviewed_shade_pixels": int(reviewed_shades.sum()),
        "envelope_all_pixels_before_seed_connectivity": int(envelope.sum()),
        "envelope_component_count": envelope_component_count,
        "retained_seed_component_count": len(components),
        "components": components,
        "reviewed_shade_component_count": len(reviewed_components),
        "reviewed_shade_components": reviewed_components,
        "user_capture_component_mapping": capture_records,
        "exact_seed_missed": seed_missed,
        "cyan_intended_region_hole_count": intended_holes,
        "non_cyan_false_positive_pixels": non_cyan_false_positives,
        "excluded_seedless_envelope_pixels": int((envelope & ~selected).sum()),
        "excluded_components_over_100_pixels": excluded_components,
        "excluded_candidates_overlay": str(excluded_overlay_path),
        "excluded_candidates_2x": excluded_closeups,
        "user_capture_before_after_closeups": capture_closeups,
        "preview_mask": str(preview_mask),
        "overlay": str(overlay_path),
        "closeups_2x": closeups,
        "production_emission": str(EMISSION_PATH),
        "unity_emission": str(UNITY_EMISSION),
        "apply_requested": arguments.apply,
        "checks": {
            "exact_seeds_all_preserved": seed_missed == 0,
            "expansion_is_nonempty": int(selected.sum()) > int(seed.sum()),
            "intended_region_has_no_holes": intended_holes == 0,
            "no_non_cyan_false_positive": non_cyan_false_positives == 0,
            "binary_rgba": sorted(int(value) for value in np.unique(binary[:, :, :3])) == [0, 255]
            and bool(np.all(binary[:, :, 3] == 255)),
        },
    }
    report["pass"] = all(report["checks"].values())
    if arguments.apply:
        if not report["pass"]:
            raise RuntimeError("Refusing to apply a failed emission expansion")
        Image.fromarray(binary, mode="RGBA").save(EMISSION_PATH)
        if not UNITY_EMISSION.is_file():
            raise RuntimeError(f"Unity emission target missing: {UNITY_EMISSION}")
        shutil.copy2(EMISSION_PATH, UNITY_EMISSION)
    (OUT / "emission_expansion_validation.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(
        json.dumps(
            {
                "seed_exact_pixels": report["seed_exact_pixels"],
                "expanded_selected_pixels": report["expanded_selected_pixels"],
                "retained_seed_components": report["retained_seed_component_count"],
                "reviewed_shade_components": report["reviewed_shade_component_count"],
                "excluded_seedless_pixels": report["excluded_seedless_envelope_pixels"],
                "pass": report["pass"],
                "applied": arguments.apply,
            }
        )
    )


if __name__ == "__main__":
    main()
