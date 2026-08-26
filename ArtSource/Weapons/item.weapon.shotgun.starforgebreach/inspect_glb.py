import bpy
import json
import sys
from pathlib import Path
from mathutils import Vector


root = Path(__file__).resolve().parent
glb_path = root / "Tripo" / "Downloaded" / "model.glb"
out_path = root / "Production" / "raw_inspection.json"

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(glb_path))

mesh_objects = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
payload = {
    "source": str(glb_path),
    "objects": [],
    "images": [],
    "materials": [],
}

for obj in mesh_objects:
    corners = [obj.matrix_world @ Vector(obj.bound_box[i]) for i in range(8)]
    payload["objects"].append({
        "name": obj.name,
        "vertices": len(obj.data.vertices),
        "polygons": len(obj.data.polygons),
        "triangles": sum(len(poly.vertices) - 2 for poly in obj.data.polygons),
        "bounds_world": {
            "min": [min(v[i] for v in corners) for i in range(3)],
            "max": [max(v[i] for v in corners) for i in range(3)],
        },
        "materials": [slot.material.name if slot.material else None for slot in obj.material_slots],
        "transform": {
            "location": list(obj.location),
            "rotation_euler": list(obj.rotation_euler),
            "scale": list(obj.scale),
        },
    })

for image in bpy.data.images:
    payload["images"].append({
        "name": image.name,
        "size": list(image.size),
        "colorspace": image.colorspace_settings.name,
        "packed": image.packed_file is not None,
        "filepath": image.filepath,
    })

for material in bpy.data.materials:
    payload["materials"].append({
        "name": material.name,
        "use_nodes": material.use_nodes,
        "nodes": [node.bl_idname for node in material.node_tree.nodes] if material.use_nodes else [],
    })

out_path.parent.mkdir(parents=True, exist_ok=True)
out_path.write_text(json.dumps(payload, indent=2), encoding="utf-8")
print(json.dumps(payload, indent=2))
