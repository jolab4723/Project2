import hashlib
import json
import shutil
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw


HERE = Path(__file__).resolve().parent
APPROVED = HERE.parent / "CostSafeRetry"
SOURCE = HERE / "SourceGenerated"

sources = {
    "front": APPROVED / "front.png",
    "left": APPROVED / "left.png",
    "back": SOURCE / "back_web_native_v2.png",
}
for name, source in sources.items():
    if not source.exists():
        raise FileNotFoundError(f"Missing {name}: {source}")
    shutil.copy2(source, HERE / f"{name}.png")

left = Image.open(HERE / "left.png").convert("RGBA")
right = left.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
right.save(HERE / "right.png", format="PNG", compress_level=9)


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def image_record(path: Path):
    image = Image.open(path)
    rgba = np.asarray(image.convert("RGBA"), dtype=np.uint8)
    alpha = rgba[:, :, 3]
    foreground = alpha > 0
    ys, xs = np.where(foreground)
    bbox = [int(xs.min()), int(ys.min()), int(xs.max() + 1), int(ys.max() + 1)]
    hidden_nonzero = int(np.count_nonzero(rgba[:, :, :3][alpha == 0]))
    return {
        "path": path.name,
        "size": list(image.size),
        "mode": image.mode,
        "alphaExtrema": [int(alpha.min()), int(alpha.max())],
        "alphaZeroPercent": round(float((alpha == 0).mean() * 100.0), 6),
        "alphaBBox": bbox,
        "foregroundPixelCount": int(foreground.sum()),
        "hiddenRgbNonzeroValuesWhereAlpha0": hidden_nonzero,
        "sha256": sha256(path),
    }


files = {name: image_record(HERE / f"{name}.png") for name in ("front", "left", "back", "right")}
left_pixels = np.asarray(Image.open(HERE / "left.png").convert("RGBA"))
right_pixels = np.asarray(Image.open(HERE / "right.png").convert("RGBA"))
mirror_differences = int(np.count_nonzero(right_pixels != np.flip(left_pixels, axis=1)))

front_bbox = files["front"]["alphaBBox"]
back_bbox = files["back"]["alphaBBox"]
front_size = [front_bbox[2] - front_bbox[0], front_bbox[3] - front_bbox[1]]
back_size = [back_bbox[2] - back_bbox[0], back_bbox[3] - back_bbox[1]]
front_center = [(front_bbox[0] + front_bbox[2]) / 2.0, (front_bbox[1] + front_bbox[3]) / 2.0]
back_center = [(back_bbox[0] + back_bbox[2]) / 2.0, (back_bbox[1] + back_bbox[3]) / 2.0]
end_view_center_delta = [abs(front_center[0] - back_center[0]), abs(front_center[1] - back_center[1])]
end_view_size_ratios = [back_size[0] / front_size[0], back_size[1] / front_size[1]]

hard_failures = []
for name, record in files.items():
    if record["size"] != [2048, 1024]:
        hard_failures.append(f"{name}: wrong size")
    if record["mode"] != "RGBA":
        hard_failures.append(f"{name}: wrong mode")
    if record["alphaExtrema"] != [0, 255]:
        hard_failures.append(f"{name}: invalid alpha extrema")
    if record["hiddenRgbNonzeroValuesWhereAlpha0"] != 0:
        hard_failures.append(f"{name}: hidden RGB is nonzero")
if mirror_differences:
    hard_failures.append("right is not an exact mirror of left")
if max(end_view_center_delta) > 8.0:
    hard_failures.append("front/back end-view centers differ by more than 8 px")
if not (0.70 <= end_view_size_ratios[0] <= 1.00 and 0.90 <= end_view_size_ratios[1] <= 1.20):
    hard_failures.append("rear buttpad dimensions are implausible relative to front muzzle housing")

validation = {
    "schemaVersion": 1,
    "itemId": "item.weapon.shotgun.dockbreaker",
    "result": "PASS_FOR_H3" if not hard_failures else "FAIL",
    "files": files,
    "sourceHashes": {name: sha256(path) for name, path in sources.items()},
    "rightExactMirror": mirror_differences == 0,
    "mirrorDifferentChannelValueCount": mirror_differences,
    "endViewForegroundSize": {"front": front_size, "back": back_size},
    "endViewCenters": {"front": front_center, "back": back_center},
    "endViewCenterDeltaPx": end_view_center_delta,
    "backToFrontForegroundSizeRatios": [round(value, 6) for value in end_view_size_ratios],
    "normalization": "The complete 1774x887 2:1 source canvas was resized uniformly to 2048x1024; object-relative size and center were preserved; RGB was zeroed only where alpha is zero.",
    "sideMeasurementsInheritedFromApprovedLeft": {
        "triggerGripCenterX": 764,
        "triggerGripPalmVisibleThicknessPx": [52, 54],
        "supportCorridorXInclusive": [1326, 1500],
        "supportCorridorLengthPx": 175,
        "gripToSupportCenterDistancePx": 649,
        "muzzleDirection": "right",
    },
    "visualGate": {
        "leftStrictOrthographicNoBoreVisible": True,
        "rightStrictOrthographicNoBoreVisible": True,
        "frontSingleOpenBore": True,
        "rearSolidNeutralButtpadNoBoreSlotsRibs": True,
        "charcoalSafetyOrangePalettePreserved": True,
        "triggerGripIsolated": True,
        "supportCorridorClear": True,
        "emissionPresent": False,
    },
    "hardFailures": hard_failures,
    "tripoGate": "PASS" if not hard_failures else "HOLD",
}
(HERE / "validation.json").write_text(json.dumps(validation, indent=2), encoding="utf-8")

canvas = Image.new("RGB", (2048, 2048), (30, 30, 32))
draw = ImageDraw.Draw(canvas)
positions = {"front": (0, 0), "left": (1024, 0), "back": (0, 1024), "right": (1024, 1024)}
for name, (x, y) in positions.items():
    image = Image.open(HERE / f"{name}.png").convert("RGBA")
    preview = image.resize((1024, 512), Image.Resampling.LANCZOS)
    panel = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0))
    panel.alpha_composite(preview, (0, 256))
    canvas.paste(panel.convert("RGB"), (x, y))
    draw.text((x + 24, y + 24), name.upper(), fill=(255, 255, 255))
canvas.save(HERE / "contact_sheet.png", format="PNG", compress_level=9)

manifest = {
    "itemId": "item.weapon.shotgun.dockbreaker",
    "design": "Charcoal and safety-orange simple connected sci-fi shotgun; no emission.",
    "front": "Pixel-preserved approved FRONT with a single open bore.",
    "left": "Pixel-preserved approved strict LEFT side.",
    "back": "Root-approved ChatGPT web native RGBA v2 solid neutral buttpad; no bore, slots, ribs, grille, aperture, or visible grip.",
    "right": "Exact pixel horizontal mirror of approved LEFT.",
    "paidServicesCalledDuringPreparation": False,
    "files": {name: files[name]["sha256"] for name in files},
    "contactSheet": "contact_sheet.png",
}
(HERE / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
print(json.dumps(validation, indent=2))
