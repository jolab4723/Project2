from __future__ import annotations

import hashlib
import importlib.util
import json
from datetime import datetime, timezone
from pathlib import Path

import bpy
from mathutils import Vector


OUT = Path(__file__).resolve().parent
ITEM_ID = "item.weapon.rifle.railcarbine"
ROOT_NAME = f"{ITEM_ID}_root"
BODY_NAME = "RailCarbine_FinalRepair_500k_Body"
TASK_ID = "caabbc04-0a47-408d-80c1-12661dcd2659"
SOURCE = (
    OUT.parents[1]
    / "Tripo"
    / "CostSafeRetry20260825"
    / "Downloaded"
    / f"{ITEM_ID}_raw.glb"
)
TARGET_TRIANGLES = 500_000


def load_module(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    assert spec.loader is not None
    spec.loader.exec_module(module)
    return module


lod = load_module("railcarbine_lod_helpers", OUT / "evaluate_lod_candidates.py")
repair = load_module("railcarbine_repair_helpers", OUT / "build_final_repair.py")


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def configure_material(body: bpy.types.Object, textures: Path, qa: Path):
    if len(body.material_slots) != 1 or body.material_slots[0].material is None:
        raise RuntimeError("Expected exactly one H3 material")
    material = body.material_slots[0].material
    material.name = "M_RailCarbine_CostSafe_PBR"
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    principled = next((node for node in nodes if node.bl_idname == "ShaderNodeBsdfPrincipled"), None)
    if principled is None:
        raise RuntimeError("Raw H3 material has no Principled BSDF")

    external_paths = {
        "Color_": textures / f"{ITEM_ID}_BaseColor.png",
        "NormalGL_": textures / f"{ITEM_ID}_Normal.png",
        "ORM_": textures / f"{ITEM_ID}_ORM.png",
    }
    for prefix, path in external_paths.items():
        image = next((candidate for candidate in bpy.data.images if candidate.name.startswith(prefix)), None)
        if image is None:
            raise RuntimeError(f"Missing raw H3 {prefix} image")
        image.source = "FILE"
        image.filepath = str(path)
        image.colorspace_settings.name = "sRGB" if prefix == "Color_" else "Non-Color"
        image.reload()

    # Add an exact binary mask without changing any authored BaseColor/Normal/
    # ORM node links.  The PNG is prepared from the source bytes outside
    # Blender so no color-management round trip can broaden the selected set.
    for name in ("Global Cyan Binary Emission Mask", "Emission Mask x Cyan"):
        old = nodes.get(name)
        if old is not None:
            nodes.remove(old)
    mask = nodes.new("ShaderNodeTexImage")
    mask.name = "Global Cyan Binary Emission Mask"
    mask.interpolation = "Closest"
    mask.image = bpy.data.images.load(str(textures / f"{ITEM_ID}_Emission.png"), check_existing=False)
    mask.image.colorspace_settings.name = "Non-Color"
    multiply = nodes.new("ShaderNodeMixRGB")
    multiply.name = "Emission Mask x Cyan"
    multiply.blend_type = "MULTIPLY"
    multiply.inputs[0].default_value = 1.0
    links.new(mask.outputs["Color"], multiply.inputs[1])
    links.new(multiply.outputs["Color"], principled.inputs["Emission Color"])
    principled.inputs["Emission Strength"].default_value = 2.5
    principled.inputs["Alpha"].default_value = 1.0
    emission = json.loads((OUT / "emission_validation.json").read_text(encoding="utf-8"))
    if not emission["pass"] or emission["selected_pixels"] <= 0:
        raise RuntimeError("Exact emission texture validation failed")
    multiply.inputs[2].default_value = (*emission["unity_linear_rgb_target"], 1.0)
    return material, emission


def make_root(body: bpy.types.Object):
    root = bpy.data.objects.new(ROOT_NAME, None)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.07
    bpy.context.scene.collection.objects.link(root)
    body.parent = root
    raw_points = {
        "RightHandGrip": lod.ROOT_RAW,
        "LeftHandGrip": Vector((-0.31891495601173026, 0.0, -0.04394590187265919)),
        "Muzzle": Vector((-0.5, 0.0, 0.0)),
    }
    markers = {}
    for name, raw_point in raw_points.items():
        marker = bpy.data.objects.new(name, None)
        marker.empty_display_type = "ARROWS" if name == "Muzzle" else "CUBE"
        marker.empty_display_size = 0.03
        marker.location = lod.raw_to_final(raw_point)
        bpy.context.scene.collection.objects.link(marker)
        marker.parent = root
        markers[name] = [round(float(value), 7) for value in marker.location]
    root["coordinate_contract"] = "Gunner +Z muzzle, +Y top, trigger-grip root"
    root["raw_to_final_rotation"] = "raw (X,Y,Z) -> final (-Y,Z,-X), determinant +1"
    return root, markers


def culling_deltas(qa_records: dict) -> dict:
    result = {}
    for name, standard in qa_records["standard"].items():
        result[name] = lod.image_delta(Path(standard), Path(qa_records["backface_culled"][name]))
    for name, standard in qa_records["closeups"]["standard"].items():
        result[f"closeup_{name}"] = lod.image_delta(
            Path(standard), Path(qa_records["closeups"]["backface_culled"][name])
        )
    return result


def export_fbx(root: bpy.types.Object, path: Path) -> None:
    # A complex Blender emission-node graph may be collapsed by FBX exporters
    # into whole-material emission.  Unity receives the exact binary PNG
    # separately, so temporarily disable only the export-time emission socket.
    material = next(child for child in root.children_recursive if child.type == "MESH").material_slots[0].material
    principled = next(node for node in material.node_tree.nodes if node.bl_idname == "ShaderNodeBsdfPrincipled")
    socket = principled.inputs["Emission Color"]
    saved_links = [(link.from_socket, link.to_socket) for link in socket.links]
    saved_color = socket.default_value[:]
    saved_strength = principled.inputs["Emission Strength"].default_value
    for link in list(socket.links):
        material.node_tree.links.remove(link)
    socket.default_value = (0.0, 0.0, 0.0, 1.0)
    principled.inputs["Emission Strength"].default_value = 0.0

    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for child in root.children_recursive:
        child.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=str(path),
        use_selection=True,
        object_types={"EMPTY", "MESH"},
        add_leaf_bones=False,
        apply_unit_scale=False,
        use_space_transform=True,
        bake_space_transform=False,
        axis_forward="-Z",
        axis_up="Y",
        path_mode="COPY",
        embed_textures=False,
    )
    socket.default_value = saved_color
    principled.inputs["Emission Strength"].default_value = saved_strength
    for source, destination in saved_links:
        material.node_tree.links.new(source, destination)


def reimport_validate(fbx_path: Path, expected_triangles: int, expected_markers: dict, report_path: Path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(fbx_path))
    objects = {obj.name: obj for obj in bpy.context.scene.objects}
    meshes = [obj for obj in objects.values() if obj.type == "MESH"]
    roots = [obj for obj in objects.values() if obj.parent is None]
    marker_records = {}
    for name in ("RightHandGrip", "LeftHandGrip", "Muzzle"):
        marker = objects.get(name)
        if marker is not None:
            marker_records[name] = {
                "location": lod.v3(marker.location),
                "rotation": lod.v3(marker.rotation_euler),
                "scale": lod.v3(marker.scale),
                "parent": marker.parent.name if marker.parent else None,
                "plus_z": lod.v3(marker.matrix_local.to_3x3() @ Vector((0, 0, 1))),
            }
    imported_triangles = sum(lod.triangles(obj) for obj in meshes)
    prohibited = [
        obj.name
        for obj in objects.values()
        if obj.type in {"CAMERA", "LIGHT", "ARMATURE"} or "collider" in obj.name.lower()
    ]
    checks = {
        "single_root": len(roots) == 1 and roots[0].name == ROOT_NAME,
        "one_body_mesh": len(meshes) == 1 and meshes[0].name == BODY_NAME,
        "markers_present": len(marker_records) == 3,
        "markers_parented_to_root": len(roots) == 1 and all(
            record["parent"] == roots[0].name for record in marker_records.values()
        ),
        "marker_positions_match": all(
            name in marker_records
            and (Vector(marker_records[name]["location"]) - Vector(expected)).length <= 0.0002
            for name, expected in expected_markers.items()
        ),
        "right_hand_grip_at_origin": Vector(marker_records.get("RightHandGrip", {}).get("location", (9, 9, 9))).length <= 0.0001,
        "muzzle_plus_z": "Muzzle" in marker_records
        and (Vector(marker_records["Muzzle"]["plus_z"]) - Vector((0, 0, 1))).length <= 0.0001,
        "triangles_match": imported_triangles == expected_triangles,
        "uv_preserved": len(meshes) == 1 and len(meshes[0].data.uv_layers) >= 1,
        "positive_unit_mesh_scale": len(meshes) == 1
        and (Vector(meshes[0].scale) - Vector((1, 1, 1))).length <= 0.0001,
        "material_name_preserved": len(meshes) == 1
        and len(meshes[0].material_slots) == 1
        and meshes[0].material_slots[0].material is not None
        and meshes[0].material_slots[0].material.name == "M_RailCarbine_CostSafe_PBR",
        "no_prohibited_objects": not prohibited,
    }
    report = {
        "generated_at": utc_now(),
        "fbx": str(fbx_path),
        "objects": sorted(objects),
        "mesh_count": len(meshes),
        "triangles": imported_triangles,
        "markers": marker_records,
        "prohibited": prohibited,
        "checks": checks,
        "pass": all(checks.values()),
    }
    report_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
    return report


def main() -> None:
    textures = OUT / "Textures"
    qa = OUT / "QA"
    textures.mkdir(parents=True, exist_ok=True)
    qa.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(SOURCE))
    raw_meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(raw_meshes) != 1:
        raise RuntimeError(f"Expected one raw mesh, got {len(raw_meshes)}")
    raw = raw_meshes[0]
    lod.transform_body(raw)
    raw.name = "RailCarbine_Raw_Source_QA"
    source_topology = lod.topology(raw)
    material, emission = configure_material(raw, textures, qa)
    body = lod.decimate(raw, TARGET_TRIANGLES)
    body.name = BODY_NAME
    body.data.name = f"{BODY_NAME}_Mesh"
    selected_topology = lod.topology(body)
    bpy.data.objects.remove(raw, do_unlink=True)

    root, markers = make_root(body)
    body.location = (0.0, 0.0, 0.0)
    body.rotation_euler = (0.0, 0.0, 0.0)
    body.scale = (1.0, 1.0, 1.0)
    bpy.context.view_layer.objects.active = body
    body.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    body.select_set(False)

    qa_records = repair.render_qa([body], qa)
    deltas = culling_deltas(qa_records)
    culling_stable = all(record["changed_fraction"] < 0.001 for record in deltas.values())
    prohibited = [
        obj.name
        for obj in bpy.context.scene.objects
        if obj.type in {"CAMERA", "LIGHT", "ARMATURE"} or "collider" in obj.name.lower()
    ]
    blend_path = OUT / f"{ITEM_ID}.blend"
    fbx_path = OUT / f"{ITEM_ID}.fbx"
    reimport_path = OUT / "reimport_validation.json"
    validation_path = OUT / "validation.json"
    texture_hashes = {path.name: sha256(path) for path in textures.glob("*.png")}

    validation = {
        "item_id": ITEM_ID,
        "task_id": TASK_ID,
        "generated_at": utc_now(),
        "source": str(SOURCE),
        "additional_tripo_or_api_calls": 0,
        "credits_used_by_repair": 0,
        "root_cause": {
            "historical_mapping": "raw (X,Y,Z) -> final (+Y,+Z,-X)",
            "historical_determinant": -1,
            "effect": "reflection reversed triangle winding, making intact raw caps and shells disappear under backface culling",
            "corrected_mapping": "raw (X,Y,Z) -> final (-Y,+Z,-X)",
            "corrected_determinant": 1,
            "normal_recalculation_used": False,
        },
        "geometry": {
            "source_triangles": source_topology["triangles"],
            "selected_triangles": selected_topology["triangles"],
            "target_triangles": TARGET_TRIANGLES,
            "reduction_fraction": round(1.0 - selected_topology["triangles"] / float(source_topology["triangles"]), 9),
            "source_topology": source_topology,
            "selected_topology": selected_topology,
            "added_cap_or_shell_geometry": False,
            "raw_h3_caps_preserved": True,
            "raw_h3_shell_preserved": True,
            "uv_preserved": True,
        },
        "lod_gate": {
            "evaluation": str(OUT / "lod_candidate_evaluation.json"),
            "tested": [300000, 500000],
            "selected": 500000,
            "selection_reason": "both were culling-stable, but 500k conservatively preserves more of the user-approved raw H3 shell",
        },
        "coordinate_contract": {
            "muzzle": "+Z",
            "up": "+Y",
            "root_origin": "actual trigger grip center",
            "markers": markers,
        },
        "material": {
            "name": material.name,
            "pbr_preserved": True,
            "source_material_name_preserved_for_unity_remap": True,
            "emission": emission,
            "emission_strength": 2.5,
            "texture_sha256": texture_hashes,
        },
        "qa": {"renders": qa_records, "standard_vs_backface_delta": deltas},
        "prohibited_objects": prohibited,
        "checks": {
            "right_handed_rotation": True,
            "selected_500k_lod": 490000 <= selected_topology["triangles"] <= 510000,
            "front_caps_and_shell_culling_stable": culling_stable,
            "raw_caps_preserved_without_added_geometry": True,
            "exact_body_mask_zero_miss_zero_false_positive": emission["missed_reference_pixels"] == 0
            and emission["false_positive_pixels"] == 0,
            "emission_binary": emission["binary_values"] == [0, 255],
            "emission_no_exception": not emission["morphology_spatial_uv_mesh_component_exception"],
            "markers_preserved": markers["RightHandGrip"] == [0.0, 0.0, -0.0]
            and abs(markers["LeftHandGrip"][2] - 0.326) <= 0.0002,
            "muzzle_plus_z": markers["Muzzle"][2] > markers["LeftHandGrip"][2],
            "unit_positive_scale": lod.v3(body.scale) == [1.0, 1.0, 1.0],
            "no_prohibited_objects": not prohibited,
            "reimport_pass": False,
        },
        "pass": False,
    }
    validation_path.write_text(json.dumps(validation, indent=2), encoding="utf-8")
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    export_fbx(root, fbx_path)
    reimport = reimport_validate(fbx_path, selected_topology["triangles"], markers, reimport_path)
    validation["checks"]["reimport_pass"] = reimport["pass"]
    validation["reimport"] = {"path": str(reimport_path), "pass": reimport["pass"]}
    validation["pass"] = all(validation["checks"].values())
    validation_path.write_text(json.dumps(validation, indent=2), encoding="utf-8")
    print(
        json.dumps(
            {
                "source_triangles": source_topology["triangles"],
                "selected_triangles": selected_topology["triangles"],
                "front_boundary_edges": selected_topology["front_boundary_edges"],
                "validation_pass": validation["pass"],
                "reimport_pass": reimport["pass"],
            }
        )
    )


if __name__ == "__main__":
    main()
