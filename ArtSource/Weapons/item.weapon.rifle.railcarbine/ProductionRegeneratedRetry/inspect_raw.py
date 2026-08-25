from pathlib import Path


current = Path(__file__).resolve()
template = current.parents[1] / "ProductionRegenerated" / "inspect_raw.py"
source = template.read_text(encoding="utf-8")
source = source.replace(
    'GLB = ROOT / "Tripo" / "Downloaded" / "item.weapon.rifle.railcarbine_raw.glb"',
    'GLB = ROOT / "Tripo" / "RetryFrontBackSwap" / "Downloaded" / "model.glb"',
)
source = source.replace(
    '73ed8f0b-00e9-415f-81be-36baa9bdd9d4',
    'f7256399-6bda-46d3-bc68-730b021391b8',
)
exec(compile(source, str(template), "exec"), {"__file__": str(current), "__name__": "__main__"})
