import json
from pathlib import Path

import bpy
from mathutils import Vector


ITEM_DIR = Path(__file__).resolve().parents[1]
RAW_GLB = ITEM_DIR / "Tripo" / "Downloaded" / "item.weapon.shotgun.thermobarrel_raw.glb"
OUT_JSON = ITEM_DIR / "Production" / "QA" / "raw_audit.json"


def vec(values):
    return [round(float(value), 6) for value in values]


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(RAW_GLB))

scene = bpy.context.scene
scene.unit_settings.system = "METRIC"

mesh_objects = [obj for obj in scene.objects if obj.type == "MESH"]
all_world_corners = []
objects = []
total_triangles = 0
grip_regions = {}

for obj in mesh_objects:
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = obj.evaluated_get(depsgraph)
    evaluated_mesh = evaluated.to_mesh()
    evaluated_mesh.calc_loop_triangles()
    triangles = len(evaluated_mesh.loop_triangles)
    total_triangles += triangles
    evaluated.to_mesh_clear()

    corners = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    all_world_corners.extend(corners)
    objects.append(
        {
            "name": obj.name,
            "vertices": len(obj.data.vertices),
            "polygons": len(obj.data.polygons),
            "triangles": triangles,
            "location": vec(obj.location),
            "rotation_euler": vec(obj.rotation_euler),
            "scale": vec(obj.scale),
            "world_bounds_min": vec([min(c[i] for c in corners) for i in range(3)]),
            "world_bounds_max": vec([max(c[i] for c in corners) for i in range(3)]),
            "materials": [slot.material.name if slot.material else None for slot in obj.material_slots],
            "uv_layers": [layer.name for layer in obj.data.uv_layers],
        }
    )

    world_vertices = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
    region_specs = {
        "right_hand_vertical_grip": lambda co: 0.16 <= co.x <= 0.27 and co.z <= -0.018,
        "right_hand_vertical_grip_positive_z": lambda co: 0.16 <= co.x <= 0.27 and co.z >= 0.018,
        "left_hand_foregrip": lambda co: -0.28 <= co.x <= -0.03 and co.z <= -0.035,
        "left_hand_foregrip_positive_z": lambda co: -0.28 <= co.x <= -0.03 and co.z >= 0.035,
        "muzzle_tip": lambda co: co.x <= -0.47,
    }
    for region_name, predicate in region_specs.items():
        selected = [co for co in world_vertices if predicate(co)]
        grip_regions[region_name] = {
            "vertex_count": len(selected),
            "bounds_min": vec([min(co[i] for co in selected) for i in range(3)]) if selected else None,
            "bounds_max": vec([max(co[i] for co in selected) for i in range(3)]) if selected else None,
            "centroid": vec([sum(co[i] for co in selected) / len(selected) for i in range(3)]) if selected else None,
        }

materials = []
for material in bpy.data.materials:
    entry = {
        "name": material.name,
        "use_nodes": material.use_nodes,
        "blend_method": getattr(material, "surface_render_method", None),
        "nodes": [],
    }
    if material.use_nodes and material.node_tree:
        for node in material.node_tree.nodes:
            node_entry = {"name": node.name, "type": node.bl_idname}
            if node.type == "BSDF_PRINCIPLED":
                for socket_name in ("Base Color", "Metallic", "Roughness", "Emission Color", "Emission Strength"):
                    socket = node.inputs.get(socket_name)
                    if socket and not socket.is_linked:
                        value = socket.default_value
                        node_entry[socket_name] = vec(value) if hasattr(value, "__len__") else round(float(value), 6)
            if node.type == "TEX_IMAGE" and node.image:
                node_entry["image"] = {
                    "name": node.image.name,
                    "filepath": bpy.path.abspath(node.image.filepath),
                    "size": list(node.image.size),
                    "colorspace": node.image.colorspace_settings.name,
                    "packed": node.image.packed_file is not None,
                }
            entry["nodes"].append(node_entry)
    materials.append(entry)

if all_world_corners:
    bounds_min = [min(c[i] for c in all_world_corners) for i in range(3)]
    bounds_max = [max(c[i] for c in all_world_corners) for i in range(3)]
    dimensions = [bounds_max[i] - bounds_min[i] for i in range(3)]
else:
    bounds_min = bounds_max = dimensions = [0.0, 0.0, 0.0]

audit = {
    "source": str(RAW_GLB),
    "blender_version": bpy.app.version_string,
    "scene_objects": [{"name": obj.name, "type": obj.type} for obj in scene.objects],
    "mesh_object_count": len(mesh_objects),
    "total_triangles": total_triangles,
    "world_bounds_min": vec(bounds_min),
    "world_bounds_max": vec(bounds_max),
    "world_dimensions": vec(dimensions),
    "objects": objects,
    "grip_regions": grip_regions,
    "materials": materials,
    "images": [
        {
            "name": image.name,
            "filepath": bpy.path.abspath(image.filepath),
            "size": list(image.size),
            "colorspace": image.colorspace_settings.name,
            "packed": image.packed_file is not None,
        }
        for image in bpy.data.images
    ],
}

OUT_JSON.parent.mkdir(parents=True, exist_ok=True)
OUT_JSON.write_text(json.dumps(audit, indent=2), encoding="utf-8")
print(json.dumps(audit, indent=2))
