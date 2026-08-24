"""Reimport and validate the production FBX in a clean Blender scene.

Run with Blender 5.1 or newer. The script deliberately starts from an empty
scene so the report describes only what is serialized in the FBX.
"""

from __future__ import annotations

import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path

import bpy
from mathutils import Vector


BASE = Path(__file__).resolve().parent
PRODUCTION = BASE / "Production"
FBX_PATH = PRODUCTION / "ITM_WPN_GNR_0013.fbx"
REPORT_PATH = PRODUCTION / "QA" / "reimport_validation.json"

ROOT_NAME = "ITM_WPN_GNR_0013_root"
MARKER_NAMES = ("RightHandGrip", "LeftHandGrip", "Muzzle")
RABBIT_NAMES = ("Rabbit_Solid", "Rabbit_Eyes", "RabbitNose", "Rabbit_Cheeks")
FACE_NAMES = ("Rabbit_Eyes", "RabbitNose", "Rabbit_Cheeks")
EXPECTED_MATERIALS = {
    "M_Weapon_P1_Base",
    "M_IceGlass",
    "M_RabbitWhite",
    "M_RabbitEyes",
    "M_RabbitNose",
    "M_RabbitCheeks",
}
EXPECTED_TRIANGLES = 27247


def rounded(values, digits: int = 6) -> list[float]:
    return [round(float(value), digits) for value in values]


def close_vector(actual, expected, tolerance: float = 1e-5) -> bool:
    return all(abs(float(a) - float(e)) <= tolerance for a, e in zip(actual, expected))


def is_under_root(obj: bpy.types.Object, root: bpy.types.Object) -> bool:
    current = obj
    while current is not None:
        if current == root:
            return True
        current = current.parent
    return False


def reset_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in (
        bpy.data.meshes,
        bpy.data.curves,
        bpy.data.materials,
        bpy.data.cameras,
        bpy.data.lights,
        bpy.data.armatures,
    ):
        for block in list(collection):
            collection.remove(block)


def root_local_bounds(root: bpy.types.Object, names: tuple[str, ...] | None = None):
    selected = [
        obj
        for obj in bpy.context.scene.objects
        if obj.type == "MESH" and (names is None or obj.name in names)
    ]
    if not selected:
        return None
    inverse_root = root.matrix_world.inverted()
    points = [
        inverse_root @ obj.matrix_world @ Vector(corner)
        for obj in selected
        for corner in obj.bound_box
    ]
    minimum = Vector(min(point[index] for point in points) for index in range(3))
    maximum = Vector(max(point[index] for point in points) for index in range(3))
    return minimum, maximum


def bounds_record(bounds):
    if bounds is None:
        return None
    minimum, maximum = bounds
    return {
        "min": rounded(minimum),
        "max": rounded(maximum),
        "dimensions": rounded(maximum - minimum),
    }


def marker_record(root: bpy.types.Object, name: str) -> dict:
    marker = bpy.data.objects.get(name)
    if marker is None:
        return {"exists": False}
    local = root.matrix_world.inverted() @ marker.matrix_world
    plus_z = (local.to_3x3() @ Vector((0.0, 0.0, 1.0))).normalized()
    return {
        "exists": True,
        "type": marker.type,
        "parent": marker.parent.name if marker.parent else None,
        "local_location": rounded(local.translation),
        "local_plus_z": rounded(plus_z),
        "scale": rounded(marker.scale),
    }


def material_record(material: bpy.types.Material) -> dict:
    record = {
        "name": material.name,
        "use_nodes": bool(material.use_nodes),
        "diffuse_color": rounded(material.diffuse_color),
    }
    if material.use_nodes:
        principled = next(
            (node for node in material.node_tree.nodes if node.bl_idname == "ShaderNodeBsdfPrincipled"),
            None,
        )
        record["principled"] = principled is not None
        if principled is not None:
            base = principled.inputs.get("Base Color")
            alpha = principled.inputs.get("Alpha")
            metallic = principled.inputs.get("Metallic")
            roughness = principled.inputs.get("Roughness")
            record["principled_values"] = {
                "base_color": rounded(base.default_value) if base else None,
                "alpha": round(float(alpha.default_value), 6) if alpha else None,
                "metallic": round(float(metallic.default_value), 6) if metallic else None,
                "roughness": round(float(roughness.default_value), 6) if roughness else None,
            }
    return record


def main() -> None:
    if not FBX_PATH.is_file():
        raise FileNotFoundError(FBX_PATH)

    reset_scene()
    bpy.ops.import_scene.fbx(filepath=str(FBX_PATH))

    objects = list(bpy.context.scene.objects)
    roots = [obj for obj in objects if obj.parent is None]
    root = bpy.data.objects.get(ROOT_NAME)
    if root is None:
        raise RuntimeError(f"Expected root {ROOT_NAME!r} was not found")

    meshes = [obj for obj in objects if obj.type == "MESH"]
    triangles = 0
    polygons = 0
    missing_material_slots = []
    material_names = set()
    mesh_records = []
    for obj in meshes:
        obj.data.calc_loop_triangles()
        mesh_triangles = len(obj.data.loop_triangles)
        triangles += mesh_triangles
        polygons += len(obj.data.polygons)
        slots = []
        for slot_index, slot in enumerate(obj.material_slots):
            if slot.material is None:
                missing_material_slots.append({"object": obj.name, "slot": slot_index})
                slots.append(None)
            else:
                material_names.add(slot.material.name)
                slots.append(slot.material.name)
        mesh_records.append(
            {
                "name": obj.name,
                "parent": obj.parent.name if obj.parent else None,
                "triangles": mesh_triangles,
                "scale": rounded(obj.scale),
                "materials": slots,
            }
        )

    markers = {name: marker_record(root, name) for name in MARKER_NAMES}
    whole_bounds = root_local_bounds(root)
    rabbit_bounds = root_local_bounds(root, RABBIT_NAMES)
    rabbit_solid_bounds = root_local_bounds(root, ("Rabbit_Solid",))
    face_bounds = root_local_bounds(root, FACE_NAMES)
    glass_bounds = root_local_bounds(root, ("CoolingChamber_Glass",))

    rabbit_inside_glass = False
    if rabbit_bounds and glass_bounds:
        rabbit_min, rabbit_max = rabbit_bounds
        glass_min, glass_max = glass_bounds
        margin = 0.01
        rabbit_inside_glass = all(
            rabbit_min[index] >= glass_min[index] + margin
            and rabbit_max[index] <= glass_max[index] - margin
            for index in range(3)
        )

    face_only_plus_z = False
    if face_bounds and rabbit_solid_bounds:
        face_min, _ = face_bounds
        solid_min, solid_max = rabbit_solid_bounds
        solid_center_z = (solid_min.z + solid_max.z) * 0.5
        face_only_plus_z = face_min.z > solid_center_z

    banned = [obj.name for obj in objects if obj.type in {"CAMERA", "LIGHT", "ARMATURE"}]
    non_unit_or_negative_scales = [
        obj.name
        for obj in objects
        if any(float(value) <= 0.0 or abs(float(value) - 1.0) > 1e-5 for value in obj.scale)
    ]
    direct_children = sorted(child.name for child in root.children)

    checks = {
        "single_top_level_root": len(roots) == 1 and roots[0] == root,
        "root_is_empty": root.type == "EMPTY",
        "all_objects_under_root": all(is_under_root(obj, root) for obj in objects),
        "markers_exist_and_are_root_direct": all(
            markers[name].get("exists") and markers[name].get("parent") == ROOT_NAME
            for name in MARKER_NAMES
        ),
        "right_hand_grip_is_root_origin": close_vector(
            markers["RightHandGrip"].get("local_location", (999, 999, 999)),
            (0.0, 0.0, 0.0),
        ),
        "muzzle_is_forward_of_grips": (
            markers["Muzzle"].get("local_location", [0, 0, -999])[2]
            > markers["LeftHandGrip"].get("local_location", [0, 0, 999])[2]
            > markers["RightHandGrip"].get("local_location", [0, 0, 999])[2]
        ),
        "muzzle_local_plus_z": close_vector(
            markers["Muzzle"].get("local_plus_z", (999, 999, 999)),
            (0.0, 0.0, 1.0),
        ),
        "expected_mesh_count": len(meshes) == 8,
        "triangle_count_preserved": triangles == EXPECTED_TRIANGLES,
        "expected_material_names": EXPECTED_MATERIALS.issubset(material_names),
        "all_mesh_material_slots_non_null": not missing_material_slots,
        "glass_mesh_and_material_preserved": (
            bpy.data.objects.get("CoolingChamber_Glass") is not None
            and "M_IceGlass" in material_names
        ),
        "rabbit_solid_and_face_parts_preserved": all(bpy.data.objects.get(name) is not None for name in RABBIT_NAMES),
        "rabbit_inside_glass_with_margin": rabbit_inside_glass,
        "rabbit_face_features_only_on_plus_z": face_only_plus_z,
        "no_camera_light_armature": not banned,
        "unit_positive_object_scales": not non_unit_or_negative_scales,
    }

    report = {
        "asset": "ITM_WPN_GNR_0013",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "blender_version": bpy.app.version_string,
        "source_fbx": str(FBX_PATH.relative_to(BASE)),
        "source_fbx_sha256": hashlib.sha256(FBX_PATH.read_bytes()).hexdigest(),
        "clean_scene_import": True,
        "coordinate_contract": {
            "muzzle": "+Z",
            "up": "+Y",
            "rabbit_face": "+Z",
            "root_origin": "trigger right-hand grip centre",
        },
        "top_level_objects": [obj.name for obj in roots],
        "root": ROOT_NAME,
        "root_children": direct_children,
        "markers": markers,
        "bounds_root_local": bounds_record(whole_bounds),
        "rabbit_bounds_root_local": bounds_record(rabbit_bounds),
        "rabbit_solid_bounds_root_local": bounds_record(rabbit_solid_bounds),
        "rabbit_face_feature_bounds_root_local": bounds_record(face_bounds),
        "glass_outer_bounds_root_local": bounds_record(glass_bounds),
        "mesh_count": len(meshes),
        "triangle_count": triangles,
        "polygon_count": polygons,
        "meshes": mesh_records,
        "materials": [material_record(bpy.data.materials[name]) for name in sorted(material_names)],
        "missing_material_slots": missing_material_slots,
        "banned_objects": banned,
        "non_unit_or_negative_scale_objects": non_unit_or_negative_scales,
        "checks": checks,
        "pass": all(checks.values()),
        "notes": [
            "The FBX was imported after deleting every object and relevant datablock from the Blender scene.",
            "Rabbit face meshes remain entirely on the muzzle-facing +Z half after FBX round-trip.",
            "Unity URP material remap and in-character grip verification remain intentionally deferred until user-approved import.",
        ],
    }

    REPORT_PATH.parent.mkdir(parents=True, exist_ok=True)
    REPORT_PATH.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"report": str(REPORT_PATH), "pass": report["pass"], "checks": checks}, indent=2))
    if not report["pass"]:
        raise RuntimeError("FBX reimport validation failed; inspect reimport_validation.json")


if __name__ == "__main__":
    main()
