from __future__ import annotations

import json
import os
from pathlib import Path

import bpy
import numpy as np


ROOT = Path(__file__).resolve().parent
SOURCE = ROOT.parent / "Tripo" / "Downloaded" / "model.glb"
REPORT = ROOT / "QA" / "anchor_geometry_analysis.json"

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=os.fspath(SOURCE))
mesh_objects = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
points = np.asarray(
    [
        tuple(obj.matrix_world @ vertex.co)
        for obj in mesh_objects
        for vertex in obj.data.vertices
    ],
    dtype=np.float64,
)
minimum = points.min(axis=0)
maximum = points.max(axis=0)

front_slices = {}
for depth in (0.001, 0.0025, 0.005, 0.01, 0.02):
    sample = points[points[:, 0] <= minimum[0] + depth]
    front_slices[f"{depth:.4f}"] = {
        "count": int(len(sample)),
        "minimum": sample.min(axis=0).tolist() if len(sample) else None,
        "maximum": sample.max(axis=0).tolist() if len(sample) else None,
        "mean": sample.mean(axis=0).tolist() if len(sample) else None,
        "median": np.median(sample, axis=0).tolist() if len(sample) else None,
        "yz_bbox_center": (
            ((sample.min(axis=0) + sample.max(axis=0)) * 0.5)[1:].tolist()
            if len(sample)
            else None
        ),
    }

report = {
    "source": str(SOURCE.resolve()),
    "bounds_min": minimum.tolist(),
    "bounds_max": maximum.tolist(),
    "front_is_negative_x": True,
    "front_vertex_slices": front_slices,
}
REPORT.parent.mkdir(parents=True, exist_ok=True)
REPORT.write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps(report, indent=2))
