from __future__ import annotations

import json
from pathlib import Path

import bpy
from mathutils import Vector


PRODUCTION = Path(__file__).resolve().parent
QA = PRODUCTION / "QA"
BASE_COLOR = PRODUCTION / "Textures" / "item.weapon.rifle.railcarbine_BaseColor.png"
OUTPUT = QA / "muzzle_uv_candidates.json"


def srgb_pixel(image: bpy.types.Image, uv: Vector) -> list[int]:
    width, height = image.size
    x = min(width - 1, max(0, round((uv.x % 1.0) * (width - 1))))
    y = min(height - 1, max(0, round((uv.y % 1.0) * (height - 1))))
    offset = (y * width + x) * 4
    rgba = image.pixels[offset : offset + 4]
    return [round(float(channel) * 255) for channel in rgba[:3]]


def main() -> None:
    body = bpy.data.objects["RailCarbine_Body"]
    mesh = body.data
    mesh.calc_loop_triangles()
    uv_layer = mesh.uv_layers.active
    if uv_layer is None:
        raise RuntimeError("RailCarbine_Body has no active UV map")

    image = bpy.data.images.load(str(BASE_COLOR), check_existing=False)
    maximum_z = max(vertex.co.z for vertex in mesh.vertices)
    records = []
    for polygon in mesh.polygons:
        center = polygon.center
        if center.z < maximum_z - 0.035 or polygon.normal.z < 0.35:
            continue
        uvs = [uv_layer.data[index].uv.copy() for index in polygon.loop_indices]
        uv_center = sum(uvs, Vector((0.0, 0.0))) / len(uvs)
        records.append(
            {
                "polygon": polygon.index,
                "center": [round(float(value), 6) for value in center],
                "normal": [round(float(value), 6) for value in polygon.normal],
                "area": round(float(polygon.area), 9),
                "uv_center": [round(float(value), 7) for value in uv_center],
                "uv_min": [round(min(float(uv[i]) for uv in uvs), 7) for i in range(2)],
                "uv_max": [round(max(float(uv[i]) for uv in uvs), 7) for i in range(2)],
                "sample_rgb": srgb_pixel(image, uv_center),
            }
        )

    records.sort(key=lambda record: (-record["center"][1], record["center"][0]))
    report = {
        "maximum_z": round(float(maximum_z), 6),
        "candidate_count": len(records),
        "candidates": records,
    }
    QA.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
