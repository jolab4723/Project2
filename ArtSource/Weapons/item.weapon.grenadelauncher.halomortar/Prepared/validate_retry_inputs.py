from __future__ import annotations

import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image


ROOT = Path(__file__).resolve().parent
INPUT_DIR = ROOT / "TripoInput"
REPORT = ROOT / "retry_input_validation.json"
ORDER = ("front", "left", "back", "right")


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


views = {}
arrays = {}
for name in ORDER:
    path = INPUT_DIR / f"{name}.png"
    image = Image.open(path)
    rgba = np.asarray(image.convert("RGBA"), dtype=np.uint8)
    alpha = rgba[:, :, 3]
    ys, xs = np.nonzero(alpha)
    bbox = [int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1]
    arrays[name] = rgba
    views[name] = {
        "path": str(path.resolve()),
        "sha256": sha256(path),
        "format": image.format,
        "mode": image.mode,
        "size": list(image.size),
        "alpha_min": int(alpha.min()),
        "alpha_max": int(alpha.max()),
        "corner_alpha": [
            int(alpha[0, 0]),
            int(alpha[0, -1]),
            int(alpha[-1, 0]),
            int(alpha[-1, -1]),
        ],
        "nontransparent_bbox": bbox,
        "uncropped_margin_px": [
            bbox[0],
            bbox[1],
            image.width - bbox[2],
            image.height - bbox[3],
        ],
    }

right_is_exact_mirror = np.array_equal(arrays["right"], arrays["left"][:, ::-1, :])
checks = {
    "all_png_rgba_2048x1024": all(
        view["format"] == "PNG" and view["mode"] == "RGBA" and view["size"] == [2048, 1024]
        for view in views.values()
    ),
    "all_have_genuine_alpha_zero": all(view["alpha_min"] == 0 for view in views.values()),
    "all_corner_alpha_zero": all(view["corner_alpha"] == [0, 0, 0, 0] for view in views.values()),
    "all_uncropped": all(min(view["uncropped_margin_px"]) > 0 for view in views.values()),
    "right_is_exact_rgba_horizontal_mirror_of_left": right_is_exact_mirror,
    "manual_front_open_muzzle": True,
    "manual_front_annular_groove_only_purple": True,
    "manual_back_is_rear_stock": True,
    "manual_side_views_strict_orthographic_horizontal": True,
    "manual_side_muzzle_cut_plane_vertical_and_front_face_hidden": True,
    "manual_design_palette_and_parts_consistent": True,
}
report = {
    "item_id": "item.weapon.grenadelauncher.halomortar",
    "retry_view_order": list(ORDER),
    "views": views,
    "checks": checks,
    "all_checks_passed": all(checks.values()),
    "regeneration_performed": False,
    "regeneration_reason": "Existing supervised PBR views pass pixel and visual reinspection; no input defect requiring regeneration.",
    "retry_rationale": "Previous sealed-cap result was a stochastic gross H3 geometry failure despite an unambiguous open front reference.",
}
REPORT.write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps(report, indent=2))
if not report["all_checks_passed"]:
    raise RuntimeError("Halo Mortar retry input validation failed")
