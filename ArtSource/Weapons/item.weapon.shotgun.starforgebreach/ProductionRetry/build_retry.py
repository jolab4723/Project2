from pathlib import Path

weapon_root = Path(__file__).resolve().parent.parent
shared = weapon_root.parent / "item.weapon.grenadelauncher.sunfallengine" / "Production" / "build_legendary_weapon.py"
source = shared.read_text(encoding="utf-8")
replacements = {
    '"task_id": "3e786fff-baaf-467d-b6fb-a138a3ce26da"': '"task_id": "216c0d80-4f54-41ca-a29d-86587428d01a"',
    '"task_id": "80f712ad-5e7c-410e-828b-2aa79f38aaf0"': '"task_id": "d6029c51-c7a7-4f40-987f-1062b5fc1e2c"',
    '"root_raw": (-0.1489425982, 0.0, -0.0970)': '"root_raw": (0.1489425982, 0.0, -0.0970)',
    '"left_raw": (0.2435045317, 0.0, 0.0260)': '"left_raw": (-0.2435045317, 0.0, 0.0260)',
    '"muzzle_raw": (0.5000, 0.0, 0.0100)': '"muzzle_raw": (-0.5000, 0.0, 0.0100)',
    '"root_raw": (-0.1716088328, 0.0, -0.0730)': '"root_raw": (0.1716088328, 0.0, -0.0730)',
    '"left_raw": (0.2384858044, 0.0, 0.0500)': '"left_raw": (-0.2384858044, 0.0, 0.0500)',
    '"muzzle_raw": (0.5000, 0.0, 0.0180)': '"muzzle_raw": (-0.5000, 0.0, 0.0180)',
    'return Vector((relative.y, relative.z, relative.x)) * scale': 'return Vector((relative.y, relative.z, -relative.x)) * scale',
    'raw (X,Y,Z) -> final (Y,Z,X)': 'raw (X,Y,Z) -> final (Y,Z,-X)',
    '"raw_muzzle": "+X"': '"raw_muzzle": "-X"',
    'root / "Tripo" / "Downloaded" / "model.glb"': 'root / "Tripo" / "RetryFrontBackSwap" / "Downloaded" / "model.glb"',
    'output = root / "Production"': 'output = root / "ProductionRetry"',
}
for old, new in replacements.items():
    if old not in source:
        raise RuntimeError(f"Missing expected source fragment: {old}")
    source = source.replace(old, new)
exec(compile(source, str(shared), "exec"), {"__file__": str(shared), "__name__": "__main__"})
