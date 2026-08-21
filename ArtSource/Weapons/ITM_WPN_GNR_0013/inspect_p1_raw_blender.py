import json
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parent
MODEL_PATH = ROOT / "Tripo" / "Downloaded" / "ITM_WPN_GNR_0013_P1_raw.glb"
OUTPUT_DIR = ROOT / "QA" / "P1Raw"
OUTPUT_PATH = OUTPUT_DIR / "p1_raw_inspection.json"


def rounded(values):
    return [round(float(value), 6) for value in values]


def object_world_bounds(obj):
    if not getattr(obj, "bound_box", None):
        return None
    points = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    return {
        "min": rounded((min(point.x for point in points), min(point.y for point in points), min(point.z for point in points))),
        "max": rounded((max(point.x for point in points), max(point.y for point in points), max(point.z for point in points))),
    }


def material_record(material):
    record = {
        "name": material.name,
        "use_nodes": material.use_nodes,
        "diffuse_color": rounded(material.diffuse_color),
        "node_types": [],
        "images": [],
    }
    if material.use_nodes and material.node_tree:
        record["node_types"] = sorted({node.bl_idname for node in material.node_tree.nodes})
        record["images"] = sorted(
            {
                node.image.name
                for node in material.node_tree.nodes
                if node.bl_idname == "ShaderNodeTexImage" and node.image
            }
        )
        for node in material.node_tree.nodes:
            if node.bl_idname == "ShaderNodeBsdfPrincipled":
                record["principled"] = {
                    "base_color": rounded(node.inputs["Base Color"].default_value),
                    "metallic": round(float(node.inputs["Metallic"].default_value), 6),
                    "roughness": round(float(node.inputs["Roughness"].default_value), 6),
                    "alpha": round(float(node.inputs["Alpha"].default_value), 6),
                    "transmission_weight": round(float(node.inputs["Transmission Weight"].default_value), 6),
                }
    return record


def connected_components(obj):
    mesh = obj.data
    parent = list(range(len(mesh.vertices)))

    def find(index):
        while parent[index] != index:
            parent[index] = parent[parent[index]]
            index = parent[index]
        return index

    def union(left, right):
        left_root = find(left)
        right_root = find(right)
        if left_root != right_root:
            parent[right_root] = left_root

    for polygon in mesh.polygons:
        vertices = list(polygon.vertices)
        for index in range(1, len(vertices)):
            union(vertices[0], vertices[index])

    records = {}
    for vertex in mesh.vertices:
        root = find(vertex.index)
        record = records.setdefault(root, {"vertex_indices": [], "polygon_count": 0, "triangle_count": 0})
        record["vertex_indices"].append(vertex.index)
    for polygon in mesh.polygons:
        root = find(polygon.vertices[0])
        records[root]["polygon_count"] += 1
        records[root]["triangle_count"] += max(1, len(polygon.vertices) - 2)

    result = []
    for record in records.values():
        points = [obj.matrix_world @ mesh.vertices[index].co for index in record["vertex_indices"]]
        minimum = [min(point[axis] for point in points) for axis in range(3)]
        maximum = [max(point[axis] for point in points) for axis in range(3)]
        result.append(
            {
                "vertices": len(record["vertex_indices"]),
                "polygons": record["polygon_count"],
                "triangles": record["triangle_count"],
                "bounds": {"min": rounded(minimum), "max": rounded(maximum)},
                "dimensions": rounded(maximum[axis] - minimum[axis] for axis in range(3)),
                "center": rounded((minimum[axis] + maximum[axis]) * 0.5 for axis in range(3)),
            }
        )
    return sorted(result, key=lambda record: record["triangles"], reverse=True)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(MODEL_PATH))

    objects = []
    total_vertices = 0
    total_polygons = 0
    total_triangles = 0
    for obj in bpy.context.scene.objects:
        record = {
            "name": obj.name,
            "type": obj.type,
            "parent": obj.parent.name if obj.parent else None,
            "location": rounded(obj.location),
            "rotation_euler": rounded(obj.rotation_euler),
            "scale": rounded(obj.scale),
            "dimensions": rounded(obj.dimensions),
            "world_bounds": object_world_bounds(obj),
        }
        if obj.type == "MESH":
            mesh = obj.data
            mesh.calc_loop_triangles()
            vertices = len(mesh.vertices)
            polygons = len(mesh.polygons)
            triangles = len(mesh.loop_triangles)
            total_vertices += vertices
            total_polygons += polygons
            total_triangles += triangles
            record.update(
                {
                    "vertices": vertices,
                    "polygons": polygons,
                    "triangles": triangles,
                    "uv_layers": [layer.name for layer in mesh.uv_layers],
                    "material_slots": [slot.material.name if slot.material else None for slot in obj.material_slots],
                    "connected_components": connected_components(obj),
                }
            )
        objects.append(record)

    all_bounds = [record["world_bounds"] for record in objects if record["world_bounds"]]
    scene_bounds = None
    if all_bounds:
        scene_bounds = {
            "min": [min(bounds["min"][axis] for bounds in all_bounds) for axis in range(3)],
            "max": [max(bounds["max"][axis] for bounds in all_bounds) for axis in range(3)],
        }
        scene_bounds["dimensions"] = [
            round(scene_bounds["max"][axis] - scene_bounds["min"][axis], 6)
            for axis in range(3)
        ]

    result = {
        "source": str(MODEL_PATH.relative_to(ROOT)),
        "blender_version": bpy.app.version_string,
        "object_count": len(objects),
        "mesh_count": sum(record["type"] == "MESH" for record in objects),
        "material_count": len(bpy.data.materials),
        "image_count": len(bpy.data.images),
        "totals": {
            "vertices": total_vertices,
            "polygons": total_polygons,
            "triangles": total_triangles,
        },
        "connected_component_count": sum(
            len(record.get("connected_components", [])) for record in objects
        ),
        "scene_bounds": scene_bounds,
        "objects": objects,
        "materials": [material_record(material) for material in bpy.data.materials],
        "images": [
            {
                "name": image.name,
                "size": list(image.size),
                "file_format": image.file_format,
                "colorspace": image.colorspace_settings.name,
            }
            for image in bpy.data.images
        ],
    }

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    print(str(OUTPUT_PATH))
    print(json.dumps({key: result[key] for key in ("object_count", "mesh_count", "material_count", "image_count", "totals", "scene_bounds")}, indent=2))


if __name__ == "__main__":
    main()
