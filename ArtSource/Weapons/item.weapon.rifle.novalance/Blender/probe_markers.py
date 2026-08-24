import json
import sys
from pathlib import Path

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree


def cli_arg(name: str) -> str:
    args = sys.argv[sys.argv.index("--") + 1 :]
    return args[args.index(name) + 1]


source = Path(cli_arg("--source")).resolve()
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(source))
obj = next(obj for obj in bpy.context.scene.objects if obj.type == "MESH")
mesh = obj.data
bvh = BVHTree.FromPolygons(
    [vertex.co.copy() for vertex in mesh.vertices],
    [list(polygon.vertices) for polygon in mesh.polygons],
    all_triangles=True,
)

queries = {
    "RightHandGrip": (0.345, 0.0, -0.060),
    "RightHandGrip_300": (0.300, 0.0, -0.060),
    "RightHandGrip_305": (0.305, 0.0, -0.060),
    "RightHandGrip_310": (0.310, 0.0, -0.060),
    "LeftHandGrip": (-0.040, 0.0, -0.040),
    "Muzzle": (-0.500, 0.0, 0.0),
    "Muzzle_025": (-0.492, 0.0, 0.025),
    "Muzzle_035": (-0.492, 0.0, 0.035),
}
report = {}
for name, query in queries.items():
    nearest = bvh.find_nearest(Vector(query))
    report[name] = {
        "query": list(query),
        "nearest": list(nearest[0]),
        "normal": list(nearest[1]),
        "distance": nearest[3],
    }
print(json.dumps(report, indent=2))
