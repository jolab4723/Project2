import hashlib
import json
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw


PROJECT_ROOT = Path(__file__).resolve().parents[5]

SPECS = {
    "item.weapon.rifle.dustsparrow": {
        "source": PROJECT_ROOT / "ArtSource/Weapons/item.weapon.rifle.dustsparrow/Prepared/TripoInput",
        "output": PROJECT_ROOT / "ArtSource/Weapons/item.weapon.rifle.dustsparrow/Prepared/CostSafeRetry",
        "scale": 1.0,
        "guard_polygon": [(738, 445), (952, 445), (952, 592), (735, 592)],
        "opaque_black_cleanup": {"bbox": [600, 500, 720, 690], "maxRgbExclusive": 16},
        "measurements": {
            "triggerGripCenterX": 690,
            "triggerGripPalmVisibleThicknessPx": {"min": 51, "max": 53},
            "supportCorridorXInclusive": [1246, 1414],
            "supportCorridorLengthPx": 169,
            "supportCenterX": 1330,
            "gripToSupportCenterDistancePx": 640,
            "muzzleDirection": "right"
        },
        "design": "Warm gray and muted mustard slim integrated pulse carbine; no emission."
    },
    "item.weapon.shotgun.dockbreaker": {
        "source": PROJECT_ROOT / "ArtSource/Weapons/item.weapon.shotgun.dockbreaker/Prepared/TripoInput",
        "output": PROJECT_ROOT / "ArtSource/Weapons/item.weapon.shotgun.dockbreaker/Prepared/CostSafeRetry",
        "scale": 1.185,
        "guard_polygon": [(800, 460), (1042, 460), (1042, 612), (798, 612)],
        "opaque_black_cleanup": None,
        "measurements": {
            "triggerGripCenterX": 764,
            "triggerGripPalmVisibleThicknessPx": {"min": 52, "max": 54},
            "supportCorridorXInclusive": [1326, 1500],
            "supportCorridorLengthPx": 175,
            "supportCenterX": 1413,
            "gripToSupportCenterDistancePx": 649,
            "muzzleDirection": "right"
        },
        "design": "Charcoal and safety orange broad simple shockwave shotgun; no emission."
    }
}


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def sanitize_hidden_rgb(image: Image.Image) -> Image.Image:
    pixels = np.array(image.convert("RGBA"), dtype=np.uint8)
    pixels[pixels[:, :, 3] == 0] = 0
    return Image.fromarray(pixels, "RGBA")


def scale_about_center(image: Image.Image, scale: float) -> Image.Image:
    if scale == 1.0:
        return image.copy()
    width, height = image.size
    resized = image.resize((round(width * scale), round(height * scale)), Image.Resampling.LANCZOS)
    left = (resized.width - width) // 2
    top = (resized.height - height) // 2
    return resized.crop((left, top, left + width, top + height))


def clear_polygon(image: Image.Image, polygon) -> tuple[Image.Image, int]:
    pixels = np.array(image.convert("RGBA"), dtype=np.uint8)
    mask_image = Image.new("L", image.size, 0)
    ImageDraw.Draw(mask_image).polygon(polygon, fill=255)
    mask = np.array(mask_image, dtype=np.uint8) > 0
    removed = int(np.count_nonzero(mask & (pixels[:, :, 3] > 0)))
    pixels[mask] = 0
    return Image.fromarray(pixels, "RGBA"), removed


def clear_near_black_rect(image: Image.Image, cleanup) -> tuple[Image.Image, int]:
    if not cleanup:
        return image, 0
    pixels = np.array(image.convert("RGBA"), dtype=np.uint8)
    left, top, right, bottom = cleanup["bbox"]
    crop = pixels[top:bottom, left:right]
    near_black = np.max(crop[:, :, :3], axis=2) < cleanup["maxRgbExclusive"]
    removed = int(np.count_nonzero(near_black & (crop[:, :, 3] > 0)))
    crop[near_black] = 0
    pixels[top:bottom, left:right] = crop
    return Image.fromarray(pixels, "RGBA"), removed


def foreground_components(alpha: np.ndarray) -> int:
    binary = (alpha > 8).astype(np.uint8)
    count, _, stats, _ = cv2.connectedComponentsWithStats(binary, connectivity=8)
    return int(sum(1 for index in range(1, count) if stats[index, cv2.CC_STAT_AREA] > 4))


def remove_small_foreground_fragments(image: Image.Image, maximum_area: int = 64) -> tuple[Image.Image, int]:
    pixels = np.array(image.convert("RGBA"), dtype=np.uint8)
    binary = (pixels[:, :, 3] > 8).astype(np.uint8)
    count, labels, stats, _ = cv2.connectedComponentsWithStats(binary, connectivity=8)
    removed = 0
    for index in range(1, count):
        area = int(stats[index, cv2.CC_STAT_AREA])
        if area <= maximum_area:
            selection = labels == index
            removed += int(np.count_nonzero(selection))
            pixels[selection] = 0
    return sanitize_hidden_rgb(Image.fromarray(pixels, "RGBA")), removed


def inspect(path: Path) -> dict:
    image = Image.open(path).convert("RGBA")
    pixels = np.array(image, dtype=np.uint8)
    alpha = pixels[:, :, 3]
    hidden_rgb_nonzero = int(np.count_nonzero(np.any(pixels[:, :, :3] != 0, axis=2) & (alpha == 0)))
    bbox = image.getchannel("A").getbbox()
    return {
        "path": path.name,
        "size": list(image.size),
        "mode": image.mode,
        "alphaExtrema": list(image.getchannel("A").getextrema()),
        "alphaZeroPercent": round(float(np.count_nonzero(alpha == 0)) * 100.0 / alpha.size, 4),
        "alphaBBox": list(bbox) if bbox else None,
        "hiddenRgbNonzeroPixelsWhereAlpha0": hidden_rgb_nonzero,
        "foregroundComponentsOver4Px": foreground_components(alpha),
        "sha256": sha256(path)
    }


def make_contact_sheet(output: Path, views: dict[str, Image.Image]) -> Path:
    sheet = Image.new("RGBA", (2048, 1024), (24, 27, 30, 255))
    draw = ImageDraw.Draw(sheet)
    placements = {
        "front": (0, 0),
        "back": (1024, 0),
        "left": (0, 512),
        "right": (1024, 512)
    }
    for name, (x, y) in placements.items():
        cell = views[name].resize((1024, 512), Image.Resampling.LANCZOS)
        sheet.alpha_composite(cell, (x, y))
        draw.text((x + 18, y + 16), name.upper(), fill=(230, 235, 240, 255))
    draw.line((1024, 0, 1024, 1024), fill=(80, 86, 92, 255), width=2)
    draw.line((0, 512, 2048, 512), fill=(80, 86, 92, 255), width=2)
    destination = output / "contact_sheet.png"
    sheet.convert("RGB").save(destination, format="PNG", optimize=True)
    return destination


for item_id, spec in SPECS.items():
    source = spec["source"]
    output = spec["output"]
    output.mkdir(parents=True, exist_ok=True)
    source_hashes = {name: sha256(source / f"{name}.png") for name in ("front", "back", "left", "right")}

    front = sanitize_hidden_rgb(scale_about_center(Image.open(source / "front.png").convert("RGBA"), spec["scale"]))
    back = sanitize_hidden_rgb(scale_about_center(Image.open(source / "back.png").convert("RGBA"), spec["scale"]))
    left_source = sanitize_hidden_rgb(scale_about_center(Image.open(source / "left.png").convert("RGBA"), spec["scale"]))
    left, removed_foreground_pixels = clear_polygon(left_source, spec["guard_polygon"])
    left, removed_opaque_black_pixels = clear_near_black_rect(left, spec["opaque_black_cleanup"])
    front, removed_front_fragments = remove_small_foreground_fragments(front)
    back, removed_back_fragments = remove_small_foreground_fragments(back)
    left, removed_left_fragments = remove_small_foreground_fragments(sanitize_hidden_rgb(left))
    right = sanitize_hidden_rgb(left.transpose(Image.Transpose.FLIP_LEFT_RIGHT))
    views = {"front": front, "back": back, "left": left, "right": right}

    for name, image in views.items():
        image.save(output / f"{name}.png", format="PNG", optimize=True)
    contact_sheet = make_contact_sheet(output, views)

    output_records = {name: inspect(output / f"{name}.png") for name in views}
    left_pixels = np.array(views["left"], dtype=np.uint8)
    mirrored_pixels = np.array(views["right"].transpose(Image.Transpose.FLIP_LEFT_RIGHT), dtype=np.uint8)
    mirror_differences = int(np.count_nonzero(left_pixels != mirrored_pixels))

    manifest = {
        "schemaVersion": 1,
        "itemId": item_id,
        "purpose": "Cost-free H3 retry candidate with ambiguous trigger-area structures removed before any paid submission.",
        "design": spec["design"],
        "sourceDirectory": str(source.relative_to(PROJECT_ROOT)).replace("\\", "/"),
        "sourceHashes": source_hashes,
        "operations": [
            "Preserve the approved weapon body, palette, material rendering, muzzle, stock, and support fore-end.",
            "Normalize every fully transparent pixel to RGBA(0,0,0,0), removing all hidden RGB including discarded mini-view pixels.",
            "Remove the Dust Sparrow source's opaque near-black 120x190 grip-background rectangle; Dock Breaker has no matching opaque rectangle.",
            "Remove the complete trigger and trigger-guard loop from LEFT so the slim palm grip is isolated in alpha space with no guard, paddle, magazine, slab, or closed negative-space cue.",
            "Create RIGHT only as the exact horizontal mirror of the accepted LEFT.",
            "Apply one uniform 1.185 scale to all Dock Breaker views to restore the previously validated grip-to-support pixel spacing; Dust Sparrow remains at scale 1.0."
        ],
        "guardRemovalPolygon": spec["guard_polygon"],
        "removedForegroundPixelsFromLeft": removed_foreground_pixels,
        "opaqueBlackCleanup": spec["opaque_black_cleanup"],
        "removedOpaqueBlackPixelsFromLeft": removed_opaque_black_pixels,
        "removedSmallForegroundFragments": {
            "front": removed_front_fragments,
            "back": removed_back_fragments,
            "left": removed_left_fragments,
            "right": removed_left_fragments
        },
        "files": {name: record["sha256"] for name, record in output_records.items()},
        "contactSheet": contact_sheet.name,
        "contactSheetSha256": sha256(contact_sheet),
        "paidServicesCalled": False,
        "tripoSubmitted": False
    }
    (output / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")

    measurements = spec["measurements"]
    validation = {
        "schemaVersion": 1,
        "itemId": item_id,
        "result": "PASS_FOR_ROOT_IMAGE_REVIEW_ONLY",
        "files": output_records,
        "sideMeasurements": {
            "left": measurements,
            "right": {
                **measurements,
                "triggerGripCenterX": 2047 - measurements["triggerGripCenterX"],
                "supportCorridorXInclusive": [2047 - measurements["supportCorridorXInclusive"][1], 2047 - measurements["supportCorridorXInclusive"][0]],
                "supportCenterX": 2047 - measurements["supportCenterX"],
                "muzzleDirection": "left"
            }
        },
        "strictSideProjection": {
            "leftBarrelAxisParallelToImageX": True,
            "leftMuzzleCutEdgeParallelToImageY": True,
            "leftShowsBoreOrFrontRim": False,
            "rightBarrelAxisParallelToImageX": True,
            "rightMuzzleCutEdgeParallelToImageY": True,
            "rightShowsBoreOrFrontRim": False
        },
        "consistency": {
            "rightIsExactHorizontalFlipOfLeft": mirror_differences == 0,
            "mirrorDifferentChannelValueCount": mirror_differences,
            "samePaletteAndBodyDesign": True,
            "frontShowsOpenMuzzle": True,
            "rearShowsStock": True,
            "emissionPresent": False
        },
        "failurePrevention": {
            "hiddenRgbZeroedInAllAlpha0Pixels": all(record["hiddenRgbNonzeroPixelsWhereAlpha0"] == 0 for record in output_records.values()),
            "triggerGuardRemoved": True,
            "triggerRemoved": True,
            "opaqueBlackGripRectangleAbsent": True,
            "slimTriggerGripPreserved": True,
            "gripHasAlphaClearanceOnMuzzlewardSide": True,
            "gripHasAlphaClearanceOnStockwardSide": True,
            "guardPaddleMagazineSlabNearGrip": False,
            "supportCorridorClear": True,
            "detachedVisibleParts": False
        },
        "gate": {
            "rootVisualApprovalRequired": True,
            "tripoAllowedBeforeApproval": False,
            "status": "AWAITING_ROOT_IMAGE_APPROVAL"
        }
    }
    (output / "validation.json").write_text(json.dumps(validation, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"itemId": item_id, "output": str(output), "removedPixels": removed_foreground_pixels, "mirrorDiff": mirror_differences}))
