from pathlib import Path


SOURCE = Path(__file__).resolve().parents[2] / "item.weapon.shotgun.thermobarrel" / "Tripo" / "h3_workflow.py"
namespace = {"__file__": str(Path(__file__).resolve()), "__name__": "item_weapon_rifle_antimatterlance_h3"}
code = SOURCE.read_text(encoding="utf-8").replace(
    'ITEM_ID = "item.weapon.shotgun.thermobarrel"',
    'ITEM_ID = "item.weapon.rifle.antimatterlance"',
)
exec(compile(code, str(Path(__file__).resolve()), "exec"), namespace)
namespace["main"]()
