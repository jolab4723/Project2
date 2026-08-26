import json
import sys
from pathlib import Path

import bpy
import numpy as np


def cli_arg(name: str) -> str:
    args = sys.argv[sys.argv.index("--") + 1 :]
    return args[args.index(name) + 1]


source = Path(cli_arg("--source")).resolve()
output = Path(cli_arg("--output")).resolve()

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(source))
obj = next(item for item in bpy.context.scene.objects if item.type == "MESH")
obj.data.calc_loop_triangles()
modifier = obj.modifiers.new("Analyze55k", "DECIMATE")
modifier.ratio = 55000 / len(obj.data.loop_triangles)
modifier.use_collapse_triangulate = True
bpy.context.view_layer.objects.active = obj
obj.select_set(True)
bpy.ops.object.modifier_apply(modifier=modifier.name)

mesh = obj.data
uv = mesh.uv_layers.active.data
image = next(image for image in bpy.data.images if image.name.startswith("Color_"))
width, height = image.size
pixels = np.empty(width * height * 4, dtype=np.float32)
image.pixels.foreach_get(pixels)
pixels = pixels.reshape((height, width, 4))

red_faces = []
for polygon in mesh.polygons:
    loop_indices = polygon.loop_indices
    uv_center = sum((uv[index].uv for index in loop_indices), start=uv[loop_indices[0]].uv.copy() * 0.0) / len(loop_indices)
    x = min(width - 1, max(0, int(uv_center.x * width)))
    y = min(height - 1, max(0, int(uv_center.y * height)))
    color = pixels[y, x, :3]
    if color[0] > 0.22 and color[0] > color[1] * 1.35 and color[0] > color[2] * 1.18:
        center = polygon.center
        red_faces.append((float(center.x), float(center.y), float(center.z), float(color[0]), float(color[1]), float(color[2])))

xs = np.array([item[0] for item in red_faces], dtype=float)
zs = np.array([item[2] for item in red_faces], dtype=float)
hist_x, edges_x = np.histogram(xs, bins=24, range=(-0.5, 0.5))
hist_z, edges_z = np.histogram(zs, bins=20, range=(-0.22, 0.22))

result = {
    "triangles": len(mesh.polygons),
    "red_face_count": len(red_faces),
    "red_bounds": {
        "x": [float(xs.min()), float(xs.max())],
        "z": [float(zs.min()), float(zs.max())],
    },
    "hist_x": [{"min": float(edges_x[i]), "max": float(edges_x[i + 1]), "count": int(hist_x[i])} for i in range(len(hist_x))],
    "hist_z": [{"min": float(edges_z[i]), "max": float(edges_z[i + 1]), "count": int(hist_z[i])} for i in range(len(hist_z))],
}
output.parent.mkdir(parents=True, exist_ok=True)
output.write_text(json.dumps(result, indent=2), encoding="utf-8")
print(json.dumps(result, indent=2))
