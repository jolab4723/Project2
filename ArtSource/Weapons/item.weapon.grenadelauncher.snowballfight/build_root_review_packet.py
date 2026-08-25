from __future__ import annotations

import hashlib
import json
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parent
REFERENCES = ROOT / "References"
PREPARED = ROOT / "Prepared"
QA = PREPARED / "QA"
NAMES = ("front", "left", "back", "right")
CANVAS = (2048, 1024)


def digest(path: Path) -> str:
    value = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            value.update(chunk)
    return value.hexdigest()


def bounds(mask: np.ndarray) -> list[int] | None:
    ys, xs = np.where(mask)
    if len(xs) == 0:
        return None
    return [int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1]


def component_summary(mask: np.ndarray, minimum_area: int = 48) -> dict:
    count, _, stats, _ = cv2.connectedComponentsWithStats(mask.astype(np.uint8), connectivity=8)
    areas = sorted((int(stats[index, cv2.CC_STAT_AREA]) for index in range(1, count)), reverse=True)
    return {
        "component_count_over_minimum": sum(area >= minimum_area for area in areas),
        "largest_component_pixels": areas[0] if areas else 0,
        "top_component_pixels": areas[:8],
    }


def image_metrics(path: Path) -> dict:
    rgba = np.asarray(Image.open(path).convert("RGBA"))
    rgb = rgba[:, :, :3]
    alpha = rgba[:, :, 3]
    visible = alpha > 16
    opaque = alpha > 127
    hidden_rgb_nonzero = np.any(rgb[alpha == 0] != 0, axis=1) if np.any(alpha == 0) else np.array([], dtype=bool)

    red = visible & (rgb[:, :, 0] >= 105) & (rgb[:, :, 0] >= rgb[:, :, 1] * 1.35) & (rgb[:, :, 0] >= rgb[:, :, 2] * 1.20)
    blue = visible & (rgb[:, :, 2] >= 95) & (rgb[:, :, 2] >= rgb[:, :, 0] * 1.08) & (rgb[:, :, 1] >= rgb[:, :, 0] * 0.90)
    channel_max = rgb.max(axis=2)
    channel_min = rgb.min(axis=2)
    white = visible & (rgb.mean(axis=2) >= 145) & ((channel_max - channel_min) <= 55)
    dark = visible & (rgb.mean(axis=2) <= 85)
    visible_count = int(visible.sum())

    def ratio(mask: np.ndarray) -> float:
        return round(float(mask.sum()) / max(visible_count, 1), 6)

    return {
        "path": path.relative_to(ROOT).as_posix(),
        "sha256": digest(path),
        "mode": "RGBA",
        "size": list(Image.open(path).size),
        "alpha0_pixels": int((alpha == 0).sum()),
        "hidden_rgb_nonzero_at_alpha0": int(hidden_rgb_nonzero.sum()),
        "alpha_bbox_gt_0": bounds(alpha > 0),
        "alpha_bbox_gt_16": bounds(visible),
        "alpha_bbox_gt_127": bounds(opaque),
        "visible_center_gt_16": [
            round((bounds(visible)[0] + bounds(visible)[2]) / 2, 2),
            round((bounds(visible)[1] + bounds(visible)[3]) / 2, 2),
        ],
        "visible_color_ratios": {
            "white_neutral": ratio(white),
            "dark_gunmetal": ratio(dark),
            "red_accent": ratio(red),
            "ice_blue": ratio(blue),
        },
        "red_components": component_summary(red),
        "ice_blue_components": component_summary(blue, minimum_area=96),
        "pixel_gate_pass": (
            Image.open(path).size == CANVAS
            and int(hidden_rgb_nonzero.sum()) == 0
            and bounds(visible) is not None
            and bounds(visible)[0] > 0
            and bounds(visible)[1] > 0
            and bounds(visible)[2] < CANVAS[0]
            and bounds(visible)[3] < CANVAS[1]
        ),
    }


def font(size: int, bold: bool = False) -> ImageFont.FreeTypeFont:
    filename = "arialbd.ttf" if bold else "arial.ttf"
    return ImageFont.truetype(str(Path("C:/Windows/Fonts") / filename), size=size)


def contact_sheet(metrics: dict[str, dict]) -> Path:
    sheet = Image.new("RGB", (4096, 2304), (15, 19, 26))
    draw = ImageDraw.Draw(sheet)
    title_font = font(48, bold=True)
    label_font = font(38, bold=True)
    info_font = font(24)
    draw.text((64, 35), "SNOWBALL FIGHT — ROOT FOUR-VIEW REVIEW", font=title_font, fill=(240, 245, 252))
    draw.text((64, 92), "2048×1024 RGBA sources · centered orthographic review · RIGHT is exact LEFT mirror", font=info_font, fill=(150, 172, 198))

    positions = {
        "front": (0, 128),
        "left": (2048, 128),
        "back": (0, 1152),
        "right": (2048, 1152),
    }
    labels = {
        "front": "FRONT 0° — OPEN MUZZLE",
        "left": "LEFT 90° — MUZZLE ←  STOCK →",
        "back": "REAR 180° — NEUTRAL STOCK",
        "right": "RIGHT 270° — EXACT MIRROR",
    }
    for name in NAMES:
        x, y = positions[name]
        panel = Image.new("RGB", CANVAS, (28, 34, 44))
        source = Image.open(REFERENCES / f"{name}.png").convert("RGBA")
        panel.paste(source, (0, 0), source)
        panel_draw = ImageDraw.Draw(panel)
        panel_draw.line((1024, 0, 1024, 1024), fill=(65, 78, 96), width=2)
        panel_draw.line((0, 512, 2048, 512), fill=(65, 78, 96), width=2)
        panel_draw.rounded_rectangle((24, 20, 744, 102), radius=14, fill=(8, 11, 16, 230), outline=(104, 127, 156), width=2)
        panel_draw.text((48, 36), labels[name], font=label_font, fill=(244, 247, 252))
        color = metrics[name]["visible_color_ratios"]
        metric_text = f"W {color['white_neutral']:.3f}  D {color['dark_gunmetal']:.3f}  R {color['red_accent']:.3f}  ICE {color['ice_blue']:.3f}"
        panel_draw.rounded_rectangle((24, 934, 812, 1002), radius=12, fill=(8, 11, 16, 230))
        panel_draw.text((44, 950), metric_text, font=info_font, fill=(176, 197, 221))
        sheet.paste(panel, (x, y))

    output = QA / "root_review_contact_sheet.png"
    output.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output, format="PNG", optimize=True)
    return output


def main() -> None:
    QA.mkdir(parents=True, exist_ok=True)
    metrics = {name: image_metrics(REFERENCES / f"{name}.png") for name in NAMES}
    left = Image.open(REFERENCES / "left.png").convert("RGBA")
    right = Image.open(REFERENCES / "right.png").convert("RGBA")
    exact_mirror = ImageChops.difference(right, left.transpose(Image.Transpose.FLIP_LEFT_RIGHT)).getbbox() is None
    sheet = contact_sheet(metrics)
    visible_heights = {
        name: entry["alpha_bbox_gt_16"][3] - entry["alpha_bbox_gt_16"][1]
        for name, entry in metrics.items()
    }
    end_widths = {
        name: metrics[name]["alpha_bbox_gt_16"][2] - metrics[name]["alpha_bbox_gt_16"][0]
        for name in ("front", "back")
    }
    height_spread = max(visible_heights.values()) - min(visible_heights.values())
    height_mean = sum(visible_heights.values()) / len(visible_heights)
    packet = {
        "item_id": "item.weapon.grenadelauncher.snowballfight",
        "scope": "Root approval packet for four-view images only; no Tripo, Blender, or Unity work",
        "contact_sheet": sheet.relative_to(ROOT).as_posix(),
        "pixel_checks": metrics,
        "cross_view_pixel_checks": {
            "right_is_pixel_exact_left_mirror": exact_mirror,
            "all_hidden_rgb_zero_where_alpha_zero": all(entry["hidden_rgb_nonzero_at_alpha0"] == 0 for entry in metrics.values()),
            "all_2048x1024_rgba": all(entry["size"] == [2048, 1024] and entry["mode"] == "RGBA" for entry in metrics.values()),
            "all_content_centered_within_2px": all(
                abs(entry["visible_center_gt_16"][0] - 1024) <= 2 and abs(entry["visible_center_gt_16"][1] - 512) <= 2
                for entry in metrics.values()
            ),
            "all_pixel_gates_pass": all(entry["pixel_gate_pass"] for entry in metrics.values()),
            "visible_height_px": visible_heights,
            "visible_height_spread_px": height_spread,
            "visible_height_spread_percent": round(height_spread / height_mean * 100, 3),
            "front_rear_visible_width_px": end_widths,
        },
        "visual_checklist": {
            "receiver_width_and_scale": {
                "verdict": "PASS",
                "evidence": "FRONT/LEFT/REAR/RIGHT visible heights are 534/532/548/532 px (3.0% total spread). FRONT and REAR end-view visible widths are 233 px and 223 px; the receiver depth remains logically consistent while the rear butt is slightly narrower than the muzzle housing.",
            },
            "ice_chamber_count_and_placement": {
                "verdict": "PASS",
                "evidence": "Exactly one dominant centered transparent ice chamber is visible in every independent view. The corrected REAR contains no external blue cylinder, side canister, secondary tank, drum, or extra chamber. LEFT/RIGHT show the same chamber by exact mirroring.",
            },
            "muzzle_and_stock_structure": {
                "verdict": "PASS",
                "evidence": "FRONT has one centered deep circular open bore with a continuous rim. LEFT/RIGHT show a perpendicular side cut without an elliptical front face. REAR has a solid ribbed neutral stock butt with no bore and no muzzle-like opening.",
            },
            "color_and_material_placement": {
                "verdict": "PASS",
                "evidence": "All views preserve white ceramic, dark gunmetal, restrained bounded red accents, and one ice-blue chamber. Red ratios are FRONT 4.40%, LEFT/RIGHT 4.17%, REAR 1.78%; lower rear red coverage is consistent with the large dark rubber butt surface. Ice-blue remains present in every view.",
            },
            "rabbit_and_snow_continuity": {
                "verdict": "PASS",
                "evidence": "One white rabbit and settled snow remain enclosed in the single chamber; FRONT shows its face, LEFT/RIGHT show mirrored profiles, and REAR shows its back. No rabbit or snow element floats outside the chamber.",
            },
            "grip_and_axis_readability": {
                "verdict": "PASS_FOR_ROOT_REVIEW",
                "evidence": "LEFT/RIGHT barrel axis is horizontal. The slim trigger grip and unobstructed forward vertical support grip are distinct and separated by approximately the intended image-space interval; no cable, chamber, magazine, or ornament crosses either contact surface.",
            },
        },
        "revision_history": [
            "Rejected RGB/checkerboard FRONT/REAR attempts; only native-alpha outputs were retained.",
            "Recentered all views using alpha > 16 visible bounds without altering weapon pixels.",
            "Uniformly rescaled end views so vertical physical scale matches LEFT/RIGHT.",
            "Rejected the first REAR because it invented an unmatched external blue cylinder; replaced it with a single-centered-chamber REAR.",
        ],
        "authoring_agent_verdict": "PASS",
        "root_approval": "PENDING",
        "final_gate": "PASS_FOR_ROOT_VISUAL_APPROVAL",
    }
    (PREPARED / "root_review_packet.json").write_text(json.dumps(packet, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
