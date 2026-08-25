from __future__ import annotations

import importlib.util
from pathlib import Path

from mathutils import Vector


THIS_FILE = Path(__file__).resolve()
ITEM_ROOT = THIS_FILE.parents[2]
COMMON_BUILD = ITEM_ROOT / "Production" / "build_glassrail.py"

spec = importlib.util.spec_from_file_location("project2_glassrail_build_retry2", COMMON_BUILD)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Unable to load build helper: {COMMON_BUILD}")
build = importlib.util.module_from_spec(spec)
spec.loader.exec_module(build)

build.ROOT = ITEM_ROOT
build.ITEM_ID = "item.weapon.rifle.glassrail"
build.TASK_ID = "3821ea37-7f12-41c1-a66a-78d466f2687c"
build.RAW_GLB = ITEM_ROOT / "Tripo" / "Retry2" / "Downloaded" / f"{build.ITEM_ID}_raw.glb"
build.PRODUCTION = ITEM_ROOT / "Production" / "Retry2"
build.TEXTURES = build.PRODUCTION / "Textures"
build.QA = build.PRODUCTION / "QA" / "Final"
build.BLEND_PATH = build.PRODUCTION / f"{build.ITEM_ID}.blend"
build.FBX_PATH = build.PRODUCTION / f"{build.ITEM_ID}.fbx"
build.VALIDATION_PATH = build.PRODUCTION / "validation.json"
build.EMISSION_REPORT_PATH = build.QA / "emission_mask_report.json"
build.MODEL_SCALE = 0.78
build.TARGET_TRIANGLES = 150_000
build.RAW_RIGHT_GRIP = Vector((0.205, 0.0, -0.055))
build.RAW_MUZZLE = Vector((-0.5, -0.000099, 0.056411))
build.LEFT_GRIP_LOCAL = Vector((0.0, 0.0, 0.326))
build.ROOT_FINAL = build.raw_to_final(build.RAW_RIGHT_GRIP)


if __name__ == "__main__":
    build.main()
