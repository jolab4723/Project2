from __future__ import annotations

import hashlib
import json
import shutil
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parent
SOURCE_DIR = ROOT / "References" / "SourceGenerated"
OUTPUT_DIR = ROOT / "References"
TARGET_SIZE = (2048, 1024)
SOURCES = {
    "front": Path(r"C:\Users\user\.codex\generated_images\01a0315e-e875-7771-bd6d-b4da64ead1b7\exec-ab38e6a8-20cf-4d07-b848-cb1264b53bf7.png"),
    "left": Path(r"C:\Users\user\.codex\generated_images\01a0315e-e875-7771-bd6d-b4da64ead1b7\exec-12227029-b791-4abc-ba3b-58c552e37e2d.png"),
    "back": Path(r"C:\Users\user\.codex\generated_images\01a0315e-e875-7771-bd6d-b4da64ead1b7\exec-c5f5a67b-d768-4c82-a6ed-2c918db5c702.png"),
    "right": Path(r"C:\Users\user\.codex\generated_images\01a0315e-e875-7771-bd6d-b4da64ead1b7\exec-ba232601-77e5-4f0d-a514-71263869d904.png"),
}


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main() -> None:
    SOURCE_DIR.mkdir(parents=True, exist_ok=True)
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    records = {}
    for view, generated_path in SOURCES.items():
        if not generated_path.is_file():
            raise FileNotFoundError(generated_path)
        archived_path = SOURCE_DIR / f"{view}.png"
        shutil.copy2(generated_path, archived_path)
        with Image.open(archived_path) as source:
            rgba = source.convert("RGBA")
            if rgba.width * 2 != rgba.height * 4:
                raise ValueError(f"{view}: expected exact 2:1 source, got {rgba.size}")
            if rgba.getchannel("A").getextrema() != (0, 255):
                raise ValueError(f"{view}: source lacks genuine transparent and opaque pixels")
            normalized = rgba.resize(TARGET_SIZE, Image.Resampling.LANCZOS)
            output_path = OUTPUT_DIR / f"{view}.png"
            normalized.save(output_path, "PNG")
        with Image.open(output_path) as final:
            if final.size != TARGET_SIZE or final.mode != "RGBA":
                raise ValueError(f"{view}: invalid normalized image {final.size} {final.mode}")
            if final.getchannel("A").getextrema() != (0, 255):
                raise ValueError(f"{view}: normalized image lacks genuine alpha")
        records[view] = {
            "generated_source": str(generated_path),
            "archived_source": str(archived_path.relative_to(ROOT)),
            "source_sha256": sha256(archived_path),
            "final_path": str(output_path.relative_to(ROOT)),
            "final_sha256": sha256(output_path),
            "final_size": list(TARGET_SIZE),
            "mode": "RGBA",
            "normalization": "uniform 2:1 resize; no crop or aspect stretch",
        }
    manifest = {
        "item_id": ROOT.name,
        "view_order": ["front", "left", "back", "right"],
        "records": records,
    }
    (OUTPUT_DIR / "reference_manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8"
    )


if __name__ == "__main__":
    main()
