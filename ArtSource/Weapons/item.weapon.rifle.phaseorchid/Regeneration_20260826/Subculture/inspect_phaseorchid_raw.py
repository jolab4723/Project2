from __future__ import annotations

import importlib.util
from pathlib import Path


WEAPON_ROOT = Path(__file__).resolve().parents[2]
VARIANT_ROOT = Path(__file__).resolve().parent
SOURCE = (
    WEAPON_ROOT.parent
    / "item.weapon.grenadelauncher.worldender"
    / "inspect_raw_blender.py"
)


def main() -> None:
    spec = importlib.util.spec_from_file_location("project2_raw_inspection", SOURCE)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load {SOURCE}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    module.ROOT = VARIANT_ROOT
    module.RAW = (
        VARIANT_ROOT
        / "Tripo"
        / "Downloaded"
        / "item.weapon.rifle.phaseorchid_raw.glb"
    )
    module.OUT = VARIANT_ROOT / "QA" / "Raw" / "raw_inspection.json"
    module.EXTRACTED = VARIANT_ROOT / "QA" / "Raw" / "Extracted"
    module.main()


if __name__ == "__main__":
    main()
