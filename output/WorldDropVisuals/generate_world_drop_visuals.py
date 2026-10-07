from __future__ import annotations

import argparse
import importlib
import sys
from pathlib import Path

sys.dont_write_bytecode = True


ROOT = Path(__file__).resolve().parent
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from world_drop_common import (  # noqa: E402
    GENERATED_DIR,
    export_asset,
    make_material,
    reset_scene,
    validate_contract,
    write_json,
)


ASSETS = {
    "WorldDrop_FighterGreatsword": ("models.fighter_greatsword", "neutral"),
    "WorldDrop_FighterBlunt": ("models.fighter_blunt", "neutral"),
    "WorldDrop_FighterAxe": ("models.fighter_axe", "neutral"),
    "WorldDrop_GunnerRifle": ("models.gunner_rifle", "neutral"),
    "WorldDrop_GunnerShotgun": ("models.gunner_shotgun", "neutral"),
    "WorldDrop_GunnerGrenadeLauncher": ("models.gunner_grenade_launcher", "neutral"),
    "WorldDrop_ArmorHelmet": ("models.armor_helmet", "neutral"),
    "WorldDrop_Armor": ("models.armor", "neutral"),
    "WorldDrop_ArmorBoots": ("models.armor_boots", "neutral"),
    "WorldDrop_Potion": ("models.potion", "potion"),
    "WorldDrop_Relic": ("models.relic", "relic"),
}

PALETTE = {
    "neutral": (0.82, 0.82, 0.82, 1.0),
    "potion": (0.82, 0.82, 0.82, 1.0),
    "relic": (0.82, 0.82, 0.82, 1.0),
}


def parse_args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--asset", choices=list(ASSETS), action="append")
    return parser.parse_args(argv)


def generate(asset_name: str) -> dict[str, object]:
    module_name, palette_name = ASSETS[asset_name]
    reset_scene()
    material = make_material("M_WorldDrop_White", PALETTE[palette_name])
    model_module = importlib.import_module(module_name)
    model = model_module.build(material)
    if model.name != asset_name:
        raise RuntimeError(f"Module {module_name} returned {model.name}, expected {asset_name}")
    metrics = export_asset(model, asset_name)
    validate_contract(metrics)
    return metrics


def main() -> None:
    args = parse_args()
    selected = args.asset or list(ASSETS)
    metrics = [generate(asset_name) for asset_name in selected]
    for item in metrics:
        write_json(GENERATED_DIR / "metrics" / f"{item['name']}.json", item)
        print(
            f"WORLD_DROP_OK {item['name']} triangles={item['triangles']} "
            f"dimensions={item['dimensions_m']}"
        )
    if args.asset is None:
        write_json(GENERATED_DIR / "authored_metrics.json", metrics)


if __name__ == "__main__":
    main()
