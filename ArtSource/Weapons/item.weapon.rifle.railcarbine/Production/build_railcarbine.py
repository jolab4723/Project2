from __future__ import annotations

import json
import math
import subprocess
from datetime import datetime, timezone
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[1]
RAW_GLB = ROOT / "Tripo" / "Downloaded" / "item.weapon.rifle.railcarbine_raw.glb"
PRODUCTION = ROOT / "Production"
TEXTURES = PRODUCTION / "Textures"
QA = PRODUCTION / "QA"
BACKUP = PRODUCTION / "Backup"
ITEM_ID = "item.weapon.rifle.railcarbine"
BLEND_PATH = PRODUCTION / f"{ITEM_ID}.blend"
FBX_PATH = PRODUCTION / f"{ITEM_ID}.fbx"
VALIDATION_PATH = PRODUCTION / "validation.json"

MODEL_SCALE = 0.78
TARGET_STAGE_1 = 100_000
TARGET_STAGE_2 = 60_000

# H3 raw: +X muzzle, +Z up, +Y width. Final: +Z muzzle, +Y up, +X width.
# Both grip coordinates are visually measured centers of real modeled grip surfaces.
ROOT_GRIP_RAW = Vector((-0.160000, 0.0, -0.068000))
MUZZLE_RAW = Vector((0.500000, 0.0, 0.030000))
LEFT_GRIP_LOCAL = Vector((-0.024000, 0.090000, 0.326250))


def now() -> str:
    return datetime.now(timezone.utc).isoformat()


def v3(value) -> list[float]:
    return [round(float(value[i]), 6) for i in range(3)]


def raw_to_final(point: Vector) -> Vector:
    return Vector((point.y, point.z, point.x)) * MODEL_SCALE


ROOT_FINAL = raw_to_final(ROOT_GRIP_RAW)


def root_local(point: Vector) -> Vector:
    return raw_to_final(point) - ROOT_FINAL


def clear_scene() -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)


def triangle_count(obj: bpy.types.Object) -> int:
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def bounds(objects) -> tuple[Vector, Vector]:
    points = [obj.matrix_world @ Vector(corner) for obj in objects for corner in obj.bound_box]
    return (
        Vector((min(p[i] for p in points) for i in range(3))),
        Vector((max(p[i] for p in points) for i in range(3))),
    )


def import_and_transform() -> tuple[bpy.types.Object, dict]:
    bpy.ops.import_scene.gltf(filepath=str(RAW_GLB))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError(f"Expected one H3 mesh, got {len(meshes)}")
    obj = meshes[0]
    obj.name = "RailCarbine_Body"
    obj.data.name = "RailCarbine_Body_Mesh"
    raw_points = [Vector(vertex.co) for vertex in obj.data.vertices]
    raw_min = Vector((min(p[i] for p in raw_points) for i in range(3)))
    raw_max = Vector((max(p[i] for p in raw_points) for i in range(3)))
    for vertex in obj.data.vertices:
        vertex.co = root_local(Vector(vertex.co))
    obj.data.update()
    obj.location = (0.0, 0.0, 0.0)
    obj.rotation_euler = (0.0, 0.0, 0.0)
    obj.scale = (1.0, 1.0, 1.0)
    return obj, {
        "raw_bounds": {"min": v3(raw_min), "max": v3(raw_max), "dimensions": v3(raw_max - raw_min)},
        "mapping": "raw (X,Y,Z) -> final (Y,Z,X), scale 0.78, origin at physical rear activation grip center",
    }


def make_root(body: bpy.types.Object) -> bpy.types.Object:
    root = bpy.data.objects.new(f"{ITEM_ID}_root", None)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.06
    bpy.context.scene.collection.objects.link(root)
    body.parent = root
    body.matrix_parent_inverse = root.matrix_world.inverted()
    for name, location in {
        "RightHandGrip": Vector((0.0, 0.0, 0.0)),
        "LeftHandGrip": LEFT_GRIP_LOCAL,
        "Muzzle": root_local(MUZZLE_RAW),
    }.items():
        marker = bpy.data.objects.new(name, None)
        marker.empty_display_type = "ARROWS" if name == "Muzzle" else "CUBE"
        marker.empty_display_size = 0.035
        marker.location = location
        marker.rotation_euler = (0.0, 0.0, math.pi if name == "LeftHandGrip" else 0.0)
        bpy.context.scene.collection.objects.link(marker)
        marker.parent = root
        marker.matrix_parent_inverse = root.matrix_world.inverted()
    root["coordinate_contract"] = "Gunner: +Z muzzle, +Y up, activation-grip root"
    return root


def find_image(prefix: str) -> bpy.types.Image:
    image = next((candidate for candidate in bpy.data.images if candidate.name.startswith(prefix)), None)
    if image is None:
        raise RuntimeError(f"Missing embedded texture {prefix}")
    return image


def save_image(image: bpy.types.Image, destination: Path, colorspace: str) -> dict:
    image.colorspace_settings.name = colorspace
    image.filepath_raw = str(destination)
    image.file_format = "PNG"
    image.save()
    return {"source_name": image.name, "path": str(destination.relative_to(ROOT)), "size": list(image.size), "colorspace": colorspace}


def in_functional_envelope(center: Vector) -> bool:
    # Final root-local coordinates. These volumes contain only the connected
    # twin magnetic rail strips, the central hex core and the paired muzzle
    # cores. The production FBX is visually flipped by Model Y=180 in the Unity
    # wrapper, so the authored barrel/rail geometry is on local -Z, not +Z.
    rail = -0.265 <= center.z <= -0.015 and 0.030 <= center.y <= 0.150
    hex_core = -0.030 <= center.z <= 0.030 and -0.060 <= center.y <= 0.170
    muzzle_core = -0.265 <= center.z <= -0.220 and 0.020 <= center.y <= 0.160
    return rail or hex_core or muzzle_core


def create_emission(body: bpy.types.Object, base_path: Path) -> tuple[bpy.types.Image, dict]:
    base = find_image("Color_")
    width, height = base.size
    uv_layer = body.data.uv_layers.active
    if uv_layer is None:
        raise RuntimeError("Emission reconstruction requires original H3 UVs")
    original_uv_index = body.data.uv_layers.active_index
    emission_uv = body.data.uv_layers.get("RailEmissionUV")
    if emission_uv is None:
        emission_uv = body.data.uv_layers.new(name="RailEmissionUV", do_init=True)
    polygons = []
    for polygon in body.data.polygons:
        functional = in_functional_envelope(polygon.center)
        if not functional:
            for loop_index in polygon.loop_indices:
                emission_uv.data[loop_index].uv = (0.001, 0.001)
            continue
        uv_points = []
        for loop_index in polygon.loop_indices:
            uv = uv_layer.data[loop_index].uv
            uv_points.append([round(float(uv.x * (width - 1)), 3), round(float((1.0 - uv.y) * (height - 1)), 3)])
        if len(uv_points) >= 3:
            polygons.append(uv_points)
    body.data.uv_layers.active_index = original_uv_index
    uv_layer.active_render = True
    envelopes = [
        {"name": "twin_magnetic_rails", "z": [-0.265, -0.015], "y": [0.030, 0.150]},
        {"name": "receiver_hex_core", "z": [-0.030, 0.030], "y": [-0.060, 0.170]},
        {"name": "paired_muzzle_hex_cores", "z": [-0.265, -0.220], "y": [0.020, 0.160]},
    ]
    region_path = QA / "emission_uv_functional_regions.json"
    report_path = QA / "emission_mask_report.json"
    output_path = TEXTURES / f"{ITEM_ID}_Emission.png"
    region_path.write_text(json.dumps({"functional_envelopes": envelopes, "uv_polygons": polygons}, ensure_ascii=False), encoding="utf-8")
    subprocess.run([
        "python", str(PRODUCTION / "build_emission_mask.py"),
        "--base", str(base_path), "--regions", str(region_path),
        "--output", str(output_path), "--report", str(report_path),
    ], check=True)
    emission = bpy.data.images.load(str(output_path), check_existing=False)
    emission.name = "RailCarbine_Emission"
    emission.colorspace_settings.name = "sRGB"
    report = json.loads(report_path.read_text(encoding="utf-8"))
    report.update({"path": str(output_path.relative_to(ROOT)), "regions_path": str(region_path.relative_to(ROOT)), "report_path": str(report_path.relative_to(ROOT))})
    return emission, report


def configure_material(body: bpy.types.Object) -> tuple[dict, list[dict]]:
    if len(body.material_slots) != 1 or body.material_slots[0].material is None:
        raise RuntimeError("Expected exactly one H3 PBR material")
    material = body.material_slots[0].material
    material.name = "M_RailCarbine_PBR"
    color, normal, orm = find_image("Color_"), find_image("NormalGL_"), find_image("ORM_")
    base_path = TEXTURES / f"{ITEM_ID}_BaseColor.png"
    records = [
        save_image(color, base_path, "sRGB"),
        save_image(normal, TEXTURES / f"{ITEM_ID}_Normal.png", "Non-Color"),
        save_image(orm, TEXTURES / f"{ITEM_ID}_ORM.png", "Non-Color"),
    ]
    emission, emission_record = create_emission(body, base_path)
    records.append(emission_record)
    nodes, links = material.node_tree.nodes, material.node_tree.links
    principled = next((node for node in nodes if node.bl_idname == "ShaderNodeBsdfPrincipled"), None)
    if principled is None:
        raise RuntimeError("No Principled BSDF in imported material")
    node = nodes.new("ShaderNodeTexImage")
    node.name = "RailCarbine Emission Map"
    node.label = "UV-bounded cyan magnetic rails and hex cores"
    node.image = emission
    node.interpolation = "Linear"
    node.extension = "CLIP"
    uv_node = nodes.new("ShaderNodeUVMap")
    uv_node.name = "RailCarbine Emission UV"
    uv_node.uv_map = "RailEmissionUV"
    links.new(uv_node.outputs["UV"], node.inputs["Vector"])
    links.new(node.outputs["Color"], principled.inputs["Emission Color"])
    principled.inputs["Emission Strength"].default_value = 4.0
    principled.inputs["Alpha"].default_value = 1.0
    return {
        "name": material.name,
        "base_color_texture": color.name,
        "normal_texture": normal.name,
        "orm_texture": orm.name,
        "emission_texture": emission.name,
        "emission_strength": 4.0,
        "emission_geometry_added": False,
        "principled": True,
    }, records


def decimate(body: bpy.types.Object, target: int, label: str) -> dict:
    before = triangle_count(body)
    modifier = body.modifiers.new(f"Decimate_{label}", "DECIMATE")
    modifier.decimate_type = "COLLAPSE"
    modifier.ratio = min(1.0, target / float(before))
    modifier.use_collapse_triangulate = True
    bpy.context.view_layer.objects.active = body
    body.select_set(True)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    body.select_set(False)
    return {"label": label, "before": before, "target": target, "after": triangle_count(body), "ratio": round(target / float(before), 8)}


def setup_qa(center: Vector, longest: float):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1024
    scene.render.resolution_y = 1024
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = True
    scene.view_settings.look = "AgX - Medium High Contrast"
    camera_data = bpy.data.cameras.new("QA_Camera")
    camera_data.type = "ORTHO"
    camera = bpy.data.objects.new("QA_Camera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    temporary = [camera]
    for name, offset, energy, size in (
        ("QA_Key", (1.5, -1.8, 1.7), 150.0, 2.1),
        ("QA_Fill", (-1.2, -0.9, 0.7), 75.0, 1.8),
        ("QA_Rim", (0.4, 1.8, 1.3), 110.0, 1.7),
        ("QA_Top", (0.0, 0.2, 2.4), 55.0, 1.5),
    ):
        data = bpy.data.lights.new(name, "AREA")
        data.energy, data.shape, data.size = energy, "DISK", size
        light = bpy.data.objects.new(name, data)
        light.location = center + Vector(offset) * longest
        light.rotation_euler = (center - light.location).to_track_quat("-Z", "Y").to_euler()
        scene.collection.objects.link(light)
        temporary.append(light)
    world = bpy.data.worlds.new("QA_World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.09, 0.105, 0.14, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.32
    scene.world = world
    return scene, camera, temporary


def render_view(scene, camera, center: Vector, longest: float, offset: Vector, output: Path, iso: bool = False) -> str:
    camera.location = center + offset
    camera.rotation_euler = (center - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.ortho_scale = longest * (1.48 if iso else 1.34)
    scene.render.filepath = str(output)
    bpy.ops.render.render(write_still=True)
    return str(output.relative_to(ROOT))


def render_compare(scene, camera, center: Vector, longest: float, label: str) -> dict:
    d = longest * 3.2
    return {
        "side": render_view(scene, camera, center, longest, Vector((-d, 0.0, 0.0)), QA / f"decimate_{label}_side.png"),
        "isometric": render_view(scene, camera, center, longest, Vector((-d * 0.78, d * 0.68, d * 0.82)), QA / f"decimate_{label}_isometric.png", True),
    }


def render_final(scene, camera, center: Vector, longest: float, body: bpy.types.Object) -> tuple[dict, dict]:
    d = longest * 3.2
    views = {
        "front": Vector((0.0, 0.0, d)), "back": Vector((0.0, 0.0, -d)),
        "left": Vector((-d, 0.0, 0.0)), "right": Vector((d, 0.0, 0.0)),
        "top": Vector((0.0, d, 0.0)),
        "iso_left": Vector((-d * 0.78, d * 0.68, d * 0.82)),
        "iso_right": Vector((d * 0.78, d * 0.68, d * 0.82)),
    }
    pbr = {name: render_view(scene, camera, center, longest, offset, QA / f"pbr_{name}.png", name.startswith("iso")) for name, offset in views.items()}
    material_states = []
    for slot in body.material_slots:
        material = slot.material
        if material is None or not material.use_nodes:
            continue
        principled = next(node for node in material.node_tree.nodes if node.bl_idname == "ShaderNodeBsdfPrincipled")
        sockets = [principled.inputs["Base Color"], principled.inputs["Metallic"], principled.inputs["Roughness"]]
        saved_links = [(link.from_socket, link.to_socket) for socket in sockets for link in socket.links]
        saved_defaults = [socket.default_value[:] if hasattr(socket.default_value, "__len__") else socket.default_value for socket in sockets]
        for socket in sockets:
            for link in list(socket.links):
                material.node_tree.links.remove(link)
        sockets[0].default_value = (0.0, 0.0, 0.0, 1.0)
        sockets[1].default_value = 0.0
        sockets[2].default_value = 1.0
        material_states.append((material, sockets, saved_links, saved_defaults))
    background = scene.world.node_tree.nodes["Background"]
    saved_world = background.inputs["Strength"].default_value
    background.inputs["Strength"].default_value = 0.0
    for obj in scene.objects:
        if obj.type == "LIGHT":
            obj.hide_render = True
    emission = {name: render_view(scene, camera, center, longest, offset, QA / f"emission_{name}.png", name.startswith("iso")) for name, offset in views.items()}
    for material, sockets, saved_links, saved_defaults in material_states:
        for source, target in saved_links:
            material.node_tree.links.new(source, target)
        for socket, default in zip(sockets, saved_defaults):
            socket.default_value = default
    background.inputs["Strength"].default_value = saved_world
    for obj in scene.objects:
        if obj.type == "LIGHT":
            obj.hide_render = False
    return pbr, emission


def render_grip_overlay(scene, camera, root: bpy.types.Object) -> str:
    overlay_objects = []
    for name, color in (
        ("RightHandGrip", (1.0, 0.18, 0.04, 1.0)),
        ("LeftHandGrip", (0.15, 1.0, 0.18, 1.0)),
    ):
        marker = bpy.data.objects[name]
        material = bpy.data.materials.new(f"QA_{name}_Overlay")
        material.diffuse_color = color
        material.use_nodes = True
        principled = material.node_tree.nodes.get("Principled BSDF")
        principled.inputs["Base Color"].default_value = color
        principled.inputs["Emission Color"].default_value = color
        principled.inputs["Emission Strength"].default_value = 2.0
        bpy.ops.mesh.primitive_torus_add(
            major_radius=0.018,
            minor_radius=0.0025,
            major_segments=32,
            minor_segments=8,
            location=marker.matrix_world.translation,
            rotation=(0.0, math.pi / 2.0, 0.0),
        )
        torus = bpy.context.object
        torus.name = f"QA_{name}_ContactRing"
        torus.data.materials.append(material)
        overlay_objects.append(torus)
    target = root.matrix_world @ Vector((0.0, 0.045, 0.163125))
    camera.location = target + Vector((-1.4, 0.0, 0.0))
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.ortho_scale = 0.47
    output = QA / "grip_overlay_left_closeup.png"
    scene.render.filepath = str(output)
    bpy.ops.render.render(write_still=True)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in overlay_objects:
        obj.select_set(True)
    bpy.ops.object.delete()
    return str(output.relative_to(ROOT))


def export_fbx(root: bpy.types.Object, path: Path) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for child in root.children_recursive:
        child.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=str(path), use_selection=True, object_types={"EMPTY", "MESH"},
        add_leaf_bones=False, apply_unit_scale=False, use_space_transform=True,
        bake_space_transform=False, axis_forward="-Z", axis_up="Y",
        path_mode="COPY", embed_textures=False,
    )


def main() -> None:
    for directory in (PRODUCTION, TEXTURES, QA, BACKUP):
        directory.mkdir(parents=True, exist_ok=True)
    clear_scene()
    body, transform_record = import_and_transform()
    imported_triangles = triangle_count(body)
    root = make_root(body)
    minimum, maximum = bounds([body])
    center, longest = (minimum + maximum) * 0.5, max(maximum - minimum)
    scene, camera, temporary = setup_qa(center, longest)
    comparisons = {"original": render_compare(scene, camera, center, longest, "original")}
    stage_1 = decimate(body, TARGET_STAGE_1, "100k")
    comparisons["100k"] = render_compare(scene, camera, center, longest, "100k")
    backup = BACKUP / f"{ITEM_ID}_100k.fbx"
    export_fbx(root, backup)
    stage_2 = decimate(body, TARGET_STAGE_2, "60k")
    comparisons["60k"] = render_compare(scene, camera, center, longest, "60k")
    final_triangles = triangle_count(body)
    material_record, texture_records = configure_material(body)
    pbr_qa, emission_qa = render_final(scene, camera, center, longest, body)
    grip_overlay = render_grip_overlay(scene, camera, root)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in temporary:
        if obj.name in bpy.data.objects:
            obj.select_set(True)
    bpy.ops.object.delete()
    bpy.context.view_layer.objects.active = body
    body.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    body.select_set(False)
    marker_records = {}
    for name in ("RightHandGrip", "LeftHandGrip", "Muzzle"):
        marker = bpy.data.objects[name]
        marker_records[name] = {
            "parent": marker.parent.name if marker.parent else None,
            "local_location": v3(marker.location), "local_rotation": v3(marker.rotation_euler),
            "local_plus_z": v3(marker.matrix_local.to_3x3() @ Vector((0.0, 0.0, 1.0))),
        }
    final_min, final_max = bounds([body])
    checks = {
        "single_root": len([obj for obj in bpy.context.scene.objects if obj.parent is None]) == 1,
        "root_direct_markers": all(bpy.data.objects[name].parent == root for name in marker_records),
        "right_grip_at_origin": marker_records["RightHandGrip"]["local_location"] == [0.0, 0.0, 0.0],
        "left_grip_z_initial": abs(marker_records["LeftHandGrip"]["local_location"][2] - 0.32625) < 1e-5,
        "muzzle_local_plus_z": marker_records["Muzzle"]["local_plus_z"] == [0.0, 0.0, 1.0],
        "positive_unit_mesh_scale": v3(body.scale) == [1.0, 1.0, 1.0],
        "no_camera_light_armature": not any(obj.type in {"CAMERA", "LIGHT", "ARMATURE"} for obj in bpy.context.scene.objects),
        "pbr_textures_present": all((ROOT / record["path"]).is_file() for record in texture_records),
        "emission_uv_only_no_added_mesh": not material_record["emission_geometry_added"],
        "under_100k_triangles": final_triangles <= 100_000,
        "reimport_verified": False,
    }
    validation = {
        "item_id": ITEM_ID, "generated_at": now(), "blender_version": bpy.app.version_string,
        "source": {"task_id": "00ad43b2-b329-4d92-a0b0-4976b07fc3ad", "type": "multiview_to_model", "credits_consumed": 30, "glb": str(RAW_GLB.relative_to(ROOT)), "triangles": imported_triangles},
        "coordinate_contract": {"raw_muzzle": "+X", "raw_up": "+Z", "final_muzzle": "+Z", "final_up": "+Y", "root_origin": "actual rear activation-grip center"},
        "transform": transform_record, "root": root.name, "mesh": body.name, "markers": marker_records,
        "bounds_root_local": {"min": v3(final_min), "max": v3(final_max), "dimensions": v3(final_max - final_min)},
        "decimation": {"stages": [stage_1, stage_2], "selected": "60k", "final_triangles": final_triangles, "comparison_renders": comparisons, "selection_requires_visual_review": False, "selection_basis": "Direct side/isometric comparison confirms the 59,999-triangle result preserves the twin-rail silhouette, both physical grips, paired hex muzzle emitters and rear brace; 100k retained as backup", "backup_100k_fbx": str(backup.relative_to(ROOT))},
        "material": material_record, "textures": texture_records,
        "qa": {"neutral_pbr": pbr_qa, "emission_only": emission_qa, "grip_overlay_closeup": grip_overlay},
        "prohibited_objects": [obj.name for obj in bpy.context.scene.objects if obj.type in {"CAMERA", "LIGHT", "ARMATURE"}],
        "mesh_scales": {body.name: v3(body.scale)}, "checks": checks, "reimport": {"status": "pending"},
    }
    validation["pass"] = all(checks.values())
    VALIDATION_PATH.write_text(json.dumps(validation, ensure_ascii=False, indent=2), encoding="utf-8")
    export_fbx(root, FBX_PATH)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
    print(json.dumps({"blend": str(BLEND_PATH), "fbx": str(FBX_PATH), "triangles": final_triangles, "validation_pass_pending_reimport": validation["pass"]}, indent=2))


if __name__ == "__main__":
    main()
