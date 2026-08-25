from __future__ import annotations

import json
from collections import defaultdict
from pathlib import Path

import bpy


PRODUCTION = Path(__file__).resolve().parent
OUTPUT = PRODUCTION / "QA" / "mesh_components.json"


def main() -> None:
    mesh = bpy.data.objects["RailCarbine_Body"].data
    parent = list(range(len(mesh.vertices)))

    def find(index: int) -> int:
        while parent[index] != index:
            parent[index] = parent[parent[index]]
            index = parent[index]
        return index

    def union(left: int, right: int) -> None:
        left_root, right_root = find(left), find(right)
        if left_root != right_root:
            parent[right_root] = left_root

    for edge in mesh.edges:
        union(edge.vertices[0], edge.vertices[1])

    vertices_by_root: dict[int, list[int]] = defaultdict(list)
    for vertex in mesh.vertices:
        vertices_by_root[find(vertex.index)].append(vertex.index)

    triangles_by_root: dict[int, int] = defaultdict(int)
    mesh.calc_loop_triangles()
    for triangle in mesh.loop_triangles:
        triangles_by_root[find(triangle.vertices[0])] += 1

    records = []
    for root, indices in vertices_by_root.items():
        points = [mesh.vertices[index].co for index in indices]
        minimum = [min(point[axis] for point in points) for axis in range(3)]
        maximum = [max(point[axis] for point in points) for axis in range(3)]
        records.append(
            {
                "root_vertex": root,
                "vertices": len(indices),
                "triangles": triangles_by_root[root],
                "min": [round(float(value), 6) for value in minimum],
                "max": [round(float(value), 6) for value in maximum],
                "center": [round(float((minimum[i] + maximum[i]) * 0.5), 6) for i in range(3)],
                "dimensions": [round(float(maximum[i] - minimum[i]), 6) for i in range(3)],
            }
        )
    records.sort(key=lambda record: record["triangles"], reverse=True)
    report = {"component_count": len(records), "components": records}
    OUTPUT.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
