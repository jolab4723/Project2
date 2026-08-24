from __future__ import annotations

import json
from pathlib import Path

import bpy


ROOT = Path(__file__).resolve().parents[1]
TEXTURES = ROOT / "Production" / "Textures"
QA = ROOT / "Production" / "QA"
ITEM_ID = "item.weapon.rifle.railcarbine"


def load_pixels(path: Path):
    image = bpy.data.images.load(str(path), check_existing=False)
    width, height = image.size
    pixels = list(image.pixels[:])
    return width, height, pixels


def sample(width: int, height: int, pixels: list[float], u: float, v: float) -> tuple[float, float, float]:
    x = max(0, min(width - 1, round((u % 1.0) * (width - 1))))
    y = max(0, min(height - 1, round((v % 1.0) * (height - 1))))
    offset = (y * width + x) * 4
    return pixels[offset], pixels[offset + 1], pixels[offset + 2]


body = bpy.data.objects.get("RailCarbine_Body")
if body is None:
    raise RuntimeError("RailCarbine_Body is missing")
uv_layer = body.data.uv_layers.get("UVMap")
if uv_layer is None:
    raise RuntimeError("UVMap is missing")

base_width, base_height, base_pixels = load_pixels(TEXTURES / f"{ITEM_ID}_BaseColor.png")
emission_width, emission_height, emission_pixels = load_pixels(TEXTURES / f"{ITEM_ID}_Emission.png")

z_min, z_max = -0.265, -0.015
bin_count = 16
bins = [{"index": index, "expected_cyan_faces": 0, "emission_faces": 0} for index in range(bin_count)]
cyan_centers = []
for polygon in body.data.polygons:
    center = polygon.center
    if not (z_min <= center.z <= z_max and -0.060 <= center.y <= 0.170):
        continue
    loops = list(polygon.loop_indices)
    if not loops:
        continue
    u = sum(uv_layer.data[index].uv.x for index in loops) / len(loops)
    v = sum(uv_layer.data[index].uv.y for index in loops) / len(loops)
    red, green, blue = sample(base_width, base_height, base_pixels, u, v)
    maximum, minimum = max(red, green, blue), min(red, green, blue)
    saturation = (maximum - minimum) / max(maximum, 1.0e-6)
    cyan = maximum > 0.45 and saturation > 0.45 and green > red * 1.50 and blue > red * 1.55
    if not cyan:
        continue
    cyan_centers.append([float(center.x), float(center.y), float(center.z)])
    bin_index = min(bin_count - 1, int((center.z - z_min) / (z_max - z_min) * bin_count))
    bins[bin_index]["expected_cyan_faces"] += 1
    emission = sample(emission_width, emission_height, emission_pixels, u, v)
    if max(emission) > 0.2:
        bins[bin_index]["emission_faces"] += 1

for record in bins:
    expected = record["expected_cyan_faces"]
    record["coverage"] = round(record["emission_faces"] / expected, 6) if expected else None

report = {
    "item_id": ITEM_ID,
    "method": "sample UV0 at every cyan-dominant face centroid in the physical twin-rail envelope and measure emission coverage in 16 longitudinal bins",
    "rail_z_range": [z_min, z_max],
    "bins": bins,
    # Face-centroid samples include anti-aliased UV borders, so demanding every
    # cyan face would reject visibly continuous strips. Requiring at least 85%
    # in every occupied segment still catches a missing or fading rail section.
    "minimum_bin_coverage_required": 0.85,
    "continuous_all_nonempty_bins": all(record["expected_cyan_faces"] == 0 or record["coverage"] >= 0.85 for record in bins),
    "cyan_face_count": len(cyan_centers),
    "cyan_center_bounds": {
        "min": [min(point[axis] for point in cyan_centers) for axis in range(3)],
        "max": [max(point[axis] for point in cyan_centers) for axis in range(3)],
    } if cyan_centers else None,
    "cyan_center_quantiles": {
        axis_name: {
            str(percent): sorted(point[axis] for point in cyan_centers)[round((len(cyan_centers) - 1) * percent / 100.0)]
            for percent in (0, 10, 25, 50, 75, 90, 100)
        }
        for axis, axis_name in enumerate(("x", "y", "z"))
    } if cyan_centers else None,
}
(QA / "emission_continuity_audit.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(report, ensure_ascii=False, indent=2))
