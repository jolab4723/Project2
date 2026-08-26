import json
from pathlib import Path

import bpy
from mathutils import Vector


ITEM_ID = "item.weapon.shotgun.thermobarrel"
ITEM_DIR = Path(__file__).resolve().parents[1]
PRODUCTION_DIR = ITEM_DIR / "Production"
FBX_PATH = PRODUCTION_DIR / f"{ITEM_ID}.fbx"
OUT_DIR = PRODUCTION_DIR / "QA" / "Reimport"
REIMPORT_BLEND = OUT_DIR / f"{ITEM_ID}_reimport.blend"
OUT_JSON = PRODUCTION_DIR / "QA" / "fbx_reimport_validation.json"


def vec(values):
    return [round(float(value), 6) for value in values]


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(FBX_PATH), use_anim=False)
scene = bpy.context.scene
scene.unit_settings.system = "METRIC"
scene.unit_settings.length_unit = "METERS"
scene.unit_settings.scale_length = 1.0

roots = [obj for obj in scene.objects if obj.parent is None]
root = next((obj for obj in roots if obj.name == ITEM_ID), None)
if root is None:
    raise RuntimeError(f"Expected top-level root {ITEM_ID}; got {[obj.name for obj in roots]}")

mesh_objects = [obj for obj in scene.objects if obj.type == "MESH"]
total_triangles = 0
for obj in mesh_objects:
    obj.data.calc_loop_triangles()
    total_triangles += len(obj.data.loop_triangles)

markers = {}
for marker_name in ("RightHandGrip", "LeftHandGrip", "Muzzle"):
    marker = scene.objects.get(marker_name)
    if marker is None:
        raise RuntimeError(f"Missing FBX marker {marker_name}")
    markers[marker_name] = {
        "parent": marker.parent.name if marker.parent else None,
        "local_location": vec(marker.location),
        "world_location": vec(marker.matrix_world.translation),
        "local_rotation_euler": vec(marker.rotation_euler),
        "local_scale": vec(marker.scale),
        "world_plus_z": vec((marker.matrix_world.to_3x3() @ Vector((0.0, 0.0, 1.0))).normalized()),
    }

materials = []
for material in bpy.data.materials:
    images = []
    if material.use_nodes and material.node_tree:
        for node in material.node_tree.nodes:
            if node.type == "TEX_IMAGE" and node.image:
                images.append(
                    {
                        "node": node.name,
                        "image": node.image.name,
                        "filepath": bpy.path.abspath(node.image.filepath),
                        "colorspace": node.image.colorspace_settings.name,
                    }
                )
    materials.append({"name": material.name, "images": images})

forbidden = [obj.name for obj in scene.objects if obj.type in {"CAMERA", "LIGHT", "ARMATURE"}]
left_world = Vector(markers["LeftHandGrip"]["world_location"])
right_world = Vector(markers["RightHandGrip"]["world_location"])
muzzle_forward = Vector(markers["Muzzle"]["world_plus_z"])

validation = {
    "item_id": ITEM_ID,
    "blender_version": bpy.app.version_string,
    "source_fbx": str(FBX_PATH),
    "top_level_roots": [{"name": obj.name, "type": obj.type} for obj in roots],
    "root_children": sorted(child.name for child in root.children),
    "root_transform": {
        "location": vec(root.location),
        "rotation_euler": vec(root.rotation_euler),
        "scale": vec(root.scale),
    },
    "markers": markers,
    "grip_spacing_z_m": round(float(left_world.z - right_world.z), 6),
    "muzzle_plus_z_dot_global_plus_z": round(float(muzzle_forward.dot(Vector((0.0, 0.0, 1.0)))), 6),
    "mesh_objects": [obj.name for obj in mesh_objects],
    "total_triangles": total_triangles,
    "materials": materials,
    "forbidden_objects": forbidden,
    "checks": {
        "single_root": len(roots) == 1 and roots[0] == root,
        "markers_direct_children": all(scene.objects[name].parent == root for name in markers),
        "grip_spacing_exact": abs((left_world.z - right_world.z) - 0.32625) <= 1e-5,
        "muzzle_points_plus_z": muzzle_forward.dot(Vector((0.0, 0.0, 1.0))) >= 0.9999,
        "triangle_budget": 40000 <= total_triangles <= 60000,
        "no_forbidden_objects": not forbidden,
        "root_transform_applied": vec(root.location) == [0.0, 0.0, 0.0] and vec(root.scale) == [1.0, 1.0, 1.0],
    },
}
validation["passed"] = all(validation["checks"].values())

OUT_DIR.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(REIMPORT_BLEND), compress=True)
OUT_JSON.write_text(json.dumps(validation, indent=2), encoding="utf-8")
print(json.dumps(validation, indent=2))
