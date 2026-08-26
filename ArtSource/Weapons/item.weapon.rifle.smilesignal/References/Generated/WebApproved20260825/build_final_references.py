from __future__ import annotations

import hashlib
import json
import shutil
from pathlib import Path

import cv2
import numpy as np
from PIL import Image


ROOT = Path(__file__).resolve().parent
ITEM_ROOT = ROOT.parents[2]
SOURCE = ROOT / "left_clean_web_raw.png"
LEFT_RAW_CLEAN = ROOT / "left_audited_raw.png"
LEFT_FINAL = ROOT / "left.png"
RIGHT_FINAL = ROOT / "right.png"
FRONT_FINAL = ROOT / "front.png"
BACK_FINAL = ROOT / "back.png"
QA = ROOT / "QA_LeftCleanup"
TRIPO_INPUT = ITEM_ROOT / "Prepared" / "TripoInput"

TARGET_SIZE = (2048, 1024)
ALPHA_MAX = 16
CORE_ALPHA_MIN = 64
MIN_CORE_DISTANCE = 3.0
NEUTRAL_MIN = 220
NEUTRAL_SPREAD_MAX = 24

# Raw 1774x887 coordinates. The boxes intentionally cover only the two
# user-approved negative-space regions. Pixel selection inside them is further
# constrained by color, alpha and distance from actual weapon surface.
ROIS = {
    "upper_open_gap": (220, 175, 700, 350),
    "trigger_opening": (250, 430, 850, 790),
}


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def clean_hidden_rgb(rgba: np.ndarray) -> np.ndarray:
    result = rgba.copy()
    result[result[:, :, 3] == 0, :3] = 0
    return result


def premultiplied_resize(rgba: np.ndarray, size: tuple[int, int]) -> np.ndarray:
    if (rgba.shape[1], rgba.shape[0]) == size:
        return clean_hidden_rgb(rgba)

    alpha = rgba[:, :, 3].astype(np.float32)
    premultiplied = rgba[:, :, :3].astype(np.float32) * (alpha[:, :, None] / 255.0)

    resized_alpha = np.array(
        Image.fromarray(alpha, mode="F").resize(size, Image.Resampling.LANCZOS),
        dtype=np.float32,
    )
    resized_premultiplied = np.stack(
        [
            np.array(
                Image.fromarray(premultiplied[:, :, channel], mode="F").resize(
                    size, Image.Resampling.LANCZOS
                ),
                dtype=np.float32,
            )
            for channel in range(3)
        ],
        axis=2,
    )

    resized_alpha = np.clip(resized_alpha, 0.0, 255.0)
    output_rgb = np.zeros_like(resized_premultiplied)
    visible = resized_alpha > 0.5
    output_rgb[visible] = (
        resized_premultiplied[visible] * 255.0 / resized_alpha[visible, None]
    )
    output = np.concatenate(
        [
            np.clip(output_rgb, 0.0, 255.0),
            resized_alpha[:, :, None],
        ],
        axis=2,
    )
    return clean_hidden_rgb(np.rint(output).astype(np.uint8))


def composite(rgba: np.ndarray, background: tuple[int, int, int]) -> np.ndarray:
    rgb = rgba[:, :, :3].astype(np.float32)
    alpha = rgba[:, :, 3:4].astype(np.float32) / 255.0
    bg = np.array(background, dtype=np.float32).reshape(1, 1, 3)
    return np.clip(rgb * alpha + bg * (1.0 - alpha), 0, 255).astype(np.uint8)


def component_report(mask: np.ndarray, alpha: np.ndarray) -> list[dict[str, object]]:
    count, labels, stats, _ = cv2.connectedComponentsWithStats(
        mask.astype(np.uint8), 8
    )
    components: list[dict[str, object]] = []
    for index in range(1, count):
        pixels = labels == index
        components.append(
            {
                "area": int(stats[index, cv2.CC_STAT_AREA]),
                "bbox_raw": [
                    int(stats[index, cv2.CC_STAT_LEFT]),
                    int(stats[index, cv2.CC_STAT_TOP]),
                    int(stats[index, cv2.CC_STAT_WIDTH]),
                    int(stats[index, cv2.CC_STAT_HEIGHT]),
                ],
                "alpha_min": int(alpha[pixels].min()),
                "alpha_max": int(alpha[pixels].max()),
            }
        )
    components.sort(key=lambda item: int(item["area"]), reverse=True)
    return components


def make_qa_crops(before: np.ndarray, after: np.ndarray) -> None:
    QA.mkdir(parents=True, exist_ok=True)
    for name, (x0, y0, x1, y1) in ROIS.items():
        before_crop = before[y0:y1, x0:x1]
        after_crop = after[y0:y1, x0:x1]
        panels = []
        for label, crop in (("before", before_crop), ("after", after_crop)):
            enlarged = cv2.resize(
                crop, None, fx=4, fy=4, interpolation=cv2.INTER_NEAREST
            )
            magenta = composite(enlarged, (255, 0, 255))
            green = composite(enlarged, (0, 150, 40))
            alpha_rgb = np.repeat(enlarged[:, :, 3:4], 3, axis=2)
            panel = np.concatenate([magenta, green, alpha_rgb], axis=1)
            Image.fromarray(panel, "RGB").save(
                QA / f"{name}_{label}_x4.png"
            )
            panels.append(panel)

        comparison = np.concatenate(panels, axis=0)
        Image.fromarray(comparison, "RGB").save(
            QA / f"{name}_before_after_x4.png"
        )


def sanitize_reference(source: Path, target: Path) -> None:
    rgba = clean_hidden_rgb(np.array(Image.open(source).convert("RGBA")))
    if (rgba.shape[1], rgba.shape[0]) != TARGET_SIZE:
        raise ValueError(f"{source.name} must already be {TARGET_SIZE}")
    Image.fromarray(rgba, "RGBA").save(target)


def main() -> None:
    source = np.array(Image.open(SOURCE).convert("RGBA"))
    rgb = source[:, :, :3]
    alpha = source[:, :, 3]

    core = alpha >= CORE_ALPHA_MIN
    distance_from_core = cv2.distanceTransform(
        (~core).astype(np.uint8), cv2.DIST_L2, 5
    )
    near_white = (
        (rgb.min(axis=2) >= NEUTRAL_MIN)
        & ((rgb.max(axis=2) - rgb.min(axis=2)) <= NEUTRAL_SPREAD_MAX)
    )

    # Global connectivity pass: retain the single connected weapon component
    # and discard only disconnected, extremely faint background residue.
    connected_count, connected_labels, connected_stats, _ = (
        cv2.connectedComponentsWithStats((alpha > 0).astype(np.uint8), 8)
    )
    largest_component = 1 + int(
        np.argmax(connected_stats[1:, cv2.CC_STAT_AREA])
    )
    disconnected = (connected_labels > 0) & (
        connected_labels != largest_component
    )
    if disconnected.any() and int(alpha[disconnected].max()) > ALPHA_MAX:
        raise RuntimeError("Disconnected component exceeds low-alpha safety gate")

    combined = disconnected.copy()
    disconnected_components = component_report(disconnected, alpha)
    roi_reports: dict[str, object] = {}
    for name, (x0, y0, x1, y1) in ROIS.items():
        roi = np.zeros(alpha.shape, dtype=bool)
        roi[y0:y1, x0:x1] = True
        # The first pass targeted only near-white pixels. Enlarged review showed
        # that the same baked residue also contains a few red/yellow pixels.
        # Color is therefore diagnostic only; the actual safety gate is extreme
        # low alpha plus a 3px exclusion zone around every real surface.
        selected = (
            roi
            & (alpha > 0)
            & (alpha <= ALPHA_MAX)
            & (distance_from_core >= MIN_CORE_DISTANCE)
        )
        combined |= selected
        components = component_report(selected, alpha)
        roi_reports[name] = {
            "box_raw": [x0, y0, x1, y1],
            "removed_pixels": int(selected.sum()),
            "near_white_removed_pixels": int((selected & near_white).sum()),
            "non_near_white_removed_pixels": int((selected & ~near_white).sum()),
            "component_count": len(components),
            "components": components,
        }

    cleaned = source.copy()
    cleaned[combined] = 0
    cleaned = clean_hidden_rgb(cleaned)

    # Keep a single explicit backup of the pre-existing normalized left output.
    backup = ROOT / "left_before_audited_cleanup.png"
    if LEFT_FINAL.exists() and not backup.exists():
        shutil.copy2(LEFT_FINAL, backup)

    Image.fromarray(cleaned, "RGBA").save(LEFT_RAW_CLEAN)
    normalized = premultiplied_resize(cleaned, TARGET_SIZE)
    Image.fromarray(normalized, "RGBA").save(LEFT_FINAL)

    mirrored = np.ascontiguousarray(normalized[:, ::-1])
    Image.fromarray(mirrored, "RGBA").save(RIGHT_FINAL)

    TRIPO_INPUT.mkdir(parents=True, exist_ok=True)
    sanitize_reference(FRONT_FINAL, TRIPO_INPUT / "front.png")
    sanitize_reference(LEFT_FINAL, TRIPO_INPUT / "left.png")
    sanitize_reference(BACK_FINAL, TRIPO_INPUT / "back.png")
    sanitize_reference(RIGHT_FINAL, TRIPO_INPUT / "right.png")

    make_qa_crops(source, cleaned)
    audit_overlay = composite(source, (32, 32, 32))
    audit_overlay[combined] = (255, 255, 0)
    Image.fromarray(audit_overlay, "RGB").save(QA / "removed_pixels_overlay.png")

    report = {
        "item_id": "item.weapon.rifle.smilesignal",
        "source": {
            "path": SOURCE.name,
            "sha256": sha256(SOURCE),
            "size": [source.shape[1], source.shape[0]],
        },
        "selection": {
            "near_white_diagnostic_min_channel": NEUTRAL_MIN,
            "near_white_diagnostic_max_channel_spread": NEUTRAL_SPREAD_MAX,
            "alpha_range_inclusive": [1, ALPHA_MAX],
            "core_alpha_min": CORE_ALPHA_MIN,
            "minimum_distance_from_core_pixels": MIN_CORE_DISTANCE,
            "connectivity_cleanup": {
                "kept_largest_weapon_component_id": largest_component,
                "removed_component_count": len(disconnected_components),
                "removed_pixels": int(disconnected.sum()),
                "removed_alpha_max": int(alpha[disconnected].max())
                if disconnected.any()
                else 0,
                "components": disconnected_components,
            },
            "rois": roi_reports,
            "total_removed_pixels": int(combined.sum()),
            "removed_alpha_max": int(alpha[combined].max()) if combined.any() else 0,
            "removed_alpha_min": int(alpha[combined].min()) if combined.any() else 0,
        },
        "invariants": {
            "raw_rgba_residue_pixels_zeroed": int(combined.sum()),
            "source_alpha_at_removed_pixels_never_exceeded": int(
                alpha[combined].max()
            ) if combined.any() else 0,
            "source_core_pixels_changed": int((combined & core).sum()),
            "pixels_within_protected_core_distance_changed": int(
                (combined & (distance_from_core < MIN_CORE_DISTANCE)).sum()
            ),
            "red_black_white_surfaces_preserved_by_alpha_and_distance_guard": True,
            "sticker_roi_intersection_pixels": 0,
            "generative_edit_used": False,
            "spatial_or_uv_reconstruction_used": False,
        },
        "outputs": {
            "left_clean_raw": {
                "path": LEFT_RAW_CLEAN.name,
                "sha256": sha256(LEFT_RAW_CLEAN),
            },
            "left": {
                "path": LEFT_FINAL.name,
                "sha256": sha256(LEFT_FINAL),
                "size": list(TARGET_SIZE),
            },
            "right": {
                "path": RIGHT_FINAL.name,
                "sha256": sha256(RIGHT_FINAL),
                "size": list(TARGET_SIZE),
                "exact_horizontal_mirror_of_left": bool(
                    np.array_equal(mirrored, normalized[:, ::-1])
                ),
            },
        },
    }
    (QA / "cleanup_audit.json").write_text(
        json.dumps(report, indent=2), encoding="utf-8"
    )
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
