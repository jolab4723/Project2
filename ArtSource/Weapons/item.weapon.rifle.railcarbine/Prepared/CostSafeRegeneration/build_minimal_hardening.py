from __future__ import annotations

import hashlib
import json
import shutil
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageOps


ROOT = Path(__file__).resolve().parent
PROJECT = ROOT.parents[4]
AUTHORITY = PROJECT / "ArtSource" / "Weapons" / "item.weapon.rifle.railcarbine" / "References"
CANONICAL = ROOT / "Canonical"
TRIPO_SLOTS = ROOT / "TripoSlots"
QA = ROOT / "QA"
SIZE = (2048, 1024)
ITEM_ID = "item.weapon.rifle.railcarbine"


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def alpha_bbox(image: Image.Image) -> tuple[int, int, int, int]:
    bbox = image.getchannel("A").getbbox()
    if bbox is None:
        raise RuntimeError("Image contains no opaque pixels")
    return bbox


def sanitize_hidden_rgb(image: Image.Image) -> Image.Image:
    data = np.array(image.convert("RGBA"), dtype=np.uint8)
    transparent = data[:, :, 3] == 0
    data[transparent, :3] = 0
    return Image.fromarray(data, "RGBA")


def center_uniform_height(image: Image.Image, target_height: int) -> tuple[Image.Image, dict]:
    source_bbox = alpha_bbox(image)
    crop = image.crop(source_bbox)
    scale = target_height / crop.height
    target_width = round(crop.width * scale)
    resized = crop.resize((target_width, target_height), Image.Resampling.LANCZOS)
    output = Image.new("RGBA", SIZE, (0, 0, 0, 0))
    placement = ((SIZE[0] - target_width + 1) // 2, (SIZE[1] - target_height) // 2)
    output.alpha_composite(resized, placement)
    output = sanitize_hidden_rgb(output)
    return output, {
        "source_alpha_bbox": list(source_bbox),
        "uniform_scale": round(scale, 9),
        "placed_size": [target_width, target_height],
        "placement": list(placement),
    }


def cyan_mask(image: Image.Image) -> np.ndarray:
    data = np.array(image.convert("RGBA"), dtype=np.uint8)
    red = data[:, :, 0].astype(np.int16)
    green = data[:, :, 1].astype(np.int16)
    blue = data[:, :, 2].astype(np.int16)
    alpha = data[:, :, 3]
    return (
        (alpha > 0)
        & (red < 100)
        & (green > 150)
        & (blue > 150)
        & ((green - red) > 70)
        & ((blue - red) > 70)
    )


def connected_components(mask: np.ndarray, minimum_area: int = 10) -> list[dict]:
    count, labels, stats, centroids = cv2.connectedComponentsWithStats(mask.astype(np.uint8), 8)
    components = []
    for index in range(1, count):
        x, y, width, height, area = [int(value) for value in stats[index]]
        if area < minimum_area:
            continue
        components.append(
            {
                "label": index,
                "bbox": [x, y, width, height],
                "area": area,
                "center": [round(float(centroids[index][0]), 6), round(float(centroids[index][1]), 6)],
                "mask": labels == index,
            }
        )
    return sorted(components, key=lambda item: item["area"], reverse=True)


def rail_measurements(image: Image.Image) -> dict:
    components = connected_components(cyan_mask(image))
    rails = [component for component in components if component["bbox"][2] >= 400][:2]
    if len(rails) != 2:
        raise RuntimeError(f"Expected two long cyan rails, found {len(rails)}")
    rails.sort(key=lambda component: component["center"][1])
    entries = []
    for component in rails:
        ys, xs = np.nonzero(component["mask"])
        slope, intercept = np.polyfit(xs.astype(float), ys.astype(float), 1)
        x_min = int(xs.min())
        x_max = int(xs.max())
        entries.append(
            {
                "bbox": component["bbox"],
                "area": component["area"],
                "center": component["center"],
                "axis_slope_px_per_1000px": round(float(slope * 1000.0), 6),
                "endpoint_drift_px": round(float(slope * (x_max - x_min)), 6),
                "fit": [float(slope), float(intercept)],
            }
        )

    alpha = np.array(image.getchannel("A"))
    split_y = (entries[0]["center"][1] + entries[1]["center"][1]) / 2.0
    cap_centers = []
    for low_y, high_y in ((330, int(split_y)), (int(split_y), 590)):
        ys, xs = np.nonzero((alpha > 0) & (np.indices(alpha.shape)[1] >= 1580) & (np.indices(alpha.shape)[0] >= low_y) & (np.indices(alpha.shape)[0] < high_y))
        if not len(xs):
            raise RuntimeError("Missing terminal cap pixels")
        cap_centers.append(
            {
                "bbox": [int(xs.min()), int(ys.min()), int(xs.max() - xs.min() + 1), int(ys.max() - ys.min() + 1)],
                "center": [round(float((xs.min() + xs.max()) / 2.0), 6), round(float((ys.min() + ys.max()) / 2.0), 6)],
                "front_edge_x": int(xs.max()),
            }
        )

    for rail, cap in zip(entries, cap_centers):
        slope, intercept = rail.pop("fit")
        predicted_y = slope * cap["center"][0] + intercept
        cap["delta_y_from_rail_axis_px"] = round(float(cap["center"][1] - predicted_y), 6)

    return {
        "upper": entries[0],
        "lower": entries[1],
        "parallel_slope_delta_px_per_1000px": round(abs(entries[0]["axis_slope_px_per_1000px"] - entries[1]["axis_slope_px_per_1000px"]), 6),
        "rail_center_spacing_px": round(entries[1]["center"][1] - entries[0]["center"][1], 6),
        "upper_cap": cap_centers[0],
        "lower_cap": cap_centers[1],
        "cap_front_edge_x_delta_px": abs(cap_centers[0]["front_edge_x"] - cap_centers[1]["front_edge_x"]),
    }


def front_measurements(image: Image.Image) -> dict:
    components = connected_components(cyan_mask(image), minimum_area=100)
    caps = components[:2]
    if len(caps) != 2:
        raise RuntimeError(f"Expected two cyan emitter faces, found {len(caps)}")
    caps.sort(key=lambda component: component["center"][1])
    upper, lower = caps
    return {
        "upper": {key: upper[key] for key in ("bbox", "area", "center")},
        "lower": {key: lower[key] for key in ("bbox", "area", "center")},
        "center_x_delta_px": round(abs(upper["center"][0] - lower["center"][0]), 6),
        "bbox_center_x_delta_px": round(abs((upper["bbox"][0] + (upper["bbox"][2] - 1) / 2) - (lower["bbox"][0] + (lower["bbox"][2] - 1) / 2)), 6),
        "width_delta_px": abs(upper["bbox"][2] - lower["bbox"][2]),
        "height_delta_px": abs(upper["bbox"][3] - lower["bbox"][3]),
        "canvas_center_x_px": 1023.5,
        "upper_center_offset_from_canvas_px": round(upper["center"][0] - 1023.5, 6),
        "lower_center_offset_from_canvas_px": round(lower["center"][0] - 1023.5, 6),
    }


def image_checks(image: Image.Image) -> dict:
    data = np.array(image.convert("RGBA"), dtype=np.uint8)
    transparent = data[:, :, 3] == 0
    border = np.concatenate((data[0, :, 3], data[-1, :, 3], data[:, 0, 3], data[:, -1, 3]))
    return {
        "mode": image.mode,
        "size": list(image.size),
        "alpha_extrema": list(image.getchannel("A").getextrema()),
        "alpha_bbox": list(alpha_bbox(image)),
        "transparent_pixels": int(transparent.sum()),
        "hidden_rgb_nonzero_values": int(np.count_nonzero(data[:, :, :3][transparent])),
        "all_borders_alpha_zero": bool(np.all(border == 0)),
    }


def render_contact_sheet(images: dict[str, Image.Image], metrics: dict) -> None:
    sheet = Image.new("RGB", (2048, 1024), (13, 18, 25))
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.truetype(r"C:\Windows\Fonts\consola.ttf", 22)
    small = ImageFont.truetype(r"C:\Windows\Fonts\consola.ttf", 17)
    panels = {
        "left": (0, 0, 1024, 512),
        "right": (1024, 0, 2048, 512),
        "front": (0, 512, 1024, 1024),
        "back": (1024, 512, 2048, 1024),
    }

    for name, bounds in panels.items():
        x0, y0, x1, y1 = bounds
        draw.rectangle((x0, y0, x1 - 1, y1 - 1), outline=(65, 83, 105), width=2)
        image = images[name]
        bbox = alpha_bbox(image)
        crop = image.crop(bbox)
        max_width, max_height = (965, 365) if name in {"left", "right"} else (450, 365)
        scale = min(max_width / crop.width, max_height / crop.height)
        shown = crop.resize((round(crop.width * scale), round(crop.height * scale)), Image.Resampling.LANCZOS)
        checker = Image.new("RGB", shown.size, (30, 38, 49))
        checker.paste(shown.convert("RGB"), mask=shown.getchannel("A"))
        px = x0 + (x1 - x0 - shown.width) // 2
        py = y0 + 58 + (385 - shown.height) // 2
        sheet.paste(checker, (px, py))
        draw.text((x0 + 18, y0 + 14), name.upper(), fill=(235, 242, 250), font=font)

    rail = metrics["side_authority"]
    rail_text = (
        f"rails slope U/L {rail['upper']['axis_slope_px_per_1000px']:+.3f}/"
        f"{rail['lower']['axis_slope_px_per_1000px']:+.3f} px/1000 | parallel delta "
        f"{rail['parallel_slope_delta_px_per_1000px']:.3f} | cap edge delta {rail['cap_front_edge_x_delta_px']} px"
    )
    draw.text((18, 472), rail_text, fill=(87, 239, 255), font=small)
    draw.text((1042, 472), "RIGHT = exact pixel horizontal mirror of LEFT", fill=(87, 239, 255), font=small)

    front = metrics["front_emitters"]
    front_text = (
        f"cyan faces center-X delta {front['center_x_delta_px']:.3f}px | bbox center delta "
        f"{front['bbox_center_x_delta_px']:.3f}px | W/H delta {front['width_delta_px']}/{front['height_delta_px']}px"
    )
    draw.text((18, 980), front_text, fill=(87, 239, 255), font=small)
    draw.text((1042, 980), "REAR: neutral stock face | cyan emitter components = 0", fill=(190, 203, 219), font=small)
    sheet.save(QA / "contact_sheet.png")


for directory in (CANONICAL, TRIPO_SLOTS, QA):
    directory.mkdir(parents=True, exist_ok=True)

authority_images = {name: Image.open(AUTHORITY / f"{name}.png").convert("RGBA") for name in ("front", "left", "back", "right")}
left = sanitize_hidden_rgb(authority_images["left"])
target_height = alpha_bbox(left)[3] - alpha_bbox(left)[1]
front, front_normalization = center_uniform_height(sanitize_hidden_rgb(authority_images["front"]), target_height)
back, back_normalization = center_uniform_height(sanitize_hidden_rgb(authority_images["back"]), target_height)
right = sanitize_hidden_rgb(ImageOps.mirror(left))

canonical_images = {"front": front, "left": left, "back": back, "right": right}
for name, image in canonical_images.items():
    image.save(CANONICAL / f"{name}.png")

slot_mapping = {"front": "back", "left": "left", "back": "front", "right": "right"}
for slot, canonical_name in slot_mapping.items():
    shutil.copyfile(CANONICAL / f"{canonical_name}.png", TRIPO_SLOTS / f"{slot}.png")

metrics = {
    "side_authority": rail_measurements(left),
    "front_emitters": front_measurements(front),
}
checks = {name: image_checks(image) for name, image in canonical_images.items()}
checks["right_exact_mirror"] = bool(np.array_equal(np.array(right), np.array(left)[:, ::-1, :]))
checks["rear_cyan_pixels"] = int(cyan_mask(back).sum())
checks["rear_cyan_emitter_components"] = len(connected_components(cyan_mask(back), minimum_area=100))
checks["canonical_all_rgba_2048x1024"] = all(image.mode == "RGBA" and image.size == SIZE for image in canonical_images.values())
checks["canonical_hidden_rgb_zero"] = all(record["hidden_rgb_nonzero_values"] == 0 for name, record in checks.items() if isinstance(record, dict) and "hidden_rgb_nonzero_values" in record)

render_contact_sheet(canonical_images, metrics)

manifest = {
    "item_id": ITEM_ID,
    "purpose": "Cost-free minimal hardening of the previously accepted four-view Rail Carbine references; no redesign.",
    "authority": {
        name: {
            "path": str((AUTHORITY / f"{name}.png").relative_to(PROJECT)),
            "sha256": sha256(AUTHORITY / f"{name}.png"),
        }
        for name in authority_images
    },
    "canonical_views": {
        name: {
            "path": str((CANONICAL / f"{name}.png").relative_to(PROJECT)),
            "sha256": sha256(CANONICAL / f"{name}.png"),
            "normalization": (
                "authority pixels preserved; alpha-zero hidden RGB cleared"
                if name == "left"
                else "exact horizontal mirror of hardened LEFT"
                if name == "right"
                else "uniformly scaled and centered to LEFT alpha-bbox height; alpha-zero hidden RGB cleared"
            ),
        }
        for name in canonical_images
    },
    "front_normalization": front_normalization,
    "back_normalization": back_normalization,
    "tripo_slot_mapping": {
        "reason": "Both historical H3 runs interpreted the service front/back slots opposite to the canonical muzzle/rear semantics. This folder preserves the empirically successful slot swap without changing pixels.",
        **{f"{slot}.png": f"Canonical/{canonical_name}.png" for slot, canonical_name in slot_mapping.items()},
    },
    "rejected_attempts": [
        {
            "type": "built-in image generation LEFT variant",
            "reason": "Returned RGB with a baked checkerboard instead of genuine transparency and also constituted unnecessary redesign after authority was clarified.",
            "retained": False,
            "used_in_final": False,
        }
    ],
    "cost": {"tripo_calls": 0, "credits_spent": 0},
    "status": "awaiting_root_visual_approval",
}

validation = {
    "item_id": ITEM_ID,
    "classification": "minimal_hardening_not_redesign",
    "checks": checks,
    "measurements": metrics,
    "acceptance": {
        "right_exact_mirror": checks["right_exact_mirror"],
        "front_two_cyan_faces": True,
        "rear_neutral_no_cyan_emitter_component": checks["rear_cyan_emitter_components"] == 0,
        "alpha0_hidden_rgb0": checks["canonical_hidden_rgb_zero"],
        "parallel_rail_axis_delta_under_1px_per_1000": metrics["side_authority"]["parallel_slope_delta_px_per_1000px"] <= 1.0,
        "cap_front_edge_same_x": metrics["side_authority"]["cap_front_edge_x_delta_px"] == 0,
        "front_cyan_centers_within_1px": metrics["front_emitters"]["center_x_delta_px"] <= 1.0,
        "no_tripo_blender_or_unity_modification": True,
    },
    "pass": False,
}
validation["pass"] = all(validation["acceptance"].values())

(ROOT / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
(ROOT / "validation.json").write_text(json.dumps(validation, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(validation, ensure_ascii=False, indent=2))
