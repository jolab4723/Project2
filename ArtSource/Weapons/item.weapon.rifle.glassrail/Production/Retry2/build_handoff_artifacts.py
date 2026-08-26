from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont


VIEWS = ("front", "back", "left", "right", "iso")


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--item-root", type=Path, required=True)
    parser.add_argument("--run", required=True)
    args = parser.parse_args()

    item_root = args.item_root.resolve()
    item_id = item_root.name
    production = item_root / "Production" / args.run
    textures = production / "Textures"
    qa_final = production / "QA" / "Final"
    qa_handoff = production / "QA" / "Handoff"
    qa_handoff.mkdir(parents=True, exist_ok=True)

    orm_path = textures / f"{item_id}_ORM.png"
    orm = np.asarray(Image.open(orm_path).convert("RGBA"), dtype=np.uint8)
    ao = orm[:, :, 0]
    roughness = orm[:, :, 1]
    metallic = orm[:, :, 2]
    smoothness = 255 - roughness

    metallic_smoothness = np.zeros_like(orm)
    metallic_smoothness[:, :, 0] = metallic
    metallic_smoothness[:, :, 3] = smoothness
    metallic_smoothness_path = textures / f"{item_id}_MetallicSmoothness.png"
    Image.fromarray(metallic_smoothness, mode="RGBA").save(metallic_smoothness_path)

    occlusion = np.empty_like(orm)
    occlusion[:, :, 0] = ao
    occlusion[:, :, 1] = ao
    occlusion[:, :, 2] = ao
    occlusion[:, :, 3] = 255
    occlusion_path = textures / f"{item_id}_Occlusion.png"
    Image.fromarray(occlusion, mode="RGBA").save(occlusion_path)

    combined = np.zeros_like(orm)
    combined[:, :, 0] = metallic
    combined[:, :, 1] = ao
    combined[:, :, 3] = smoothness
    combined_path = textures / f"{item_id}_UnityMask_MOS.png"
    Image.fromarray(combined, mode="RGBA").save(combined_path)

    channel_checks = {
        "metallic_smoothness_r_equals_orm_b": bool(np.array_equal(metallic_smoothness[:, :, 0], metallic)),
        "metallic_smoothness_gb_zero": bool(np.all(metallic_smoothness[:, :, 1:3] == 0)),
        "metallic_smoothness_a_equals_one_minus_roughness": bool(np.array_equal(metallic_smoothness[:, :, 3], smoothness)),
        "occlusion_rgb_equals_orm_r": bool(all(np.array_equal(occlusion[:, :, channel], ao) for channel in range(3))),
        "occlusion_alpha_opaque": bool(np.all(occlusion[:, :, 3] == 255)),
        "combined_r_equals_metallic": bool(np.array_equal(combined[:, :, 0], metallic)),
        "combined_g_equals_ao": bool(np.array_equal(combined[:, :, 1], ao)),
        "combined_b_zero": bool(np.all(combined[:, :, 2] == 0)),
        "combined_a_equals_smoothness": bool(np.array_equal(combined[:, :, 3], smoothness)),
    }

    thumb_size = (360, 210)
    label_height = 28
    title_height = 38
    sheet = Image.new("RGBA", (thumb_size[0] * len(VIEWS), title_height + 2 * (thumb_size[1] + label_height)), (8, 10, 14, 255))
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.load_default()
    draw.text((12, 12), f"{item_id} - final handoff QA", fill=(235, 240, 248, 255), font=font)
    for row, family in enumerate(("pbr", "emission")):
        y = title_height + row * (thumb_size[1] + label_height)
        for column, view in enumerate(VIEWS):
            source = Image.open(qa_final / f"{family}_{view}.png").convert("RGBA")
            source.thumbnail(thumb_size, Image.Resampling.LANCZOS)
            x = column * thumb_size[0]
            paste_x = x + (thumb_size[0] - source.width) // 2
            paste_y = y + (thumb_size[1] - source.height) // 2
            sheet.alpha_composite(source, (paste_x, paste_y))
            draw.text((x + 8, y + thumb_size[1] + 8), f"{family.upper()} {view.upper()}", fill=(220, 226, 236, 255), font=font)
    contact_sheet = qa_handoff / "contact_sheet_pbr_emission.png"
    sheet.save(contact_sheet)

    report = {
        "item_id": item_id,
        "source_orm": str(orm_path),
        "orm_contract": {"R": "ambient occlusion", "G": "roughness", "B": "metallic"},
        "metallic_smoothness": {
            "path": str(metallic_smoothness_path),
            "channels": {"R": "metallic", "G": "0", "B": "0", "A": "smoothness = 255 - roughness"},
            "unity_import": "Default texture, sRGB disabled; assign to URP Lit Metallic Map"
        },
        "occlusion": {
            "path": str(occlusion_path),
            "channels": {"R": "ambient occlusion", "G": "ambient occlusion", "B": "ambient occlusion", "A": "255"},
            "unity_import": "Default texture, sRGB disabled; assign to URP Lit Occlusion Map (green channel is populated)"
        },
        "combined_mos": {
            "path": str(combined_path),
            "channels": {"R": "metallic", "G": "ambient occlusion", "B": "0", "A": "smoothness"},
            "unity_import": "Optional custom packed mask; sRGB disabled"
        },
        "size": [int(orm.shape[1]), int(orm.shape[0])],
        "channel_ranges": {
            "ao": [int(ao.min()), int(ao.max())],
            "roughness": [int(roughness.min()), int(roughness.max())],
            "metallic": [int(metallic.min()), int(metallic.max())],
            "smoothness": [int(smoothness.min()), int(smoothness.max())]
        },
        "sha256": {
            "source_orm": sha256(orm_path),
            "metallic_smoothness": sha256(metallic_smoothness_path),
            "occlusion": sha256(occlusion_path),
            "combined_mos": sha256(combined_path),
            "contact_sheet": sha256(contact_sheet)
        },
        "checks": channel_checks,
        "pass": all(channel_checks.values()),
        "contact_sheet": str(contact_sheet)
    }
    (qa_handoff / "packed_texture_report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
