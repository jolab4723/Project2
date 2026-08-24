from __future__ import annotations

import json
from pathlib import Path

import bpy
from mathutils import Vector


PRODUCTION = Path(__file__).resolve().parent
BLEND = PRODUCTION / "item.weapon.rifle.railcarbine.blend"
TARGET_Z = 0.32625


def v3(value):
    return [round(float(value[i]), 6) for i in range(3)]


def main() -> None:
    bpy.ops.wm.open_mainfile(filepath=str(BLEND))
    root = bpy.data.objects["item.weapon.rifle.railcarbine_root"]
    mesh = bpy.data.objects["RailCarbine_Body"]
    marker = bpy.data.objects["LeftHandGrip"]
    vertices = [root.matrix_world.inverted() @ mesh.matrix_world @ vertex.co for vertex in mesh.data.vertices]
    section = [point for point in vertices if abs(point.z - TARGET_Z) <= 0.003]
    if not section:
        section = sorted(vertices, key=lambda point: abs(point.z - TARGET_Z))[:1000]
    current = root.matrix_world.inverted() @ marker.matrix_world.translation
    nearest = sorted(section, key=lambda point: (point - current).length)[:40]
    report = {
        "target_z": TARGET_Z,
        "section_vertex_count": len(section),
        "current_marker": v3(current),
        "nearest_section_vertices": [
            {"point": v3(point), "distance": round((point - current).length, 6)}
            for point in nearest
        ],
        "section_bounds": {
            "min": v3(Vector((min(point[i] for point in section) for i in range(3)))),
            "max": v3(Vector((max(point[i] for point in section) for i in range(3)))),
        },
    }
    output = PRODUCTION / "QA" / "left_grip_section_audit.json"
    output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
