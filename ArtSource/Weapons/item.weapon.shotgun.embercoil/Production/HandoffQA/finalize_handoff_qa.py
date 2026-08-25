from __future__ import annotations

import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont


ITEM_ID = "item.weapon.shotgun.embercoil"
QA_ROOT = Path(__file__).resolve().parent
PRODUCTION_ROOT = QA_ROOT.parent
TEXTURE_REPORT_PATH = QA_ROOT / "texture_and_emission_validation.json"
GEOMETRY_REPORT_PATH = QA_ROOT / "fbx_geometry_normals_validation.json"
CULLING_REPORT_PATH = QA_ROOT / "backface_culling_comparison.json"
MANIFEST_PATH = QA_ROOT / "handoff_manifest.json"
CONTACT_SHEET_PATH = QA_ROOT / "contact_sheet.png"

VIEW_NAMES = ["left_x_pos", "right_x_neg", "top_y_pos", "muzzle_z_pos", "iso_muzzle"]
ROWS = [
    ("PBR + exact binary emission", "PBR_Emission"),
    ("Emission-only contamination gate", "EmissionOnly"),
    ("World normals + backface culling", "NormalsBackfaceCulled"),
]


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def load_report(path: Path) -> dict:
    if not path.is_file():
        raise FileNotFoundError(path)
    return json.loads(path.read_text(encoding="utf-8"))


texture_report = load_report(TEXTURE_REPORT_PATH)
geometry_report = load_report(GEOMETRY_REPORT_PATH)

culling_views = {}
for view_name in VIEW_NAMES:
    off_path = QA_ROOT / "Renders" / "CullingOff" / f"{view_name}.png"
    on_path = QA_ROOT / "Renders" / "CullingOn" / f"{view_name}.png"
    with Image.open(off_path) as off_image, Image.open(on_path) as on_image:
        off = np.asarray(off_image.convert("RGBA"), dtype=np.uint8)
        on = np.asarray(on_image.convert("RGBA"), dtype=np.uint8)
    off_visible = off[:, :, 3] > 8
    on_visible = on[:, :, 3] > 8
    lost = off_visible & ~on_visible
    gained = on_visible & ~off_visible
    off_count = int(off_visible.sum())
    lost_count = int(lost.sum())
    culling_views[view_name] = {
        "culling_off_visible_pixels": off_count,
        "culling_on_visible_pixels": int(on_visible.sum()),
        "pixels_lost_when_culling_enabled": lost_count,
        "pixels_gained_when_culling_enabled": int(gained.sum()),
        "lost_ratio_of_double_sided_coverage": 0.0 if off_count == 0 else lost_count / off_count,
        "off_sha256": sha256(off_path),
        "on_sha256": sha256(on_path),
    }

maximum_culling_loss_ratio = max(
    entry["lost_ratio_of_double_sided_coverage"] for entry in culling_views.values()
)
culling_checks = {
    "all_views_have_visible_geometry": all(
        entry["culling_off_visible_pixels"] > 0 and entry["culling_on_visible_pixels"] > 0
        for entry in culling_views.values()
    ),
    "no_material_silhouette_loss_with_backface_culling": maximum_culling_loss_ratio <= 0.001,
}
culling_report = {
    "item_id": ITEM_ID,
    "method": (
        "Matched orthographic flat-unlit renders were compared pixel-for-pixel using alpha > 8. "
        "The only changed render setting is material backface culling."
    ),
    "views": culling_views,
    "maximum_lost_ratio": maximum_culling_loss_ratio,
    "pass_threshold": 0.001,
    "checks": culling_checks,
    "all_checks_passed": all(culling_checks.values()),
}
CULLING_REPORT_PATH.write_text(json.dumps(culling_report, indent=2), encoding="utf-8")

cell_size = 384
label_height = 44
row_title_width = 280
sheet_width = row_title_width + cell_size * len(VIEW_NAMES)
sheet_height = label_height + (cell_size + label_height) * len(ROWS)
sheet = Image.new("RGB", (sheet_width, sheet_height), (12, 14, 20))
draw = ImageDraw.Draw(sheet)
font = ImageFont.load_default()

view_labels = {
    "left_x_pos": "LEFT +X",
    "right_x_neg": "RIGHT -X",
    "top_y_pos": "TOP +Y",
    "muzzle_z_pos": "MUZZLE +Z",
    "iso_muzzle": "ISO MUZZLE",
}
for column, view_name in enumerate(VIEW_NAMES):
    x = row_title_width + column * cell_size
    draw.text((x + 12, 16), view_labels[view_name], fill=(232, 235, 244), font=font)

render_hashes = {}
for row, (row_label, folder_name) in enumerate(ROWS):
    y = label_height + row * (cell_size + label_height)
    draw.text((14, y + 16), row_label, fill=(232, 235, 244), font=font)
    for column, view_name in enumerate(VIEW_NAMES):
        image_path = QA_ROOT / "Renders" / folder_name / f"{view_name}.png"
        with Image.open(image_path) as source:
            tile = source.convert("RGB")
            tile.thumbnail((cell_size, cell_size), Image.Resampling.LANCZOS)
        tile_canvas = Image.new("RGB", (cell_size, cell_size), (7, 9, 14))
        tile_canvas.paste(tile, ((cell_size - tile.width) // 2, (cell_size - tile.height) // 2))
        x = row_title_width + column * cell_size
        sheet.paste(tile_canvas, (x, y))
        draw.rectangle((x, y, x + cell_size - 1, y + cell_size - 1), outline=(54, 61, 78), width=1)
        render_hashes[str(image_path.relative_to(QA_ROOT)).replace("\\", "/")] = sha256(image_path)

footer_y = sheet_height - label_height
draw.text(
    (14, footer_y + 15),
    "Emission: #FF4A1A / bounded global RGB family / strict binary mask / no UV or spatial exception",
    fill=(245, 132, 72),
    font=font,
)
sheet.save(CONTACT_SHEET_PATH, format="PNG", optimize=True)

production_files = {
    "blend": PRODUCTION_ROOT / f"{ITEM_ID}.blend",
    "fbx": PRODUCTION_ROOT / f"{ITEM_ID}.fbx",
    "base_color": PRODUCTION_ROOT / "Textures" / f"{ITEM_ID}_BaseColor.png",
    "normal_gl": PRODUCTION_ROOT / "Textures" / f"{ITEM_ID}_Normal.png",
    "orm": PRODUCTION_ROOT / "Textures" / f"{ITEM_ID}_MetallicRoughness.png",
    "emission_binary": PRODUCTION_ROOT / "Textures" / f"{ITEM_ID}_Emission.png",
    "reimport_blend": PRODUCTION_ROOT / "QA" / "Reimport" / f"{ITEM_ID}_reimport.blend",
}
production_entries = {
    role: {
        "path": str(path.resolve()),
        "exists": path.is_file(),
        "sha256": sha256(path) if path.is_file() else None,
    }
    for role, path in production_files.items()
}

unity_outputs = texture_report["unity_outputs"]
manifest_checks = {
    "production_files_exist": all(entry["exists"] for entry in production_entries.values()),
    "texture_and_emission_pixel_checks_pass": texture_report["all_checks_passed"],
    "fbx_geometry_anchor_normal_checks_pass": geometry_report["all_geometry_checks_passed"],
    "backface_culling_render_comparison_pass": culling_report["all_checks_passed"],
    "five_pbr_emission_views_exist": all(
        (QA_ROOT / "Renders" / "PBR_Emission" / f"{name}.png").is_file() for name in VIEW_NAMES
    ),
    "five_emission_only_views_exist": all(
        (QA_ROOT / "Renders" / "EmissionOnly" / f"{name}.png").is_file() for name in VIEW_NAMES
    ),
    "five_normal_culled_views_exist": all(
        (QA_ROOT / "Renders" / "NormalsBackfaceCulled" / f"{name}.png").is_file() for name in VIEW_NAMES
    ),
}

manifest = {
    "item_id": ITEM_ID,
    "status": "pass" if all(manifest_checks.values()) else "fail",
    "scope": "ArtSource handoff QA only; no Unity Assets were modified",
    "production_files": production_entries,
    "unity_texture_handoff": unity_outputs,
    "unity_import_contract": {
        "BaseColor": "Default texture, sRGB enabled",
        "NormalGL": "Normal Map, sRGB disabled; source is tangent-space NormalGL. Leave Flip Green disabled unless the project shader explicitly uses the opposite handedness",
        "MetallicSmoothness": "Default texture, sRGB disabled; URP Lit Metallic Map uses R and Smoothness Source=Metallic Alpha",
        "Occlusion": "Default texture, sRGB disabled; grayscale AO in RGB",
        "MOS": "Default texture, sRGB disabled; custom packed R=Metallic G=Occlusion B=Smoothness",
        "Emission": "Default texture, sRGB disabled for binary mask; HDR color #FF4A1A, enable _EMISSION",
    },
    "emission": texture_report["emission_validation"],
    "geometry_and_anchors": {
        "bounds_min_m": geometry_report["geometry"]["bounds_min_m"],
        "bounds_max_m": geometry_report["geometry"]["bounds_max_m"],
        "dimensions_m": geometry_report["geometry"]["dimensions_m"],
        "triangles": geometry_report["geometry"]["triangles"],
        "vertices": geometry_report["geometry"]["vertices"],
        "anchors_world": geometry_report["anchors_world"],
        "muzzle_probe": geometry_report["muzzle_probe"],
    },
    "normals_and_backface": {
        "zero_area_faces": geometry_report["geometry"]["zero_area_faces"],
        "invalid_normal_faces": geometry_report["geometry"]["invalid_normal_faces"],
        "shared_edge_winding_conflicts": geometry_report["geometry"]["shared_edge_winding_conflicts"],
        "boundary_edges_single_face": geometry_report["geometry"]["boundary_edges_single_face"],
        "watertight": geometry_report["geometry"]["watertight"],
        "nonmanifold_edges_more_than_two_faces": geometry_report["geometry"]["nonmanifold_edges_more_than_two_faces"],
        "maximum_culling_lost_ratio": maximum_culling_loss_ratio,
        "retained_source_topology_risk": geometry_report["topology_note"],
    },
    "render_hashes": render_hashes,
    "reports": {
        "texture_and_emission": str(TEXTURE_REPORT_PATH.resolve()),
        "fbx_geometry_normals": str(GEOMETRY_REPORT_PATH.resolve()),
        "backface_culling": str(CULLING_REPORT_PATH.resolve()),
        "contact_sheet": str(CONTACT_SHEET_PATH.resolve()),
        "contact_sheet_sha256": sha256(CONTACT_SHEET_PATH),
    },
    "regeneration": {
        "entrypoint": str((QA_ROOT / "run_handoff_qa.ps1").resolve()),
        "steps": [
            "../build_emission_mask.py",
            "build_unity_handoff_textures.py",
            "render_fbx_handoff_qa.py in Blender 5.1 background mode",
            "finalize_handoff_qa.py",
        ],
    },
    "checks": manifest_checks,
    "all_checks_passed": all(manifest_checks.values()),
}
MANIFEST_PATH.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
print(json.dumps(manifest, indent=2))
if not manifest["all_checks_passed"]:
    raise RuntimeError("Handoff manifest validation failed")
