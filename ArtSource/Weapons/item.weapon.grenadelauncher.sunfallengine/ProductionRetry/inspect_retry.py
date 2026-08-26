from pathlib import Path

weapon_root = Path(__file__).resolve().parent.parent
source = (weapon_root / "analyze_raw.py").read_text(encoding="utf-8")
source = source.replace('ROOT / "Tripo" / "Downloaded" / "model.glb"', 'ROOT / "Tripo" / "RetryFrontBackSwap" / "Downloaded" / "model.glb"')
source = source.replace('OUT = ROOT / "Production"', 'OUT = ROOT / "ProductionRetry"')
exec(compile(source, str(weapon_root / "analyze_raw.py"), "exec"), {"__file__": str(weapon_root / "analyze_raw.py"), "__name__": "__main__"})
