from __future__ import annotations

import importlib.util
from pathlib import Path


WEAPON_ROOT = Path(__file__).resolve().parents[2]
VARIANT_ROOT = Path(__file__).resolve().parent
SOURCE = (
    WEAPON_ROOT.parent
    / "item.weapon.grenadelauncher.worldender"
    / "Regeneration_20260826"
    / "Subculture"
    / "render_and_probe_raw.py"
)


def main() -> None:
    spec = importlib.util.spec_from_file_location("project2_raw_probe", SOURCE)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load {SOURCE}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    module.ROOT = VARIANT_ROOT
    module.GLB = (
        VARIANT_ROOT
        / "Tripo"
        / "Downloaded"
        / "item.weapon.rifle.phaseorchid_raw.glb"
    )
    module.OUT = VARIANT_ROOT / "QA" / "Raw"
    module.main()


if __name__ == "__main__":
    main()
