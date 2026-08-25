from __future__ import annotations

import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image


ITEM_ID = "item.weapon.shotgun.embercoil"
QA_ROOT = Path(__file__).resolve().parent
PRODUCTION_ROOT = QA_ROOT.parent
SOURCE_TEXTURES = PRODUCTION_ROOT / "Textures"
OUTPUT_TEXTURES = QA_ROOT / "UnityTextures"
REPORT_PATH = QA_ROOT / "texture_and_emission_validation.json"

BASE_COLOR_PATH = SOURCE_TEXTURES / f"{ITEM_ID}_BaseColor.png"
ORM_PATH = SOURCE_TEXTURES / f"{ITEM_ID}_MetallicRoughness.png"
EMISSION_PATH = SOURCE_TEXTURES / f"{ITEM_ID}_Emission.png"
NORMAL_PATH = SOURCE_TEXTURES / f"{ITEM_ID}_Normal.png"

METALLIC_SMOOTHNESS_PATH = OUTPUT_TEXTURES / f"{ITEM_ID}_MetallicSmoothness.png"
OCCLUSION_PATH = OUTPUT_TEXTURES / f"{ITEM_ID}_Occlusion.png"
MOS_PATH = OUTPUT_TEXTURES / f"{ITEM_ID}_MOS.png"

DESIGNATED_SRGB = np.array([255, 74, 26], dtype=np.uint8)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def load_rgba(path: Path) -> np.ndarray:
    if not path.is_file():
        raise FileNotFoundError(path)
    with Image.open(path) as image:
        return np.asarray(image.convert("RGBA"), dtype=np.uint8).copy()


def save_rgba(path: Path, pixels: np.ndarray) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(pixels, mode="RGBA").save(path, format="PNG", optimize=True)


base = load_rgba(BASE_COLOR_PATH)
orm = load_rgba(ORM_PATH)
emission = load_rgba(EMISSION_PATH)
normal = load_rgba(NORMAL_PATH)

source_shapes = {tuple(array.shape) for array in (base, orm, emission, normal)}
if len(source_shapes) != 1:
    raise RuntimeError(f"PBR texture dimensions differ: {source_shapes}")

ao = orm[:, :, 0]
roughness = orm[:, :, 1]
metallic = orm[:, :, 2]
smoothness = 255 - roughness

metallic_smoothness = np.zeros_like(orm)
metallic_smoothness[:, :, 0] = metallic
metallic_smoothness[:, :, 3] = smoothness

occlusion = np.empty_like(orm)
occlusion[:, :, 0] = ao
occlusion[:, :, 1] = ao
occlusion[:, :, 2] = ao
occlusion[:, :, 3] = 255

mos = np.empty_like(orm)
mos[:, :, 0] = metallic
mos[:, :, 1] = ao
mos[:, :, 2] = smoothness
mos[:, :, 3] = 255

save_rgba(METALLIC_SMOOTHNESS_PATH, metallic_smoothness)
save_rgba(OCCLUSION_PATH, occlusion)
save_rgba(MOS_PATH, mos)

mask_rgb = emission[:, :, :3]
mask_white = np.all(mask_rgb == 255, axis=2)
mask_black = np.all(mask_rgb == 0, axis=2)
mask_binary = mask_white | mask_black

base_rgb = base[:, :, :3]
r = base_rgb[:, :, 0].astype(np.int16)
g = base_rgb[:, :, 1].astype(np.int16)
b = base_rgb[:, :, 2].astype(np.int16)
global_rule = (
    (r >= 235)
    & (g >= 90)
    & (g <= 120)
    & (b <= 45)
    & ((r - g) >= 115)
    & (g >= b)
)
exact_designated = np.all(base_rgb == DESIGNATED_SRGB, axis=2)

selected_count = int(mask_white.sum())
exact_designated_total = int(exact_designated.sum())
exact_designated_selected = int((exact_designated & mask_white).sum())
selected_outside_global_rule = int((mask_white & ~global_rule).sum())
global_rule_missing_from_mask = int((global_rule & ~mask_white).sum())
nonbinary_pixels = int((~mask_binary).sum())

selected_colors, selected_color_counts = np.unique(
    base_rgb[mask_white].reshape(-1, 3), axis=0, return_counts=True
)
top_order = np.argsort(selected_color_counts)[::-1][:16]
top_selected_colors = [
    {
        "srgb": [int(value) for value in selected_colors[index]],
        "count": int(selected_color_counts[index]),
    }
    for index in top_order
]

def channel_extrema(channel: np.ndarray) -> list[int]:
    return [int(channel.min()), int(channel.max())]


outputs = {
    "MetallicSmoothness": {
        "path": str(METALLIC_SMOOTHNESS_PATH.resolve()),
        "contract": "R=Metallic, G=0, B=0, A=Smoothness(255-Roughness)",
        "channel_extrema": {
            "R": channel_extrema(metallic_smoothness[:, :, 0]),
            "G": channel_extrema(metallic_smoothness[:, :, 1]),
            "B": channel_extrema(metallic_smoothness[:, :, 2]),
            "A": channel_extrema(metallic_smoothness[:, :, 3]),
        },
    },
    "Occlusion": {
        "path": str(OCCLUSION_PATH.resolve()),
        "contract": "RGB=Occlusion(ORM.R), A=255",
        "channel_extrema": {
            "R": channel_extrema(occlusion[:, :, 0]),
            "G": channel_extrema(occlusion[:, :, 1]),
            "B": channel_extrema(occlusion[:, :, 2]),
            "A": channel_extrema(occlusion[:, :, 3]),
        },
    },
    "MOS": {
        "path": str(MOS_PATH.resolve()),
        "contract": "R=Metallic(ORM.B), G=Occlusion(ORM.R), B=Smoothness(255-ORM.G), A=255",
        "channel_extrema": {
            "R": channel_extrema(mos[:, :, 0]),
            "G": channel_extrema(mos[:, :, 1]),
            "B": channel_extrema(mos[:, :, 2]),
            "A": channel_extrema(mos[:, :, 3]),
        },
    },
}
for output in outputs.values():
    output_path = Path(output["path"])
    output["sha256"] = sha256(output_path)
    output["size"] = [int(base.shape[1]), int(base.shape[0])]
    output["mode"] = "RGBA"

checks = {
    "all_source_textures_same_dimensions": len(source_shapes) == 1,
    "emission_mask_is_strict_binary_rgb": nonbinary_pixels == 0,
    "emission_alpha_is_opaque": bool(np.all(emission[:, :, 3] == 255)),
    "all_mask_white_pixels_match_global_rgb_rule": selected_outside_global_rule == 0,
    "global_rgb_rule_and_binary_mask_are_exactly_equivalent": global_rule_missing_from_mask == 0,
    "all_exact_designated_rgb_pixels_are_selected": exact_designated_selected == exact_designated_total,
    "unity_metallic_smoothness_channels_match_source": bool(
        np.array_equal(metallic_smoothness[:, :, 0], metallic)
        and np.array_equal(metallic_smoothness[:, :, 3], smoothness)
    ),
    "unity_occlusion_channels_match_source": bool(
        np.array_equal(occlusion[:, :, 0], ao)
        and np.array_equal(occlusion[:, :, 1], ao)
        and np.array_equal(occlusion[:, :, 2], ao)
    ),
    "unity_mos_channels_match_source": bool(
        np.array_equal(mos[:, :, 0], metallic)
        and np.array_equal(mos[:, :, 1], ao)
        and np.array_equal(mos[:, :, 2], smoothness)
    ),
}

report = {
    "item_id": ITEM_ID,
    "source_contract": "Tripo ORM: R=Occlusion, G=Roughness, B=Metallic",
    "source_textures": {
        "BaseColor_sRGB": {"path": str(BASE_COLOR_PATH.resolve()), "sha256": sha256(BASE_COLOR_PATH)},
        "ORM_NonColor": {"path": str(ORM_PATH.resolve()), "sha256": sha256(ORM_PATH)},
        "NormalGL_NonColor": {"path": str(NORMAL_PATH.resolve()), "sha256": sha256(NORMAL_PATH)},
        "Emission_GlobalBinary_NonColor": {"path": str(EMISSION_PATH.resolve()), "sha256": sha256(EMISSION_PATH)},
    },
    "texture_size": [int(base.shape[1]), int(base.shape[0])],
    "emission_validation": {
        "designated_srgb": [int(value) for value in DESIGNATED_SRGB],
        "designated_hex": "#FF4A1A",
        "global_binary_rule": {
            "r_min": 235,
            "g_min": 90,
            "g_max": 120,
            "b_max": 45,
            "r_minus_g_min": 115,
            "g_greater_than_or_equal_to_b": True,
        },
        "selected_pixels": selected_count,
        "selected_percent": selected_count * 100.0 / mask_white.size,
        "nonbinary_pixels": nonbinary_pixels,
        "selected_outside_global_rule": selected_outside_global_rule,
        "global_rule_missing_from_mask": global_rule_missing_from_mask,
        "exact_designated_rgb_total": exact_designated_total,
        "exact_designated_rgb_selected": exact_designated_selected,
        "family_colored_selected_pixels": selected_count - exact_designated_selected,
        "top_selected_base_colors": top_selected_colors,
        "contamination_interpretation": (
            "Pixel QA: zero mask pixels outside the approved bounded global RGB family and zero approved-family pixels omitted. "
            "The bounded family separates the coil from the lower-green receiver fleck and higher-green copper trim. "
            "Geometry-projected five-view emission-only renders provide the visual contamination gate."
        ),
        "uv_exception": False,
        "mesh_spatial_morphology_blur_filtering": False,
    },
    "unity_outputs": outputs,
    "checks": checks,
    "all_checks_passed": all(checks.values()),
}
REPORT_PATH.write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps(report, indent=2))
if not report["all_checks_passed"]:
    raise RuntimeError("Texture or emission validation failed")
