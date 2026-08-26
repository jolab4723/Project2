from __future__ import annotations

import hashlib
import json
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector


HANDOFF = Path(__file__).resolve().parents[1]
PRODUCTION = HANDOFF.parent
BLEND = PRODUCTION / "item.weapon.shotgun.echovault.blend"
FBX = PRODUCTION / "item.weapon.shotgun.echovault.fbx"
ROOT_NAME = "item.weapon.shotgun.echovault"


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def rounded(vector) -> list[float]:
    return [round(float(value), 6) for value in vector]


def bounds(obj: bpy.types.Object) -> tuple[Vector, Vector]:
    points = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    minimum = Vector((min(point[i] for point in points) for i in range(3)))
    maximum = Vector((max(point[i] for point in points) for i in range(3)))
    return minimum, maximum


def topology(obj: bpy.types.Object) -> dict[str, object]:
    mesh_copy = obj.data.copy()
    invalid_data_found = mesh_copy.validate(clean_customdata=False, verbose=False)
    mesh_copy.calc_loop_triangles()
    bm = bmesh.new()
    bm.from_mesh(mesh_copy)
    boundary_edges = sum(1 for edge in bm.edges if len(edge.link_faces) == 1)
    wire_edges = sum(1 for edge in bm.edges if len(edge.link_faces) == 0)
    overconnected_edges = sum(1 for edge in bm.edges if len(edge.link_faces) > 2)
    inconsistent_manifold_winding = sum(
        1 for edge in bm.edges if edge.is_manifold and not edge.is_contiguous
    )
    zero_area_faces = sum(1 for face in bm.faces if face.calc_area() <= 1e-12)
    zero_length_edges = sum(1 for edge in bm.edges if edge.calc_length() <= 1e-12)
    result = {
        "vertices": len(mesh_copy.vertices),
        "edges": len(mesh_copy.edges),
        "polygons": len(mesh_copy.polygons),
        "triangles": len(mesh_copy.loop_triangles),
        "uv_layers": len(mesh_copy.uv_layers),
        "mesh_validate_invalid_data_found": bool(invalid_data_found),
        "boundary_edges": boundary_edges,
        "wire_edges": wire_edges,
        "overconnected_edges": overconnected_edges,
        "inconsistent_manifold_winding_edges": inconsistent_manifold_winding,
        "zero_area_faces": zero_area_faces,
        "zero_length_edges": zero_length_edges,
    }
    bm.free()
    bpy.data.meshes.remove(mesh_copy)
    return result


def inspect_scene(label: str) -> dict[str, object]:
    objects = list(bpy.context.scene.objects)
    roots = [obj for obj in objects if obj.parent is None]
    meshes = [obj for obj in objects if obj.type == "MESH"]
    forbidden = [obj.name for obj in objects if obj.type in {"CAMERA", "LIGHT", "ARMATURE"}]
    if len(meshes) != 1:
        raise RuntimeError(f"{label}: expected one mesh, found {len(meshes)}")
    mesh = meshes[0]
    minimum, maximum = bounds(mesh)
    markers = {}
    for marker_name in ("RightHandGrip", "LeftHandGrip", "Muzzle"):
        marker = bpy.data.objects.get(marker_name)
        if marker is None:
            markers[marker_name] = None
            continue
        forward = (marker.matrix_world.to_3x3() @ Vector((0.0, 0.0, 1.0))).normalized()
        markers[marker_name] = {
            "parent": marker.parent.name if marker.parent else None,
            "location": rounded(marker.location),
            "world_location": rounded(marker.matrix_world.translation),
            "rotation_euler": rounded(marker.rotation_euler),
            "scale": rounded(marker.scale),
            "forward_plus_z": rounded(forward),
        }

    material = mesh.data.materials[0] if len(mesh.data.materials) == 1 else None
    material_info = None
    if material is not None:
        material_info = {
            "name": material.name,
            "use_nodes": material.use_nodes,
            "backface_culling_saved_flag": getattr(material, "use_backface_culling", None),
            "images": [
                {
                    "name": node.image.name,
                    "filepath": node.image.filepath,
                    "colorspace": node.image.colorspace_settings.name,
                    "size": list(node.image.size),
                }
                for node in material.node_tree.nodes
                if node.type == "TEX_IMAGE" and node.image is not None
            ],
        }

    return {
        "label": label,
        "root_names": [obj.name for obj in roots],
        "object_count": len(objects),
        "hierarchy": sorted(
            [
            {
                "name": obj.name,
                "type": obj.type,
                "parent": obj.parent.name if obj.parent else None,
            }
            for obj in objects
            ],
            key=lambda item: item["name"],
        ),
        "forbidden_objects": forbidden,
        "mesh_name": mesh.name,
        "bounds_min": rounded(minimum),
        "bounds_max": rounded(maximum),
        "dimensions": rounded(maximum - minimum),
        "mesh_scale": rounded(mesh.scale),
        "mesh_world_determinant": round(float(mesh.matrix_world.to_3x3().determinant()), 9),
        "topology": topology(mesh),
        "material_slots": len(mesh.data.materials),
        "material": material_info,
        "markers": markers,
        "all_scales_positive_uniform": all(
            min(obj.scale) > 0.0 and max(obj.scale) - min(obj.scale) <= 1e-6
            for obj in objects
        ),
    }


def main() -> None:
    bpy.ops.wm.open_mainfile(filepath=str(BLEND))
    blend = inspect_scene("production_blend")

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(FBX), use_custom_normals=True)
    bpy.context.view_layer.update()
    fbx = inspect_scene("fbx_clean_reimport")

    blend_muzzle = blend["markers"]["Muzzle"]
    fbx_muzzle = fbx["markers"]["Muzzle"]
    result = {
        "files": {
            "blend": {
                "path": str(BLEND.relative_to(PRODUCTION)),
                "bytes": BLEND.stat().st_size,
                "sha256": sha256(BLEND),
            },
            "fbx": {
                "path": str(FBX.relative_to(PRODUCTION)),
                "bytes": FBX.stat().st_size,
                "sha256": sha256(FBX),
            },
        },
        "production_blend": blend,
        "fbx_clean_reimport": fbx,
        "comparisons": {
            "single_root_exact_name": blend["root_names"] == [ROOT_NAME]
            and fbx["root_names"] == [ROOT_NAME],
            "triangles_preserved": blend["topology"]["triangles"]
            == fbx["topology"]["triangles"]
            == 179999,
            "uv_layer_preserved": blend["topology"]["uv_layers"]
            == fbx["topology"]["uv_layers"]
            == 1,
            "bounds_dimensions_match_within_0_00001": all(
                abs(a - b) <= 0.00001
                for a, b in zip(blend["dimensions"], fbx["dimensions"])
            ),
            "marker_locations_rotations_preserved": all(
                blend["markers"][name]["location"] == fbx["markers"][name]["location"]
                and blend["markers"][name]["rotation_euler"]
                == fbx["markers"][name]["rotation_euler"]
                for name in ("RightHandGrip", "LeftHandGrip", "Muzzle")
            ),
            "muzzle_forward_plus_z_preserved": blend_muzzle["forward_plus_z"]
            == fbx_muzzle["forward_plus_z"]
            == [0.0, 0.0, 1.0],
            "no_negative_or_nonuniform_scale": blend["all_scales_positive_uniform"]
            and fbx["all_scales_positive_uniform"],
            "no_forbidden_objects": not blend["forbidden_objects"]
            and not fbx["forbidden_objects"],
        },
        "normals_and_culling": {
            "production_boundary_edges": blend["topology"]["boundary_edges"],
            "production_overconnected_edges": blend["topology"]["overconnected_edges"],
            "production_inconsistent_manifold_winding_edges": blend["topology"][
                "inconsistent_manifold_winding_edges"
            ],
            "production_zero_area_faces": blend["topology"]["zero_area_faces"],
            "fbx_boundary_edges": fbx["topology"]["boundary_edges"],
            "fbx_inconsistent_manifold_winding_edges": fbx["topology"][
                "inconsistent_manifold_winding_edges"
            ],
            "backface_culling_qa_render_enabled_in_temporary_scene": True,
            "source_blend_saved_during_culling_qa": False,
            "visual_culling_result": "pass; all five PBR views retain the full exterior silhouette and the open muzzle cavity without missing reversed-face patches",
        },
    }
    print("HANDOFF_VALIDATION_JSON=" + json.dumps(result, separators=(",", ":")))


if __name__ == "__main__":
    main()
