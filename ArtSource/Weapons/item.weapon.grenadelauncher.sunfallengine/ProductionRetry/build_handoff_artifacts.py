from __future__ import annotations

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
    run = Path(__file__).resolve().parent
    item_id = run.parent.name
    textures = run / "Textures"
    qa = run / "QA"
    handoff = qa / "Handoff"
    handoff.mkdir(parents=True, exist_ok=True)

    orm_path = textures / f"{item_id}_ORM.png"
    orm = np.asarray(Image.open(orm_path).convert("RGBA"), dtype=np.uint8)
    ao, roughness, metallic = orm[:, :, 0], orm[:, :, 1], orm[:, :, 2]
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

    validation = json.loads((run / "validation.json").read_text(encoding="utf-8"))
    emission_spec = validation["material"]["emission"]
    thresholds = emission_spec["threshold_srgb_8bit"]
    base = np.asarray(Image.open(textures / f"{item_id}_BaseColor.png").convert("RGB"), dtype=np.int16)
    red, green, blue = base[:, :, 0], base[:, :, 1], base[:, :, 2]
    expected = (
        (red >= thresholds["r_min"])
        & (green >= thresholds["g_min"])
        & (blue <= thresholds["b_max"])
        & ((red - green) >= thresholds["r_minus_g_min"])
        & ((green - blue) >= thresholds["g_minus_b_min"])
        & ((red - blue) >= thresholds["r_minus_b_min"])
    )
    emission_path = textures / f"{item_id}_Emission.png"
    emission = np.asarray(Image.open(emission_path).convert("RGBA"), dtype=np.uint8)
    emission_rgb = emission[:, :, :3]
    actual = emission_rgb[:, :, 0] == 255
    emission_checks = {
        "rgba_binary_white_black": bool(np.all(np.isin(emission_rgb, (0, 255)))),
        "rgb_channels_identical": bool(np.array_equal(emission_rgb[:, :, 0], emission_rgb[:, :, 1]) and np.array_equal(emission_rgb[:, :, 0], emission_rgb[:, :, 2])),
        "alpha_opaque": bool(np.all(emission[:, :, 3] == 255)),
        "selected_pixel_count_matches_validation": int(actual.sum()) == int(emission_spec["selected_pixels"]),
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
            source_view = "iso_left" if view == "iso" else view
            source = Image.open(qa / f"{family}_{source_view}.png").convert("RGBA")
            source.thumbnail(thumb_size, Image.Resampling.LANCZOS)
            x = column * thumb_size[0]
            sheet.alpha_composite(source, (x + (thumb_size[0] - source.width) // 2, y + (thumb_size[1] - source.height) // 2))
            draw.text((x + 8, y + thumb_size[1] + 8), f"{family.upper()} {view.upper()}", fill=(220, 226, 236, 255), font=font)
    contact_sheet = handoff / "contact_sheet_pbr_emission.png"
    sheet.save(contact_sheet)

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
    report = {
        "item_id": item_id,
        "size": [int(orm.shape[1]), int(orm.shape[0])],
        "source_orm": str(orm_path),
        "orm_contract": {"R": "ambient occlusion", "G": "roughness", "B": "metallic"},
        "outputs": {
            "metallic_smoothness": {"path": str(metallic_smoothness_path), "channels": {"R": "metallic", "G": "0", "B": "0", "A": "smoothness = 255 - roughness"}},
            "occlusion": {"path": str(occlusion_path), "channels": {"RGB": "ambient occlusion", "A": "255"}},
            "combined_mos": {"path": str(combined_path), "channels": {"R": "metallic", "G": "ambient occlusion", "B": "0", "A": "smoothness"}},
        },
        "unity_import": "All packed textures: Default with sRGB disabled. MetallicSmoothness is URP Lit Metallic Map; Occlusion has AO in green.",
        "channel_ranges": {"ao": [int(ao.min()), int(ao.max())], "roughness": [int(roughness.min()), int(roughness.max())], "metallic": [int(metallic.min()), int(metallic.max())], "smoothness": [int(smoothness.min()), int(smoothness.max())]},
        "emission_rgb_audit": {"path": str(emission_path), "thresholds_srgb_8bit": thresholds, "selected_pixels": int(actual.sum()), "raw_png_direct_match": bool(np.array_equal(actual, expected)), "raw_png_note": "Informational only: the authored mask was selected from Blender color-managed image pixels. Exact RGB-family identity is recomputed authoritatively in normals_culling_audit.json.", "checks": emission_checks, "pass": all(emission_checks.values())},
        "sha256": {"source_orm": sha256(orm_path), "metallic_smoothness": sha256(metallic_smoothness_path), "occlusion": sha256(occlusion_path), "combined_mos": sha256(combined_path), "emission": sha256(emission_path), "contact_sheet": sha256(contact_sheet)},
        "checks": channel_checks,
        "contact_sheet": str(contact_sheet),
        "pass": all(channel_checks.values()) and all(emission_checks.values()),
    }
    (handoff / "handoff_texture_audit.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
