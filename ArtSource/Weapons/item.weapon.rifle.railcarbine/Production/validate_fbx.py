from __future__ import annotations

import json
from datetime import datetime, timezone
from pathlib import Path

import bpy
from mathutils import Vector


PRODUCTION = Path(__file__).resolve().parent
ROOT = PRODUCTION.parent
ITEM_ID = "item.weapon.rifle.railcarbine"
FBX = PRODUCTION / f"{ITEM_ID}.fbx"
VALIDATION = PRODUCTION / "validation.json"
REPORT = PRODUCTION / "QA" / "reimport_validation.json"
EXPECTED_TRIANGLES = 59_999


def v3(value) -> list[float]:
    return [round(float(value[i]), 6) for i in range(3)]


def triangle_count(obj: bpy.types.Object) -> int:
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def root_local_point(root: bpy.types.Object, obj: bpy.types.Object, point: Vector) -> Vector:
    return root.matrix_world.inverted() @ obj.matrix_world @ point


def nearest_surface_distance(root: bpy.types.Object, mesh: bpy.types.Object, marker: bpy.types.Object) -> float:
    marker_point = root.matrix_world.inverted() @ marker.matrix_world.translation
    return min((root_local_point(root, mesh, vertex.co) - marker_point).length for vertex in mesh.data.vertices)


def main() -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(FBX), use_custom_normals=True)
    objects = list(bpy.context.scene.objects)
    roots = [obj for obj in objects if obj.parent is None]
    root = bpy.data.objects.get(f"{ITEM_ID}_root")
    meshes = [obj for obj in objects if obj.type == "MESH"]
    markers = {name: bpy.data.objects.get(name) for name in ("RightHandGrip", "LeftHandGrip", "Muzzle")}
    errors = []
    if len(roots) != 1:
        errors.append(f"expected one top-level root, got {len(roots)}")
    if root is None:
        errors.append("missing expected direct item root")
    if len(meshes) != 1:
        errors.append(f"expected one mesh, got {len(meshes)}")
    if any(marker is None for marker in markers.values()):
        errors.append("missing one or more direct marker empties")
    mesh = meshes[0] if len(meshes) == 1 else None
    triangles = triangle_count(mesh) if mesh else None
    if triangles != EXPECTED_TRIANGLES:
        errors.append(f"triangle mismatch: expected {EXPECTED_TRIANGLES}, got {triangles}")

    marker_records, contacts = {}, {}
    if root is not None:
        for name, marker in markers.items():
            if marker is None:
                continue
            local = root.matrix_world.inverted() @ marker.matrix_world
            marker_records[name] = {
                "parent": marker.parent.name if marker.parent else None,
                "local_location": v3(local.translation), "local_scale": v3(local.to_scale()),
                "local_plus_z": v3(local.to_3x3().normalized() @ Vector((0.0, 0.0, 1.0))),
                "world_plus_z": v3(marker.matrix_world.to_3x3().normalized() @ Vector((0.0, 0.0, 1.0))),
            }
            if marker.parent != root:
                errors.append(f"{name} is not a direct root child")
            if mesh is not None and name != "Muzzle":
                contacts[name] = round(nearest_surface_distance(root, mesh, marker), 6)

    right = marker_records.get("RightHandGrip", {}).get("local_location")
    left = marker_records.get("LeftHandGrip", {}).get("local_location")
    muzzle_axis = marker_records.get("Muzzle", {}).get("local_plus_z")
    if right != [0.0, 0.0, 0.0]:
        errors.append(f"RightHandGrip is not at root origin: {right}")
    if not left or abs(left[2] - 0.32625) > 1e-4:
        errors.append(f"LeftHandGrip Z is not 0.32625m: {left}")
    if muzzle_axis != [0.0, 0.0, 1.0]:
        errors.append(f"Muzzle +Z mismatch: {muzzle_axis}")

    if root is not None:
        direct_markers = sorted(child.name for child in root.children if child.type == "EMPTY" and child.name in markers)
        if direct_markers != ["LeftHandGrip", "Muzzle", "RightHandGrip"]:
            errors.append(f"direct marker set mismatch: {direct_markers}")
        if v3(root.location) != [0.0, 0.0, 0.0] or v3(root.rotation_euler) != [0.0, 0.0, 0.0] or v3(root.scale) != [1.0, 1.0, 1.0]:
            errors.append(f"root transform not identity: loc={v3(root.location)} rot={v3(root.rotation_euler)} scale={v3(root.scale)}")
        root_up = v3(root.matrix_world.to_3x3().normalized() @ Vector((0.0, 1.0, 0.0)))
        if root_up != [0.0, 1.0, 0.0]:
            errors.append(f"root world +Y mismatch: {root_up}")
    else:
        direct_markers, root_up = [], None
    if mesh is not None and (v3(mesh.location) != [0.0, 0.0, 0.0] or v3(mesh.rotation_euler) != [0.0, 0.0, 0.0] or v3(mesh.scale) != [1.0, 1.0, 1.0]):
        errors.append(f"mesh transform not identity: loc={v3(mesh.location)} rot={v3(mesh.rotation_euler)} scale={v3(mesh.scale)}")

    prohibited = [{"name": obj.name, "type": obj.type} for obj in objects if obj.type in {"CAMERA", "LIGHT", "ARMATURE"} or "collider" in obj.name.lower()]
    if prohibited:
        errors.append(f"prohibited objects present: {prohibited}")
    non_uniform = []
    for obj in objects:
        scale = obj.scale
        if min(scale) <= 0.0 or max(scale) - min(scale) > 1e-5:
            non_uniform.append({"name": obj.name, "scale": v3(scale)})
    if non_uniform:
        errors.append(f"non-positive/non-uniform transforms: {non_uniform}")

    materials = []
    if mesh is not None:
        for slot in mesh.material_slots:
            material = slot.material
            images = []
            if material and material.use_nodes:
                for node in material.node_tree.nodes:
                    if node.bl_idname == "ShaderNodeTexImage" and node.image is not None:
                        images.append({"node": node.name, "image": node.image.name, "filepath": bpy.path.abspath(node.image.filepath)})
            materials.append({"slot": slot.name, "material": material.name if material else None, "use_nodes": bool(material and material.use_nodes), "image_textures": images})
    if not materials or any(record["material"] is None for record in materials):
        errors.append("missing reimported material slot")
    expected_paths = {name: PRODUCTION / "Textures" / f"{ITEM_ID}_{name}.png" for name in ("BaseColor", "Normal", "ORM", "Emission")}
    external = {name: {"path": str(path.relative_to(ROOT)), "exists": path.is_file()} for name, path in expected_paths.items()}
    if not all(record["exists"] for record in external.values()):
        errors.append(f"missing external PBR texture set: {external}")
    if materials and len(materials[0]["image_textures"]) < 3:
        errors.append("FBX material did not preserve BaseColor/Normal/Emission image slots")

    bounds_record = None
    if root is not None and mesh is not None:
        points = [root_local_point(root, mesh, Vector(corner)) for corner in mesh.bound_box]
        minimum = Vector((min(point[i] for point in points) for i in range(3)))
        maximum = Vector((max(point[i] for point in points) for i in range(3)))
        bounds_record = {"min": v3(minimum), "max": v3(maximum), "dimensions": v3(maximum - minimum)}
        muzzle_location = marker_records.get("Muzzle", {}).get("local_location")
        if not muzzle_location or abs(muzzle_location[2] - maximum.z) > 1e-3:
            errors.append(f"Muzzle is not on +Z barrel end: marker={muzzle_location}, maxZ={maximum.z}")

    transforms = {obj.name: {"type": obj.type, "location": v3(obj.location), "rotation": v3(obj.rotation_euler), "scale": v3(obj.scale)} for obj in objects}
    report = {
        "verified_at": datetime.now(timezone.utc).isoformat(), "blender_version": bpy.app.version_string,
        "method": "factory empty scene -> FBX import -> hierarchy/axis/transform/material/triangle/contact audit",
        "fbx": str(FBX.relative_to(ROOT)), "root": root.name if root else None,
        "top_level_roots": [obj.name for obj in roots], "direct_children": [obj.name for obj in root.children] if root else [],
        "direct_markers": direct_markers, "root_world_plus_y": root_up, "object_count": len(objects), "mesh_count": len(meshes),
        "triangles": triangles, "material_slots": len(mesh.material_slots) if mesh else None, "materials": materials,
        "external_pbr_textures": external, "bounds_root_local": bounds_record, "markers": marker_records,
        "grip_nearest_surface_distance_m": contacts, "transforms": transforms,
        "prohibited_objects": prohibited, "non_positive_or_non_uniform": non_uniform,
        "errors": errors, "pass": not errors,
    }
    REPORT.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    validation = json.loads(VALIDATION.read_text(encoding="utf-8"))
    validation["reimport"] = {"status": "passed" if report["pass"] else "failed", "report": str(REPORT.relative_to(ROOT)), "triangles": triangles, "root": report["root"], "markers": marker_records, "grip_nearest_surface_distance_m": contacts, "errors": errors}
    validation["checks"]["reimport_verified"] = report["pass"]
    validation["pass"] = all(validation["checks"].values())
    VALIDATION.write_text(json.dumps(validation, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"report": str(REPORT), "pass": report["pass"], "errors": errors, "contacts": contacts}, indent=2))
    if errors:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
