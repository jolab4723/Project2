from __future__ import annotations

import json
from pathlib import Path

import numpy as np
from PIL import Image


RUN = Path(__file__).resolve().parent
ITEM_ID = RUN.parent.name
TEXTURES = RUN / "Textures"


def main() -> None:
    orm_path = TEXTURES / f"{ITEM_ID}_ORM.png"
    orm = np.asarray(Image.open(orm_path).convert("RGBA"), dtype=np.uint8)
    occlusion = orm[:, :, 0]
    roughness = orm[:, :, 1]
    metallic = orm[:, :, 2]
    smoothness = 255 - roughness

    metallic_smoothness = np.empty_like(orm)
    metallic_smoothness[:, :, :3] = metallic[:, :, None]
    metallic_smoothness[:, :, 3] = smoothness
    occlusion_map = np.full_like(orm, 255)
    occlusion_map[:, :, 0] = occlusion
    mos = np.empty_like(orm)
    mos[:, :, 0] = metallic
    mos[:, :, 1] = occlusion
    mos[:, :, 2] = smoothness
    mos[:, :, 3] = 255

    outputs = {
        "metallic_smoothness": TEXTURES / f"{ITEM_ID}_MetallicSmoothness.png",
        "occlusion": TEXTURES / f"{ITEM_ID}_Occlusion.png",
        "mos": TEXTURES / f"{ITEM_ID}_MOS.png",
    }
    Image.fromarray(metallic_smoothness, mode="RGBA").save(outputs["metallic_smoothness"])
    Image.fromarray(occlusion_map, mode="RGBA").save(outputs["occlusion"])
    Image.fromarray(mos, mode="RGBA").save(outputs["mos"])
    report = {
        "source_orm": str(orm_path),
        "source_channels": {"R": "occlusion", "G": "roughness", "B": "metallic"},
        "metallic_smoothness": {"path": str(outputs["metallic_smoothness"]), "R_G_B": "metallic", "A": "1 - roughness"},
        "occlusion": {"path": str(outputs["occlusion"]), "R": "occlusion", "G_B_A": 255},
        "mos": {"path": str(outputs["mos"]), "R": "metallic", "G": "occlusion", "B": "smoothness", "A": 255},
        "size": [int(orm.shape[1]), int(orm.shape[0])],
    }
    (TEXTURES / "unity_texture_pack_report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
