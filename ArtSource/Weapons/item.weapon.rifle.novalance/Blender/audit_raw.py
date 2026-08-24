import json
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def cli_arg(name: str) -> str:
    args = sys.argv[sys.argv.index("--") + 1 :]
    index = args.index(name)
    return args[index + 1]


source = Path(cli_arg("--source")).resolve()
output = Path(cli_arg("--output")).resolve()

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(source))
bpy.context.view_layer.update()

records = []
total_triangles = 0
all_world = []
for obj in bpy.context.scene.objects:
    record = {
        "name": obj.name,
        "type": obj.type,
        "location": list(obj.location),
        "rotation_euler": list(obj.rotation_euler),
        "scale": list(obj.scale),
        "parent": obj.parent.name if obj.parent else None,
    }
    if obj.type == "MESH":
        mesh = obj.data
        mesh.calc_loop_triangles()
        triangles = len(mesh.loop_triangles)
        total_triangles += triangles
        record.update(
            {
                "vertices": len(mesh.vertices),
                "polygons": len(mesh.polygons),
                "triangles": triangles,
                "uv_layers": [layer.name for layer in mesh.uv_layers],
                "materials": [slot.material.name if slot.material else None for slot in obj.material_slots],
            }
        )
        all_world.extend([obj.matrix_world @ Vector(corner) for corner in obj.bound_box])
    records.append(record)

materials = []
for material in bpy.data.materials:
    nodes = []
    if material.use_nodes and material.node_tree:
        for node in material.node_tree.nodes:
            item = {"name": node.name, "type": node.bl_idname}
            if node.bl_idname == "ShaderNodeTexImage" and node.image:
                item["image"] = node.image.name
                item["image_size"] = list(node.image.size)
                item["packed"] = bool(node.image.packed_file)
            nodes.append(item)
    materials.append({"name": material.name, "use_nodes": material.use_nodes, "nodes": nodes})

if all_world:
    bounds_min = [min(vector[index] for vector in all_world) for index in range(3)]
    bounds_max = [max(vector[index] for vector in all_world) for index in range(3)]
else:
    bounds_min = bounds_max = [0.0, 0.0, 0.0]

result = {
    "source": str(source),
    "blender_version": bpy.app.version_string,
    "object_count": len(records),
    "mesh_count": sum(record["type"] == "MESH" for record in records),
    "total_triangles": total_triangles,
    "bounds_min": bounds_min,
    "bounds_max": bounds_max,
    "dimensions": [bounds_max[i] - bounds_min[i] for i in range(3)],
    "objects": records,
    "materials": materials,
    "images": [
        {
            "name": image.name,
            "size": list(image.size),
            "packed": bool(image.packed_file),
            "source": image.source,
        }
        for image in bpy.data.images
    ],
}
output.parent.mkdir(parents=True, exist_ok=True)
output.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps({key: result[key] for key in ("blender_version", "object_count", "mesh_count", "total_triangles", "dimensions")}, indent=2))
