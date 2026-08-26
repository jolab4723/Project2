from __future__ import annotations

import importlib.util
from pathlib import Path

from mathutils import Vector


THIS_FILE = Path(__file__).resolve()
ITEM_ROOT = THIS_FILE.parents[2]
WEAPONS_ROOT = THIS_FILE.parents[3]
COMMON_BUILD = WEAPONS_ROOT / "item.weapon.rifle.glassrail" / "Production" / "build_glassrail.py"

spec = importlib.util.spec_from_file_location("project2_weapon_build_common", COMMON_BUILD)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Unable to load build helper: {COMMON_BUILD}")
build = importlib.util.module_from_spec(spec)
spec.loader.exec_module(build)

build.ROOT = ITEM_ROOT
build.ITEM_ID = "item.weapon.grenadelauncher.pulsecask"
build.TASK_ID = "3e456e27-b34a-4117-a11a-ec5d07a17dc2"
build.RAW_GLB = ITEM_ROOT / "Tripo" / "Regenerated" / "Downloaded" / f"{build.ITEM_ID}_raw.glb"
build.PRODUCTION = ITEM_ROOT / "Production" / "Regenerated"
build.TEXTURES = build.PRODUCTION / "Textures"
build.QA = build.PRODUCTION / "QA" / "Final"
build.BLEND_PATH = build.PRODUCTION / f"{build.ITEM_ID}.blend"
build.FBX_PATH = build.PRODUCTION / f"{build.ITEM_ID}.fbx"
build.VALIDATION_PATH = build.PRODUCTION / "validation.json"
build.EMISSION_REPORT_PATH = build.QA / "emission_mask_report.json"
build.MODEL_SCALE = 0.78
build.TARGET_TRIANGLES = 150_000
build.RAW_RIGHT_GRIP = Vector((0.205, 0.0, -0.055))
build.RAW_MUZZLE = Vector((-0.5, 0.00004, 0.043612))
build.LEFT_GRIP_LOCAL = Vector((0.0, 0.0, 0.326))
build.ROOT_FINAL = build.raw_to_final(build.RAW_RIGHT_GRIP)

original_import_transform = build.import_transform
original_configure_material = build.configure_material


def import_transform():
    body, record = original_import_transform()
    body.name = "PulseCask_Body"
    body.data.name = "PulseCask_Body_Mesh"
    return body, record


def configure_material(body):
    material_record, texture_records, emission_report = original_configure_material(body)
    material = body.material_slots[0].material
    material.name = "M_PulseCask_PBR"
    material_record["name"] = material.name
    for image in build.bpy.data.images:
        if image.name == "GlassRail_EmissionMask":
            image.name = "PulseCask_EmissionMask"
    for record in texture_records:
        if record["path"].endswith("_Emission.png"):
            record["source_name"] = "PulseCask_EmissionMask"
    for node in material.node_tree.nodes:
        node.name = node.name.replace("GlassRail", "PulseCask")
    return material_record, texture_records, emission_report


build.import_transform = import_transform
build.configure_material = configure_material


if __name__ == "__main__":
    build.main()
