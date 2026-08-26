from __future__ import annotations

import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "Production" / "artifact_manifest.json"
ITEM_ID = "item.weapon.rifle.smilesignal"

paths = [
    *(ROOT / "Prepared" / "TripoInput" / f"{view}.png" for view in ("front", "left", "back", "right")),
    ROOT / "Prepared" / "QA" / "four_view_pixel_gate.json",
    ROOT / "Tripo" / "RetryAfterExpired20260825" / "h3_task.json",
    ROOT / "Tripo" / "RetryAfterExpired20260825" / "h3_task_result.json",
    ROOT / "Tripo" / "RetryAfterExpired20260825" / "Downloaded" / f"{ITEM_ID}_raw.glb",
    ROOT / "Tripo" / "RetryAfterExpired20260825" / "RawQA" / "raw_gate.json",
    ROOT / "Production" / f"{ITEM_ID}.blend",
    ROOT / "Production" / f"{ITEM_ID}.fbx",
    ROOT / "Production" / "validation.json",
    ROOT / "Production" / "decimation_selection.json",
    ROOT / "Production" / "QA" / "emission_mask_report.json",
    ROOT / "Production" / "QA" / "Reimport" / "reimport_validation.json",
    *(ROOT / "Production" / "Textures" / f"{ITEM_ID}_{suffix}.png" for suffix in ("BaseColor", "Normal", "ORM", "Occlusion", "MetallicSmoothness", "Emission")),
]

missing = [str(path) for path in paths if not path.is_file()]
if missing:
    raise FileNotFoundError(json.dumps(missing, ensure_ascii=False, indent=2))

records = {}
for path in paths:
    relative = str(path.relative_to(ROOT)).replace("\\", "/")
    records[relative] = {
        "bytes": path.stat().st_size,
        "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
    }

qa_files = list((ROOT / "Production" / "QA").rglob("*.png"))
manifest = {
    "item_id": ITEM_ID,
    "generated_at": datetime.now(timezone.utc).isoformat(),
    "status": "PASS",
    "task_id": "1fa3dd59-189c-4160-b050-36cd85e529d4",
    "consumed_credit": 30,
    "selected_triangles": 249999,
    "production_qa_png_count": len(qa_files),
    "files": records,
}
OUTPUT.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(manifest, ensure_ascii=False, indent=2))
