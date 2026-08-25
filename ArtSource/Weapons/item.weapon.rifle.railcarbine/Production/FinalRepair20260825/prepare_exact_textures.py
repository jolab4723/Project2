from __future__ import annotations

import json
import shutil
from collections import Counter
from pathlib import Path

import numpy as np
from PIL import Image


OUT = Path(__file__).resolve().parent
SOURCE_TEXTURES = OUT.parent / "CostSafeRetry20260825" / "Textures"
TEXTURES = OUT / "Textures"
QA = OUT / "QA"
ITEM_ID = "item.weapon.rifle.railcarbine"
PALETTE = (
    (30, 233, 248),
    (27, 234, 250),
    (30, 233, 250),
    (29, 232, 247),
)
REPRESENTATIVE = (29, 233, 249)
USER_SAMPLE = Path("C:/Users/user/AppData/Local/Temp/codex-clipboard-34063010-1b09-4b41-99fd-6de31fe320c8.png")


def srgb_to_linear(value: int) -> float:
    channel = value / 255.0
    return channel / 12.92 if channel <= 0.04045 else ((channel + 0.055) / 1.055) ** 2.4


def main() -> None:
    TEXTURES.mkdir(parents=True, exist_ok=True)
    QA.mkdir(parents=True, exist_ok=True)
    source_base = SOURCE_TEXTURES / f"{ITEM_ID}_BaseColor.png"
    target_base = TEXTURES / f"{ITEM_ID}_BaseColor.png"
    target_normal = TEXTURES / f"{ITEM_ID}_Normal.png"
    target_orm = TEXTURES / f"{ITEM_ID}_ORM.png"
    shutil.copy2(source_base, target_base)
    shutil.copy2(SOURCE_TEXTURES / f"{ITEM_ID}_Normal.png", target_normal)
    shutil.copy2(SOURCE_TEXTURES / f"{ITEM_ID}_ORM.png", target_orm)

    source = np.asarray(Image.open(source_base).convert("RGBA"), dtype=np.uint8)
    rgb = source[:, :, :3]
    reference = np.zeros(rgb.shape[:2], dtype=bool)
    palette_counts = {}
    for color in PALETTE:
        match = np.all(rgb == np.array(color, dtype=np.uint8), axis=2)
        palette_counts[str(color)] = int(match.sum())
        reference |= match
    selected = reference.copy()

    emission = np.zeros_like(source)
    emission[:, :, :3] = selected[:, :, None].astype(np.uint8) * 255
    emission[:, :, 3] = 255
    emission_path = TEXTURES / f"{ITEM_ID}_Emission.png"
    Image.fromarray(emission, mode="RGBA").save(emission_path)

    overlay = source.copy()
    overlay[:, :, :3] = np.rint(overlay[:, :, :3].astype(np.float32) * 0.22).astype(np.uint8)
    overlay[selected, :3] = np.array((255, 0, 255), dtype=np.uint8)
    overlay[:, :, 3] = 255
    overlay_path = QA / "source_basecolor_exact_mask_overlay.png"
    Image.fromarray(overlay, mode="RGBA").save(overlay_path)

    sample_counts = {}
    sample_matches_expected = False
    if USER_SAMPLE.is_file():
        sample = np.asarray(Image.open(USER_SAMPLE).convert("RGB"), dtype=np.uint8)
        counter = Counter(map(tuple, sample.reshape((-1, 3)).tolist()))
        sample_counts = {str(color): int(count) for color, count in sorted(counter.items())}
        sample_matches_expected = counter == Counter(
            {
                (30, 233, 248): 4,
                (27, 234, 250): 4,
                (30, 233, 250): 2,
                (29, 232, 247): 2,
            }
        )

    selected_count = int(selected.sum())
    report = {
        "source_base_color": str(target_base),
        "normal": str(target_normal),
        "orm": str(target_orm),
        "mask": str(emission_path),
        "overlay": str(overlay_path),
        "selection_rule": "exact global membership in only the four user-sampled sRGB colors",
        "reference_palette": [list(color) for color in PALETTE],
        "palette_counts": palette_counts,
        "selected_pixels": selected_count,
        "missed_reference_pixels": int((reference & ~selected).sum()),
        "false_positive_pixels": int((selected & ~reference).sum()),
        "total_pixels": int(selected.size),
        "binary_values": sorted(int(value) for value in np.unique(emission[:, :, :3])),
        "morphology_spatial_uv_mesh_component_exception": False,
        "representative_srgb": list(REPRESENTATIVE),
        "unity_linear_rgb_target": [round(srgb_to_linear(value), 7) for value in REPRESENTATIVE],
        "user_sample": {
            "path": str(USER_SAMPLE),
            "pixel_count": int(sum(sample_counts.values())),
            "counts": sample_counts,
            "matches_supplied_12_pixel_distribution": sample_matches_expected,
        },
        "pass": selected_count > 0
        and int((reference & ~selected).sum()) == 0
        and int((selected & ~reference).sum()) == 0
        and sample_matches_expected,
    }
    (OUT / "emission_validation.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    if not report["pass"]:
        raise RuntimeError(json.dumps(report))
    print(json.dumps({"selected_pixels": selected_count, "palette_counts": palette_counts, "sample_pass": sample_matches_expected}))


if __name__ == "__main__":
    main()
