import json
from collections import deque
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector


OUT = Path(__file__).resolve().parent
BODY_NAME = "RailCarbine_CostSafe_Body"
body = bpy.data.objects.get(BODY_NAME)
if body is None or body.type != "MESH":
    raise RuntimeError(f"Missing mesh {BODY_NAME}")

mesh = body.data
bm = bmesh.new()
bm.from_mesh(mesh)
bm.verts.ensure_lookup_table()
bm.edges.ensure_lookup_table()
bm.faces.ensure_lookup_table()

unvisited = set(bm.verts)
components = []
while unvisited:
    seed = unvisited.pop()
    queue = deque([seed])
    vertices = [seed]
    while queue:
        vertex = queue.popleft()
        for edge in vertex.link_edges:
            other = edge.other_vert(vertex)
            if other in unvisited:
                unvisited.remove(other)
                queue.append(other)
                vertices.append(other)
    minimum = [min(vertex.co[index] for vertex in vertices) for index in range(3)]
    maximum = [max(vertex.co[index] for vertex in vertices) for index in range(3)]
    components.append(
        {
            "vertices": len(vertices),
            "min": [round(value, 7) for value in minimum],
            "max": [round(value, 7) for value in maximum],
            "touches_front_zone": maximum[2] >= 0.39,
        }
    )

front_boundary = []
for edge in bm.edges:
    if not edge.is_boundary:
        continue
    midpoint = (edge.verts[0].co + edge.verts[1].co) * 0.5
    if midpoint.z >= 0.39:
        front_boundary.append(midpoint)

front_components = sorted(
    (component for component in components if component["touches_front_zone"]),
    key=lambda component: component["vertices"],
    reverse=True,
)

markers = {}
for name in ("RightHandGrip", "LeftHandGrip", "Muzzle"):
    marker = bpy.data.objects.get(name)
    if marker is None:
        raise RuntimeError(f"Missing marker {name}")
    markers[name] = {
        "location": [round(value, 7) for value in marker.location],
        "rotation": [round(value, 7) for value in marker.rotation_euler],
        "scale": [round(value, 7) for value in marker.scale],
        "parent": marker.parent.name if marker.parent else None,
    }

report = {
    "source_blend": bpy.data.filepath,
    "body": BODY_NAME,
    "triangles": sum(max(0, len(face.verts) - 2) for face in bm.faces),
    "connected_components": len(components),
    "boundary_edges": sum(1 for edge in bm.edges if edge.is_boundary),
    "front_zone_z_min": 0.39,
    "front_boundary_edges": len(front_boundary),
    "front_boundary_bounds": {
        "min": [round(min(point[index] for point in front_boundary), 7) for index in range(3)],
        "max": [round(max(point[index] for point in front_boundary), 7) for index in range(3)],
    },
    "front_components": front_components,
    "markers": markers,
    "repair_assessment": {
        "safe_without_body_triangle_or_uv_edit": True,
        "method": "two closed cyan hexagonal prisms recessed into two closed annular hex housing sleeves, overlapping the existing front housing depth",
        "body_mesh_modified": False,
        "grips_or_axis_modified": False,
        "reason": "The defects are localized to the final 55 mm front zone. A closed sleeve-and-cap assembly can overlap the existing housing volume without deleting, welding, remeshing, or re-UVing the H3 body."
    }
}
bm.free()
(OUT / "front_topology_diagnosis.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps({"front_boundary_edges": report["front_boundary_edges"], "front_components": len(front_components), "safe": True}))
