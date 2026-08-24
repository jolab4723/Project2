from __future__ import annotations

import json
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parent
RAW = ROOT / "Tripo" / "Downloaded" / "item.weapon.grenadelauncher.worldender_raw.glb"
OUT = ROOT / "QA" / "Raw" / "raw_inspection.json"
EXTRACTED = ROOT / "QA" / "Raw" / "Extracted"


def rounded(values, digits=6):
    return [round(float(value), digits) for value in values]


def bounds(objects):
    points = [obj.matrix_world @ Vector(corner) for obj in objects if obj.type == "MESH" for corner in obj.bound_box]
    minimum = Vector(min(point[i] for point in points) for i in range(3))
    maximum = Vector(max(point[i] for point in points) for i in range(3))
    return {"min": rounded(minimum), "max": rounded(maximum), "dimensions": rounded(maximum - minimum), "center": rounded((minimum + maximum) * 0.5)}


def material_record(material):
    result = {"name": material.name, "use_nodes": bool(material.use_nodes), "diffuse_color": rounded(material.diffuse_color)}
    if not material.use_nodes or not material.node_tree:
        return result
    result["nodes"] = []
    for node in material.node_tree.nodes:
        record = {"name": node.name, "type": node.bl_idname}
        if node.bl_idname == "ShaderNodeTexImage" and node.image:
            record.update({"image": node.image.name, "colorspace": node.image.colorspace_settings.name})
        if node.bl_idname == "ShaderNodeBsdfPrincipled":
            record["inputs"] = {
                socket.name: {
                    "linked": bool(socket.is_linked),
                    "default": rounded(socket.default_value) if hasattr(socket.default_value, "__len__") else round(float(socket.default_value), 6),
                    "from": [f"{link.from_node.name}.{link.from_socket.name}" for link in socket.links],
                }
                for socket in node.inputs
                if socket.name in {"Base Color", "Metallic", "Roughness", "Alpha", "Normal", "Emission Color", "Emission Strength"}
            }
        result["nodes"].append(record)
    result["links"] = [f"{link.from_node.name}.{link.from_socket.name} -> {link.to_node.name}.{link.to_socket.name}" for link in material.node_tree.links]
    return result


def main():
    if not RAW.is_file():
        raise FileNotFoundError(RAW)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(RAW))
    EXTRACTED.mkdir(parents=True, exist_ok=True)
    for image in bpy.data.images:
        destination = EXTRACTED / f"{image.name}.png"
        image.filepath_raw = str(destination)
        image.file_format = "PNG"
        image.save()
    objects = list(bpy.context.scene.objects)
    mesh_objects = [obj for obj in objects if obj.type == "MESH"]
    records = []
    totals = {"vertices": 0, "polygons": 0, "triangles": 0}
    for obj in objects:
        record = {"name": obj.name, "type": obj.type, "parent": obj.parent.name if obj.parent else None, "location": rounded(obj.location), "rotation_euler": rounded(obj.rotation_euler), "scale": rounded(obj.scale), "dimensions": rounded(obj.dimensions)}
        if obj.type == "MESH":
            obj.data.calc_loop_triangles()
            record.update({"vertices": len(obj.data.vertices), "polygons": len(obj.data.polygons), "triangles": len(obj.data.loop_triangles), "uv_layers": [layer.name for layer in obj.data.uv_layers], "material_slots": [slot.material.name if slot.material else None for slot in obj.material_slots]})
            totals["vertices"] += record["vertices"]
            totals["polygons"] += record["polygons"]
            totals["triangles"] += record["triangles"]
        records.append(record)
    report = {"source": str(RAW.relative_to(ROOT)), "blender_version": bpy.app.version_string, "object_count": len(objects), "mesh_count": len(mesh_objects), "material_count": len(bpy.data.materials), "image_count": len(bpy.data.images), "totals": totals, "bounds": bounds(mesh_objects), "objects": records, "materials": [material_record(material) for material in bpy.data.materials], "images": [{"name": image.name, "size": list(image.size), "file_format": image.file_format, "colorspace": image.colorspace_settings.name, "packed": image.packed_file is not None} for image in bpy.data.images]}
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({key: report[key] for key in ("source", "blender_version", "mesh_count", "material_count", "image_count", "totals", "bounds")}, indent=2))


if __name__ == "__main__":
    main()
