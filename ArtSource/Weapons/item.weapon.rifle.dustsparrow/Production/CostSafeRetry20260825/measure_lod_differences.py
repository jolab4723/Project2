import json
from pathlib import Path

import numpy as np
from PIL import Image


root = Path(__file__).resolve().parent / "QA" / "LODComparison"
labels = ["450k", "300k", "150k", "100k"]
views = ["left_full", "right_full", "muzzle_front_close", "grip_left_close"]
records = {}
for label in labels:
    view_records = {}
    for view in views:
        reference = np.asarray(Image.open(root / "raw" / f"{view}.png").convert("RGBA"), dtype=np.int16)
        candidate = np.asarray(Image.open(root / label / f"{view}.png").convert("RGBA"), dtype=np.int16)
        reference_mask = reference[:, :, 3] > 0
        candidate_mask = candidate[:, :, 3] > 0
        union = reference_mask | candidate_mask
        intersection = reference_mask & candidate_mask
        silhouette_iou = float(intersection.sum() / max(1, union.sum()))
        silhouette_xor = float((reference_mask ^ candidate_mask).sum() / max(1, union.sum()))
        rgb_mae = float(np.abs(reference[:, :, :3] - candidate[:, :, :3])[intersection].mean()) if intersection.any() else 0.0
        view_records[view] = {
            "silhouette_iou": round(silhouette_iou, 8),
            "silhouette_xor_fraction": round(silhouette_xor, 8),
            "rgb_mae_8bit": round(rgb_mae, 5),
        }
    records[label] = view_records

(root / "lod_difference_metrics.json").write_text(json.dumps(records, indent=2), encoding="utf-8")
print(json.dumps(records, indent=2))
