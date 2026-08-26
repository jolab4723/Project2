import json
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[1]
GLB = ROOT / "Tripo" / "Downloaded" / "item.weapon.rifle.railcarbine_raw.glb"
REPORT = ROOT / "Tripo" / "raw_audit.json"


def rounded(values):
    return [round(float(value), 6) for value in values]


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(GLB))
objects = list(bpy.context.scene.objects)
meshes = [obj for obj in objects if obj.type == "MESH"]
points = [obj.matrix_world @ Vector(corner) for obj in meshes for corner in obj.bound_box]
minimum = Vector((min(point[index] for point in points) for index in range(3)))
maximum = Vector((max(point[index] for point in points) for index in range(3)))
for obj in meshes:
    obj.data.calc_loop_triangles()

material_reports = []
for material in bpy.data.materials:
    nodes = list(material.node_tree.nodes) if material.use_nodes else []
    principled = next(
        (node for node in nodes if node.bl_idname == "ShaderNodeBsdfPrincipled"),
        None,
    )
    input_records = {}
    if principled:
        for name in (
            "Base Color",
            "Metallic",
            "Roughness",
            "Emission Color",
            "Emission Strength",
            "Alpha",
        ):
            socket = principled.inputs.get(name)
            if socket is None:
                continue
            input_records[name] = {
                "linked": socket.is_linked,
                "default": (
                    rounded(socket.default_value)
                    if hasattr(socket.default_value, "__len__")
                    else round(float(socket.default_value), 6)
                ),
                "from": [
                    {
                        "node": link.from_node.name,
                        "node_type": link.from_node.bl_idname,
                        "socket": link.from_socket.name,
                    }
                    for link in socket.links
                ],
            }
    material_reports.append(
        {
            "name": material.name,
            "use_nodes": material.use_nodes,
            "principled_inputs": input_records,
            "nodes": [
                {
                    "name": node.name,
                    "type": node.bl_idname,
                    "image": node.image.name
                    if node.bl_idname == "ShaderNodeTexImage" and node.image
                    else None,
                    "image_colorspace": node.image.colorspace_settings.name
                    if node.bl_idname == "ShaderNodeTexImage" and node.image
                    else None,
                }
                for node in nodes
            ],
        }
    )

report = {
    "blender_version": bpy.app.version_string,
    "glb": str(GLB),
    "objects": [
        {
            "name": obj.name,
            "type": obj.type,
            "parent": obj.parent.name if obj.parent else None,
            "location": rounded(obj.location),
            "rotation": rounded(obj.rotation_euler),
            "scale": rounded(obj.scale),
            "materials": [slot.material.name if slot.material else None for slot in obj.material_slots],
            "triangles": len(obj.data.loop_triangles) if obj.type == "MESH" else None,
        }
        for obj in objects
    ],
    "bounds": {
        "min": rounded(minimum),
        "max": rounded(maximum),
        "dimensions": rounded(maximum - minimum),
    },
    "images": [
        {
            "name": image.name,
            "size": list(image.size),
            "colorspace": image.colorspace_settings.name,
            "packed": image.packed_file is not None,
        }
        for image in bpy.data.images
    ],
    "materials": material_reports,
}
REPORT.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(report, ensure_ascii=False, indent=2))
