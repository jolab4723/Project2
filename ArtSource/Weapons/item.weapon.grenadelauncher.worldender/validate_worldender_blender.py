from __future__ import annotations

import json
from datetime import datetime, timezone
from math import cos, sin, tau
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector


ROOT = Path(__file__).resolve().parent
PRODUCTION = ROOT / "Production"
ITEM_ID = "item.weapon.grenadelauncher.worldender"
FBX = PRODUCTION / f"{ITEM_ID}.fbx"
VALIDATION = PRODUCTION / "validation.json"
REPORT = PRODUCTION / "QA" / "reimport_validation.json"


def v3(value) -> list[float]:
    return [round(float(value[i]), 6) for i in range(3)]


def triangle_count(obj: bpy.types.Object) -> int:
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def root_local_point(root, obj, point: Vector) -> Vector:
    return root.matrix_world.inverted() @ obj.matrix_world @ point


def nearest_surface_distance(root, mesh, marker) -> float:
    point = root.matrix_world.inverted() @ marker.matrix_world.translation
    return min((root_local_point(root, mesh, vertex.co) - point).length for vertex in mesh.data.vertices)


def ray_cast_root_local(root, mesh, origin: Vector, direction: Vector):
    origin_world = root.matrix_world @ origin
    direction_world = root.matrix_world.to_3x3() @ direction
    origin_mesh = mesh.matrix_world.inverted() @ origin_world
    direction_mesh = (mesh.matrix_world.inverted().to_3x3() @ direction_world).normalized()
    hit, location, normal, face = mesh.ray_cast(origin_mesh, direction_mesh, distance=2.0)
    location_root = root.matrix_world.inverted() @ mesh.matrix_world @ location if hit else None
    return hit, location_root, normal, face


def topology_record(mesh) -> dict:
    bm = bmesh.new()
    bm.from_mesh(mesh.data)
    boundary = sum(1 for edge in bm.edges if edge.is_boundary)
    non_manifold = sum(1 for edge in bm.edges if not edge.is_manifold)
    loose = sum(1 for edge in bm.edges if edge.is_wire)
    degenerate = sum(1 for face in bm.faces if face.calc_area() <= 1.0e-12)
    bm.free()
    return {
        "boundary_edges": boundary,
        "non_manifold_edges_including_boundaries": non_manifold,
        "loose_edges": loose,
        "degenerate_faces": degenerate,
    }


def radial_material_coverage(root, mesh, material_name: str, z_levels: list[float], center_y: float, samples: int = 24) -> dict:
    levels = []
    for z_value in z_levels:
        hits = 0
        material_hits = 0
        for index in range(samples):
            angle = tau * index / samples
            direction = Vector((cos(angle), sin(angle), 0.0))
            hit, _location, _normal, face_index = ray_cast_root_local(
                root,
                mesh,
                Vector((0.0, center_y, z_value)),
                direction,
            )
            hits += int(hit)
            if hit and face_index >= 0:
                polygon = mesh.data.polygons[face_index]
                slot = mesh.material_slots[polygon.material_index] if polygon.material_index < len(mesh.material_slots) else None
                material_hits += int(bool(slot and slot.material and slot.material.name == material_name))
        levels.append({"z": z_value, "samples": samples, "hits": hits, "material_hits": material_hits})
    return {
        "material": material_name,
        "levels": levels,
        "complete": all(level["material_hits"] == samples for level in levels),
    }


def main() -> None:
    expected = json.loads(VALIDATION.read_text(encoding="utf-8"))
    expected_triangles = expected["decimation"]["final_triangles"]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(FBX), use_custom_normals=True)
    objects = list(bpy.context.scene.objects)
    roots = [obj for obj in objects if obj.parent is None]
    root = bpy.data.objects.get(f"{ITEM_ID}_root")
    meshes = [obj for obj in objects if obj.type == "MESH"]
    mesh = meshes[0] if len(meshes) == 1 else None
    markers = {name: bpy.data.objects.get(name) for name in ("RightHandGrip", "LeftHandGrip", "Muzzle")}
    errors = []
    if len(roots) != 1: errors.append(f"expected one top-level root, got {len(roots)}")
    if root is None: errors.append(f"missing root {ITEM_ID}_root")
    if len(meshes) != 1: errors.append(f"expected one mesh, got {len(meshes)}")
    if any(marker is None for marker in markers.values()): errors.append("missing one or more marker empties")
    tri_count = triangle_count(mesh) if mesh else None
    if tri_count != expected_triangles: errors.append(f"triangle mismatch: expected {expected_triangles}, got {tri_count}")

    marker_records, contact_distances = {}, {}
    if root:
        for name, marker in markers.items():
            if marker is None: continue
            local = root.matrix_world.inverted() @ marker.matrix_world
            marker_records[name] = {
                "parent": marker.parent.name if marker.parent else None,
                "local_location": v3(local.translation), "local_scale": v3(local.to_scale()),
                "local_plus_z": v3(local.to_3x3().normalized() @ Vector((0.0, 0.0, 1.0))),
                "world_plus_z": v3(marker.matrix_world.to_3x3().normalized() @ Vector((0.0, 0.0, 1.0))),
            }
            if marker.parent != root: errors.append(f"{name} is not a direct root child")
            if mesh and name != "Muzzle": contact_distances[name] = round(nearest_surface_distance(root, mesh, marker), 6)
    right = marker_records.get("RightHandGrip", {}).get("local_location")
    left = marker_records.get("LeftHandGrip", {}).get("local_location")
    muzzle_axis = marker_records.get("Muzzle", {}).get("local_plus_z")
    if right != [0.0, 0.0, 0.0]: errors.append(f"RightHandGrip origin mismatch: {right}")
    if not left or abs(left[2] - 0.32625) > 1e-4: errors.append(f"LeftHandGrip Z mismatch: {left}")
    if muzzle_axis != [0.0, 0.0, 1.0]: errors.append(f"Muzzle +Z mismatch: {muzzle_axis}")

    direct_markers = []
    root_up = None
    if root:
        direct_markers = sorted(child.name for child in root.children if child.name in markers)
        if direct_markers != ["LeftHandGrip", "Muzzle", "RightHandGrip"]: errors.append(f"direct marker set mismatch: {direct_markers}")
        if v3(root.location) != [0.0, 0.0, 0.0] or v3(root.rotation_euler) != [0.0, 0.0, 0.0] or v3(root.scale) != [1.0, 1.0, 1.0]: errors.append("root transform is not applied/unit")
        root_up = v3(root.matrix_world.to_3x3().normalized() @ Vector((0.0, 1.0, 0.0)))
        if root_up != [0.0, 1.0, 0.0]: errors.append(f"root +Y up mismatch: {root_up}")
    if mesh and (v3(mesh.location) != [0.0, 0.0, 0.0] or v3(mesh.rotation_euler) != [0.0, 0.0, 0.0] or v3(mesh.scale) != [1.0, 1.0, 1.0]): errors.append("mesh transform is not applied/unit")

    prohibited = [{"name": obj.name, "type": obj.type} for obj in objects if obj.type in {"CAMERA", "LIGHT", "ARMATURE"} or "collider" in obj.name.lower()]
    if prohibited: errors.append(f"prohibited objects present: {prohibited}")
    non_uniform = [{"name": obj.name, "scale": v3(obj.scale)} for obj in objects if min(obj.scale) <= 0.0 or max(obj.scale) - min(obj.scale) > 1e-5]
    if non_uniform: errors.append(f"non-positive/non-uniform transforms: {non_uniform}")

    materials = []
    if mesh:
        for slot in mesh.material_slots:
            material = slot.material
            images = []
            if material and material.use_nodes:
                for node in material.node_tree.nodes:
                    if node.bl_idname == "ShaderNodeTexImage" and node.image:
                        images.append({"node": node.name, "image": node.image.name, "filepath": bpy.path.abspath(node.image.filepath)})
            materials.append({"slot": slot.name, "material": material.name if material else None, "use_nodes": bool(material and material.use_nodes), "image_textures": images})
    if not materials or any(record["material"] is None for record in materials): errors.append("missing material slot")
    materials_by_name = {record["material"]: record for record in materials if record["material"]}
    base_record = materials_by_name.get("M_Worldender_PBR")
    core_record = materials_by_name.get("M_Worldender_SealedCore_PBR_Emission")
    bell_inner_record = materials_by_name.get("M_Worldender_BellInner_PBR")
    bell_rim_record = materials_by_name.get("M_Worldender_BellRim_PBR")
    cap_record = materials_by_name.get("M_Worldender_StockPadCap_PBR")
    if not all((base_record, core_record, bell_inner_record, bell_rim_record, cap_record)):
        errors.append(f"expected base/core/bell-inner/bell-rim/cap material slots, got {sorted(materials_by_name)}")
    if base_record:
        base_images = {record["image"] for record in base_record["image_textures"]}
        if not any("Color_" in name for name in base_images) or not any("NormalGL_" in name for name in base_images):
            errors.append("PBR-only base slot did not retain BaseColor/Normal")
        if any("Emission" in name for name in base_images):
            errors.append("ordinary body PBR slot unexpectedly retained emission")
    if core_record:
        core_images = {record["image"] for record in core_record["image_textures"]}
        if not any("Color_" in name for name in core_images) or not any("NormalGL_" in name for name in core_images) or not any("Emission" in name for name in core_images):
            errors.append("sealed-core slot did not retain BaseColor/Normal/Emission")
    if bell_inner_record and any("Emission" in record["image"] for record in bell_inner_record["image_textures"]):
        errors.append("bell inner repair must be non-emissive")
    if bell_rim_record and any("Emission" in record["image"] for record in bell_rim_record["image_textures"]):
        errors.append("bell rim repair must be non-emissive")
    if cap_record and any("Emission" in record["image"] for record in cap_record["image_textures"]):
        errors.append("stock cap must be non-emissive")
    external_paths = {name: PRODUCTION / "Textures" / f"{ITEM_ID}_{name}.png" for name in ("BaseColor", "Normal", "ORM", "Emission")}
    external = {name: {"path": str(path.relative_to(ROOT)), "exists": path.is_file()} for name, path in external_paths.items()}
    if not all(record["exists"] for record in external.values()): errors.append("missing external PBR texture set")

    bounds_record = None
    structural_repair = None
    material_face_usage = None
    if root and mesh:
        points = [root_local_point(root, mesh, Vector(corner)) for corner in mesh.bound_box]
        minimum = Vector((min(point[i] for point in points) for i in range(3)))
        maximum = Vector((max(point[i] for point in points) for i in range(3)))
        bounds_record = {"min": v3(minimum), "max": v3(maximum), "dimensions": v3(maximum - minimum)}
        muzzle = marker_records.get("Muzzle", {}).get("local_location")
        if not muzzle or abs(muzzle[2] - maximum.z) > 1e-3: errors.append(f"Muzzle is not at +Z barrel end: marker={muzzle}, maxZ={maximum.z}")
        bell_origin = Vector((0.0, 0.108, maximum.z + 0.02))
        bell_hit, bell_location, _normal, _face = ray_cast_root_local(root, mesh, bell_origin, Vector((0.0, 0.0, -1.0)))
        bell_clear_depth = float(maximum.z - bell_location.z) if bell_hit else 2.0
        stock_origin = Vector((0.0, 0.100, minimum.z - 0.02))
        stock_hit, stock_location, _normal, _face = ray_cast_root_local(root, mesh, stock_origin, Vector((0.0, 0.0, 1.0)))
        stock_hit_distance = float(stock_location.z - stock_origin.z) if stock_hit else None
        topology = topology_record(mesh)
        expected_bell = expected["structural_repair"]["bell_closed_reconstruction"]
        gold_coverage = radial_material_coverage(
            root,
            mesh,
            expected_bell["gold"]["material"],
            expected_bell["gold"]["probe_z_levels"],
            expected_bell["center_y"],
        )
        rim_coverage = radial_material_coverage(
            root,
            mesh,
            expected_bell["rim"]["material"],
            expected_bell["rim"]["probe_z_levels"],
            expected_bell["center_y"],
        )
        structural_repair = {
            "bell_first_hit_root_local": v3(bell_location) if bell_location else None,
            "bell_clear_depth_m": round(bell_clear_depth, 6),
            "stock_first_hit_root_local": v3(stock_location) if stock_location else None,
            "stock_cap_hit_distance_m": round(stock_hit_distance, 6) if stock_hit_distance is not None else None,
            "topology": topology,
            "gold_radial_coverage": gold_coverage,
            "rim_radial_coverage": rim_coverage,
        }
        if bell_clear_depth < 0.08: errors.append(f"reimport bell aperture is not open deeply enough: {bell_clear_depth:.6f}m")
        if stock_hit_distance is None or stock_hit_distance > 0.03: errors.append(f"reimport stock bore is not flush-sealed: {stock_hit_distance}")
        if topology["loose_edges"] != 0: errors.append(f"repair introduced loose edges: {topology['loose_edges']}")
        if topology["degenerate_faces"] != 0: errors.append(f"repair introduced degenerate faces: {topology['degenerate_faces']}")
        if not gold_coverage["complete"]: errors.append(f"reimport gold bell coverage is incomplete: {gold_coverage}")
        if not rim_coverage["complete"]: errors.append(f"reimport crimson bell rim coverage is incomplete: {rim_coverage}")
        material_face_usage = {}
        envelope = expected["textures"][-1]["functional_envelope"]
        envelope_min = Vector(envelope["min"])
        envelope_max = Vector(envelope["max"])
        core_outside = 0
        for index, slot in enumerate(mesh.material_slots):
            faces = [polygon for polygon in mesh.data.polygons if polygon.material_index == index]
            centers = [root_local_point(root, mesh, polygon.center) for polygon in faces]
            material_face_usage[slot.material.name if slot.material else slot.name] = {
                "face_count": len(faces),
                "center_bounds": {
                    "min": v3(Vector((min(point[i] for point in centers) for i in range(3)))),
                    "max": v3(Vector((max(point[i] for point in centers) for i in range(3)))),
                } if centers else None,
            }
            if slot.material and slot.material.name == "M_Worldender_SealedCore_PBR_Emission":
                core_outside = sum(
                    1 for point in centers
                    if not all(envelope_min[axis] - 1.0e-4 <= point[axis] <= envelope_max[axis] + 1.0e-4 for axis in range(3))
                )
        if core_outside:
            errors.append(f"sealed-core emission material leaked outside functional envelope: {core_outside} faces")
        material_face_usage["sealed_core_outside_envelope_faces"] = core_outside
    report = {
        "verified_at": datetime.now(timezone.utc).isoformat(), "blender_version": bpy.app.version_string,
        "method": "factory empty scene -> FBX import -> hierarchy/axis/transform/material/triangle audit",
        "fbx": str(FBX.relative_to(ROOT)), "root": root.name if root else None,
        "top_level_roots": [obj.name for obj in roots], "direct_children": sorted(obj.name for obj in root.children) if root else [],
        "direct_markers": direct_markers, "root_world_plus_y": root_up, "object_count": len(objects),
        "mesh_count": len(meshes), "triangles": tri_count, "material_slots": len(mesh.material_slots) if mesh else None,
        "materials": materials, "material_face_usage": material_face_usage,
        "external_pbr_textures": external, "bounds_root_local": bounds_record,
        "structural_repair_reimport": structural_repair,
        "markers": marker_records, "grip_nearest_surface_distance_m": contact_distances,
        "transforms": {obj.name: {"type": obj.type, "location": v3(obj.location), "rotation": v3(obj.rotation_euler), "scale": v3(obj.scale)} for obj in objects},
        "prohibited_objects": prohibited, "non_positive_or_non_uniform": non_uniform,
        "errors": errors, "pass": not errors,
    }
    REPORT.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    expected["reimport"] = {"status": "passed" if report["pass"] else "failed", "report": str(REPORT.relative_to(ROOT)), "triangles": tri_count, "root": report["root"], "markers": marker_records, "grip_nearest_surface_distance_m": contact_distances, "errors": errors}
    expected["checks"]["reimport_verified"] = report["pass"]
    expected["pass"] = all(expected["checks"].values())
    VALIDATION.write_text(json.dumps(expected, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"report": str(REPORT), "pass": report["pass"], "errors": errors}, indent=2))
    if errors: raise SystemExit(1)


if __name__ == "__main__":
    main()
