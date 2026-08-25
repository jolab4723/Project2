from __future__ import annotations

import importlib.util
import json
from pathlib import Path


HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("rail_build", HERE / "build_railcarbine.py")
rail = importlib.util.module_from_spec(spec)
spec.loader.exec_module(rail)

rail.clear_scene()
body, _ = rail.import_and_transform()
rail.make_root(body)
rail.decimate(body, rail.TARGET_STAGE_1, "diagnostic_100k")
rail.decimate(body, rail.TARGET_STAGE_2, "diagnostic_60k")

records = []
for component in rail.connected_components(body):
    records.append({
        "vertices": component["vertices"],
        "min": rail.v3(component["min"]),
        "max": rail.v3(component["max"]),
        "center": rail.v3(component["center"]),
        "dimensions": rail.v3(component["dimensions"]),
    })

(rail.QA / "geometry_components_pre_repair.json").write_text(
    json.dumps({"components": records}, indent=2), encoding="utf-8"
)
