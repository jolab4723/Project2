from __future__ import annotations

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
output = Path(cli_arg("--output")).resolve()

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(source), automatic_bone_orientation=False)
bpy.context.view_layer.update()

objects = list(bpy.context.scene.objects)
meshes = [obj for obj in objects if obj.type == "MESH"]
roots = [obj for obj in objects if obj.parent is None]
if len(meshes) != 1:
    raise RuntimeError(f"Expected one mesh, got {len(meshes)}")
mesh_object = meshes[0]
mesh_object.data.calc_loop_triangles()


def find_named(name: str) -> bpy.types.Object:
    matches = [obj for obj in objects if obj.name == name or obj.name.startswith(name + ".")]
    if len(matches) != 1:
        raise RuntimeError(f"Expected one {name}, got {[obj.name for obj in matches]}")
    return matches[0]


root = find_named("item.weapon.rifle.novalance")
right_grip = find_named("RightHandGrip")
left_grip = find_named("LeftHandGrip")
muzzle = find_named("Muzzle")

world_vertices = [mesh_object.matrix_world @ vertex.co for vertex in mesh_object.data.vertices]
polygons = [list(polygon.vertices) for polygon in mesh_object.data.polygons]
bvh = BVHTree.FromPolygons(world_vertices, polygons, all_triangles=True)


def surface_distance(obj: bpy.types.Object) -> float:
    return float(bvh.find_nearest(obj.matrix_world.translation)[3])


bounds = [mesh_object.matrix_world @ Vector(corner) for corner in mesh_object.bound_box]
bounds_min = [min(point[index] for point in bounds) for index in range(3)]
bounds_max = [max(point[index] for point in bounds) for index in range(3)]

materials = []
for material in bpy.data.materials:
    node_images = []
    if material.use_nodes and material.node_tree:
        for node in material.node_tree.nodes:
            if node.bl_idname == "ShaderNodeTexImage" and node.image:
                node_images.append(node.image.name)
    materials.append({"name": material.name, "images": node_images})


def transform_record(obj: bpy.types.Object) -> dict:
    return {
        "name": obj.name,
        "type": obj.type,
        "parent": obj.parent.name if obj.parent else None,
        "location": list(obj.location),
        "rotation_euler": list(obj.rotation_euler),
        "scale": list(obj.scale),
        "world_location": list(obj.matrix_world.translation),
    }


result = {
    "source": str(source),
    "blender_version": bpy.app.version_string,
    "root_count": len(roots),
    "root_names": [obj.name for obj in roots],
    "object_count": len(objects),
    "objects": [transform_record(obj) for obj in objects],
    "mesh_count": len(meshes),
    "triangles": len(mesh_object.data.loop_triangles),
    "uv_layers": [layer.name for layer in mesh_object.data.uv_layers],
    "materials": materials,
    "images": [{"name": image.name, "size": list(image.size), "filepath": image.filepath} for image in bpy.data.images],
    "bounds_min": bounds_min,
    "bounds_max": bounds_max,
    "dimensions": [bounds_max[i] - bounds_min[i] for i in range(3)],
    "surface_distance": {
        "RightHandGrip": surface_distance(right_grip),
        "LeftHandGrip": surface_distance(left_grip),
        "Muzzle": surface_distance(muzzle),
    },
    "direct_children": {
        "RightHandGrip": right_grip.parent == root,
        "LeftHandGrip": left_grip.parent == root,
        "Muzzle": muzzle.parent == root,
        "Mesh": mesh_object.parent == root,
    },
    "muzzle_forward": list(muzzle.matrix_world.to_3x3() @ Vector((0.0, 0.0, 1.0))),
    "forbidden_object_types": [obj.name for obj in objects if obj.type in {"CAMERA", "LIGHT", "ARMATURE"}],
}
result["pass"] = bool(
    result["root_count"] == 1
    and result["mesh_count"] == 1
    and 40000 <= result["triangles"] <= 60000
    and all(result["direct_children"].values())
    and all(distance < 0.03 for distance in result["surface_distance"].values())
    and not result["forbidden_object_types"]
    and result["muzzle_forward"][2] > 0.999
)

output.parent.mkdir(parents=True, exist_ok=True)
output.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(result, ensure_ascii=False, indent=2))
if not result["pass"]:
    raise SystemExit(2)
