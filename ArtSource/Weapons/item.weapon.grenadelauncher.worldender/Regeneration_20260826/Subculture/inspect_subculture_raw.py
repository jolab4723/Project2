from __future__ import annotations

import importlib.util
import os
from pathlib import Path


WEAPON_ROOT = Path(__file__).resolve().parents[2]
VARIANT_ROOT = Path(__file__).resolve().parent
SOURCE = WEAPON_ROOT / "inspect_raw_blender.py"
ATTEMPT = os.environ.get("WORLDENDER_H3_ATTEMPT", "1").strip()
TRIPO_FOLDER = "Tripo" if ATTEMPT == "1" else "TripoAttempt2"
QA_FOLDER = "Raw" if ATTEMPT == "1" else "RawAttempt2"


def main() -> None:
    spec = importlib.util.spec_from_file_location("worldender_raw_inspection", SOURCE)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load {SOURCE}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    module.ROOT = VARIANT_ROOT
    module.RAW = (
        VARIANT_ROOT
        / TRIPO_FOLDER
        / "Downloaded"
        / "item.weapon.grenadelauncher.worldender_raw.glb"
    )
    module.OUT = VARIANT_ROOT / "QA" / QA_FOLDER / "raw_inspection.json"
    module.EXTRACTED = VARIANT_ROOT / "QA" / QA_FOLDER / "Extracted"
    module.main()


if __name__ == "__main__":
    main()
