from __future__ import annotations

import json
import os
from collections import deque
from pathlib import Path

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree


ROOT = Path(__file__).resolve().parent
SOURCE = ROOT.parent / "Tripo" / "Downloaded" / "model.glb"
REPORT = ROOT / "QA" / "muzzle_opening_analysis.json"

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=os.fspath(SOURCE))
mesh = next(obj for obj in bpy.context.scene.objects if obj.type == "MESH")
depsgraph = bpy.context.evaluated_depsgraph_get()
bvh = BVHTree.FromObject(mesh, depsgraph)

front_x = min((mesh.matrix_world @ vertex.co).x for vertex in mesh.data.vertices)
front_vertices = [
    mesh.matrix_world @ vertex.co
    for vertex in mesh.data.vertices
    if (mesh.matrix_world @ vertex.co).x <= front_x + 0.02
]
front_y_min = min(point.y for point in front_vertices)
front_y_max = max(point.y for point in front_vertices)
front_z_min = min(point.z for point in front_vertices)
front_z_max = max(point.z for point in front_vertices)
y_values = [-0.04 + index * 0.0005 for index in range(161)]
z_values = [-0.07 + index * 0.0005 for index in range(341)]
depths: list[list[float | None]] = []
open_grid: list[list[bool]] = []
for z in z_values:
    depth_row = []
    open_row = []
    for y in y_values:
        location, _normal, _face, _distance = bvh.ray_cast(
            Vector((front_x - 0.025, y, z)), Vector((1.0, 0.0, 0.0)), 1.2
        )
        depth = None if location is None else float(location.x - front_x)
        depth_row.append(depth)
        within_front_projection = (
            front_y_min <= y <= front_y_max and front_z_min <= z <= front_z_max
        )
        open_row.append(
            within_front_projection and depth is not None and depth >= 0.05
        )
    depths.append(depth_row)
    open_grid.append(open_row)

visited = [[False for _ in y_values] for _ in z_values]
components = []
for zi in range(len(z_values)):
    for yi in range(len(y_values)):
        if visited[zi][yi] or not open_grid[zi][yi]:
            continue
        queue = deque([(zi, yi)])
        visited[zi][yi] = True
        cells = []
        while queue:
            current_z, current_y = queue.popleft()
            cells.append((current_z, current_y))
            for dz, dy in ((-1, 0), (1, 0), (0, -1), (0, 1)):
                next_z = current_z + dz
                next_y = current_y + dy
                if not (0 <= next_z < len(z_values) and 0 <= next_y < len(y_values)):
                    continue
                if visited[next_z][next_y] or not open_grid[next_z][next_y]:
                    continue
                visited[next_z][next_y] = True
                queue.append((next_z, next_y))
        if len(cells) < 20:
            continue
        component_y = [y_values[cell_y] for _, cell_y in cells]
        component_z = [z_values[cell_z] for cell_z, _ in cells]
        component_depths = [depths[cell_z][cell_y] for cell_z, cell_y in cells]
        components.append(
            {
                "cell_count": len(cells),
                "y_min": min(component_y),
                "y_max": max(component_y),
                "z_min": min(component_z),
                "z_max": max(component_z),
                "grid_centroid_yz": [
                    sum(component_y) / len(component_y),
                    sum(component_z) / len(component_z),
                ],
                "bbox_center_yz": [
                    (min(component_y) + max(component_y)) * 0.5,
                    (min(component_z) + max(component_z)) * 0.5,
                ],
                "mean_first_hit_depth": sum(component_depths) / len(component_depths),
                "max_first_hit_depth": max(component_depths),
            }
        )

components.sort(key=lambda component: component["cell_count"], reverse=True)
report = {
    "source": str(SOURCE.resolve()),
    "front_axis": "-X",
    "front_x": front_x,
    "grid_spacing_m": 0.0005,
    "open_definition": "first hit at least 0.05m behind front plane",
    "front_0.02m_projection_bbox_yz": [
        front_y_min,
        front_y_max,
        front_z_min,
        front_z_max,
    ],
    "components": components[:12],
}
REPORT.write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps(report, indent=2))
