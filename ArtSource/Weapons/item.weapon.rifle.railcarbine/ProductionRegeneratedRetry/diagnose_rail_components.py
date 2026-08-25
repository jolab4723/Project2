from __future__ import annotations

import json
from array import array
from collections import defaultdict
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(bpy.data.filepath).resolve().parent
BODY_NAME = "RailCarbine_Regenerated_Body"
REPORT_PATH = ROOT / "QA" / "rail_component_diagnosis.json"


class DisjointSet:
    def __init__(self, count: int) -> None:
        self.parent = list(range(count))
        self.rank = [0] * count

    def find(self, index: int) -> int:
        parent = self.parent
        while parent[index] != index:
            parent[index] = parent[parent[index]]
            index = parent[index]
        return index

    def union(self, a: int, b: int) -> None:
        a = self.find(a)
        b = self.find(b)
        if a == b:
            return
        if self.rank[a] < self.rank[b]:
            a, b = b, a
        self.parent[b] = a
        if self.rank[a] == self.rank[b]:
            self.rank[a] += 1


def rounded(values, digits: int = 9) -> list[float]:
    return [round(float(value), digits) for value in values]


body = bpy.data.objects[BODY_NAME]
mesh = body.data
mesh.calc_loop_triangles()

sets = DisjointSet(len(mesh.vertices))
for edge in mesh.edges:
    sets.union(edge.vertices[0], edge.vertices[1])

vertices_by_component: dict[int, list[int]] = defaultdict(list)
for vertex in mesh.vertices:
    vertices_by_component[sets.find(vertex.index)].append(vertex.index)

faces_by_component: dict[int, list[int]] = defaultdict(list)
for polygon in mesh.polygons:
    component = sets.find(polygon.vertices[0])
    assert all(sets.find(index) == component for index in polygon.vertices)
    faces_by_component[component].append(polygon.index)

edge_face_counts = [0] * len(mesh.edges)
for polygon in mesh.polygons:
    for edge_index in polygon.edge_keys:
        # edge_keys contains vertex pairs, not mesh edge indices.
        pass

edge_lookup = {tuple(sorted(edge.vertices)): edge.index for edge in mesh.edges}
for polygon in mesh.polygons:
    vertices = list(polygon.vertices)
    for i, start in enumerate(vertices):
        key = tuple(sorted((start, vertices[(i + 1) % len(vertices)])))
        edge_face_counts[edge_lookup[key]] += 1

component_edges: dict[int, list[int]] = defaultdict(list)
for edge in mesh.edges:
    component_edges[sets.find(edge.vertices[0])].append(edge.index)

components = []
for root_index, vertex_indices in vertices_by_component.items():
    coordinates = [mesh.vertices[index].co.copy() for index in vertex_indices]
    minimum = Vector((min(v.x for v in coordinates), min(v.y for v in coordinates), min(v.z for v in coordinates)))
    maximum = Vector((max(v.x for v in coordinates), max(v.y for v in coordinates), max(v.z for v in coordinates)))
    centroid = sum(coordinates, Vector()) / len(coordinates)
    face_indices = faces_by_component[root_index]
    edge_indices = component_edges[root_index]
    boundary_edges = sum(1 for index in edge_indices if edge_face_counts[index] == 1)
    non_manifold_edges = sum(1 for index in edge_indices if edge_face_counts[index] != 2)
    dimensions = maximum - minimum
    components.append(
        {
            "id": root_index,
            "vertices": len(vertex_indices),
            "edges": len(edge_indices),
            "faces": len(face_indices),
            "triangles": sum(len(mesh.polygons[index].vertices) - 2 for index in face_indices),
            "boundary_edges": boundary_edges,
            "non_manifold_edges": non_manifold_edges,
            "min": rounded(minimum),
            "max": rounded(maximum),
            "dimensions": rounded(dimensions),
            "centroid": rounded(centroid),
        }
    )

components.sort(key=lambda item: (-item["triangles"], item["id"]))

emission_path = ROOT / "Textures" / "item.weapon.rifle.railcarbine_Emission.png"
emission_image = bpy.data.images.load(str(emission_path), check_existing=True)
width, height = emission_image.size
pixels = array("f", [0.0]) * (width * height * 4)
emission_image.pixels.foreach_get(pixels)
uv_layer = mesh.uv_layers.active.data


def loop_is_emissive(loop_index: int) -> bool:
    uv = uv_layer[loop_index].uv
    x = min(width - 1, max(0, int((uv.x % 1.0) * width)))
    y = min(height - 1, max(0, int((uv.y % 1.0) * height)))
    offset = (y * width + x) * 4
    return pixels[offset] > 0.5


emissive_faces_by_component: dict[int, list[int]] = defaultdict(list)
emissive_face_centers = []
for polygon in mesh.polygons:
    emissive_loops = sum(loop_is_emissive(index) for index in polygon.loop_indices)
    if emissive_loops * 2 < len(polygon.loop_indices):
        continue
    component = sets.find(polygon.vertices[0])
    emissive_faces_by_component[component].append(polygon.index)
    emissive_face_centers.append((polygon.index, polygon.center.copy()))

for component in components:
    emissive_faces = emissive_faces_by_component[component["id"]]
    component["emissive_faces"] = len(emissive_faces)
    component["emissive_fraction"] = round(
        len(emissive_faces) / component["faces"] if component["faces"] else 0.0,
        9,
    )

emissive_components = [component for component in components if component["emissive_faces"]]

# Quantify the long twin-rail envelope in longitudinal bins without changing topology.
rail_bins = []
for low_z in [0.15 + 0.025 * i for i in range(11)]:
    high_z = low_z + 0.025
    indices = [
        vertex.index
        for vertex in mesh.vertices
        if low_z <= vertex.co.z < high_z and vertex.co.y >= 0.045
    ]
    if not indices:
        continue
    coordinates = [mesh.vertices[index].co for index in indices]
    rail_bins.append(
        {
            "z_range": rounded((low_z, high_z), 6),
            "vertices": len(indices),
            "x_min_max": rounded((min(v.x for v in coordinates), max(v.x for v in coordinates))),
            "y_min_max": rounded((min(v.y for v in coordinates), max(v.y for v in coordinates))),
            "x_center": round(sum(v.x for v in coordinates) / len(coordinates), 9),
            "y_center": round(sum(v.y for v in coordinates) / len(coordinates), 9),
        }
    )

emissive_centerline_bins = []
for low_z in [0.15 + 0.025 * i for i in range(12)]:
    high_z = low_z + 0.025
    entry = {"z_range": rounded((low_z, high_z), 6)}
    for name, low_y, high_y in (
        ("lower", 0.025, 0.078),
        ("upper", 0.078, 0.12),
    ):
        centers = [
            center
            for _, center in emissive_face_centers
            if low_z <= center.z < high_z and low_y <= center.y < high_y
        ]
        entry[name] = (
            {
                "faces": len(centers),
                "center": rounded(sum(centers, Vector()) / len(centers)),
                "x_min_max": rounded((min(v.x for v in centers), max(v.x for v in centers))),
                "y_min_max": rounded((min(v.y for v in centers), max(v.y for v in centers))),
            }
            if centers
            else None
        )
    emissive_centerline_bins.append(entry)

report = {
    "item_id": "item.weapon.rifle.railcarbine",
    "mesh": BODY_NAME,
    "totals": {
        "vertices": len(mesh.vertices),
        "edges": len(mesh.edges),
        "faces": len(mesh.polygons),
        "triangles": len(mesh.loop_triangles),
        "uv_layers": len(mesh.uv_layers),
        "connected_components": len(components),
    },
    "components": components,
    "emissive_components": emissive_components,
    "longitudinal_upper_envelope_bins": rail_bins,
    "emissive_centerline_bins": emissive_centerline_bins,
}
REPORT_PATH.parent.mkdir(parents=True, exist_ok=True)
REPORT_PATH.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(report, ensure_ascii=False, indent=2))
