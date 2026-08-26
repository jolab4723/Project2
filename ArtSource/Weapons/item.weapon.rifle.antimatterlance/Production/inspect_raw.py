import json
from pathlib import Path

import bpy
from mathutils import Vector


ITEM_ID = "item.weapon.rifle.antimatterlance"
ITEM_DIR = Path(__file__).resolve().parents[1]
RAW_GLB = ITEM_DIR / "Tripo" / "Downloaded" / f"{ITEM_ID}_raw.glb"
OUT_JSON = ITEM_DIR / "Production" / "QA" / "raw_audit.json"


def vec(values):
    return [round(float(value), 6) for value in values]


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(RAW_GLB))
scene = bpy.context.scene
mesh_objects = [obj for obj in scene.objects if obj.type == "MESH"]

all_corners = []
objects = []
total_triangles = 0
grip_regions = {}
for obj in mesh_objects:
    obj.data.calc_loop_triangles()
    triangles = len(obj.data.loop_triangles)
    total_triangles += triangles
    corners = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    all_corners.extend(corners)
    objects.append(
        {
            "name": obj.name,
            "vertices": len(obj.data.vertices),
            "triangles": triangles,
            "bounds_min": vec([min(point[index] for point in corners) for index in range(3)]),
            "bounds_max": vec([max(point[index] for point in corners) for index in range(3)]),
            "materials": [slot.material.name if slot.material else None for slot in obj.material_slots],
            "uv_layers": [layer.name for layer in obj.data.uv_layers],
        }
    )
    world_vertices = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
    region_specs = {
        "right_hand_grip": lambda co: 0.14 <= co.x <= 0.30 and co.z <= -0.018,
        "left_hand_foregrip": lambda co: -0.20 <= co.x <= 0.06 and co.z <= -0.018,
        "muzzle_tip": lambda co: co.x <= -0.47,
    }
    for name, predicate in region_specs.items():
        selected = [co for co in world_vertices if predicate(co)]
        grip_regions[name] = {
            "vertex_count": len(selected),
            "bounds_min": vec([min(point[index] for point in selected) for index in range(3)]) if selected else None,
            "bounds_max": vec([max(point[index] for point in selected) for index in range(3)]) if selected else None,
            "centroid": vec([sum(point[index] for point in selected) / len(selected) for index in range(3)]) if selected else None,
        }

bounds_min = [min(point[index] for point in all_corners) for index in range(3)]
bounds_max = [max(point[index] for point in all_corners) for index in range(3)]
audit = {
    "item_id": ITEM_ID,
    "blender_version": bpy.app.version_string,
    "scene_objects": [{"name": obj.name, "type": obj.type} for obj in scene.objects],
    "mesh_object_count": len(mesh_objects),
    "total_triangles": total_triangles,
    "bounds_min": vec(bounds_min),
    "bounds_max": vec(bounds_max),
    "dimensions": vec([bounds_max[index] - bounds_min[index] for index in range(3)]),
    "objects": objects,
    "grip_regions": grip_regions,
    "materials": [material.name for material in bpy.data.materials],
    "images": [
        {
            "name": image.name,
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
