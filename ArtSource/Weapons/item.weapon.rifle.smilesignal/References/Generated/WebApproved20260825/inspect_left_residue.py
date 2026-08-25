from __future__ import annotations

import json
from pathlib import Path

import cv2
import numpy as np
from PIL import Image


ROOT = Path(__file__).resolve().parent
SOURCE = ROOT / "left_clean_web_raw.png"
QA = ROOT / "QA_LeftCleanup"

# Raw 1774x887 source coordinates. These boxes intentionally cover only the
# two user-identified negative-space areas and their immediate antialiased rim.
ROIS = {
    "upper_open_gap": (220, 175, 700, 350),
    "trigger_opening": (250, 430, 850, 790),
}


def composite(rgba: np.ndarray, background: tuple[int, int, int]) -> np.ndarray:
    rgb = rgba[:, :, :3].astype(np.float32)
    alpha = rgba[:, :, 3:4].astype(np.float32) / 255.0
    bg = np.array(background, dtype=np.float32).reshape(1, 1, 3)
    return np.clip(rgb * alpha + bg * (1.0 - alpha), 0, 255).astype(np.uint8)


def main() -> None:
    QA.mkdir(parents=True, exist_ok=True)
    rgba = np.array(Image.open(SOURCE).convert("RGBA"))
    alpha = rgba[:, :, 3]
    report: dict[str, object] = {
        "source": SOURCE.name,
        "size": [rgba.shape[1], rgba.shape[0]],
        "mode": "RGBA",
        "alpha_extrema": [int(alpha.min()), int(alpha.max())],
        "rois": {},
    }

    for name, (x0, y0, x1, y1) in ROIS.items():
        crop = rgba[y0:y1, x0:x1]
        enlarged = cv2.resize(crop, None, fx=3, fy=3, interpolation=cv2.INTER_NEAREST)
        magenta = composite(enlarged, (255, 0, 255))
        green = composite(enlarged, (0, 150, 40))
        alpha_rgb = np.repeat(enlarged[:, :, 3:4], 3, axis=2)
        strip = np.concatenate([magenta, green, alpha_rgb], axis=1)
        Image.fromarray(strip, "RGB").save(QA / f"{name}_raw_x3.png")

        roi_alpha = crop[:, :, 3]
        visible = roi_alpha > 0
        opaque = roi_alpha == 255
        near_white = (
            (crop[:, :, :3].min(axis=2) >= 225)
            & ((crop[:, :, :3].max(axis=2) - crop[:, :, :3].min(axis=2)) <= 24)
        )
        labels_count, labels, stats, _ = cv2.connectedComponentsWithStats(
            (visible & near_white).astype(np.uint8), 8
        )
        components = []
        for index in range(1, labels_count):
            area = int(stats[index, cv2.CC_STAT_AREA])
            if area < 2:
                continue
            x = int(stats[index, cv2.CC_STAT_LEFT]) + x0
            y = int(stats[index, cv2.CC_STAT_TOP]) + y0
            w = int(stats[index, cv2.CC_STAT_WIDTH])
            h = int(stats[index, cv2.CC_STAT_HEIGHT])
            values = roi_alpha[labels == index]
            components.append(
                {
                    "area": area,
                    "bbox_raw": [x, y, w, h],
                    "alpha_min": int(values.min()),
                    "alpha_max": int(values.max()),
                }
            )
        components.sort(key=lambda item: item["area"], reverse=True)
        report["rois"][name] = {
            "box_raw": [x0, y0, x1, y1],
            "visible_pixels": int(visible.sum()),
            "opaque_pixels": int(opaque.sum()),
            "near_white_visible_pixels": int((visible & near_white).sum()),
            "near_white_components_top20": components[:20],
        }

    (QA / "analysis.json").write_text(
        json.dumps(report, indent=2), encoding="utf-8"
    )
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
