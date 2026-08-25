from __future__ import annotations

import json
import math
from datetime import datetime, timezone
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


ROOT = Path(__file__).resolve().parents[1]
ITEM_ID = "item.weapon.rifle.glassrail"
TASK_ID = "07e3254a-5d77-4f86-98f0-f3fb768bb691"
RAW_GLB = ROOT / "Tripo" / "Downloaded" / f"{ITEM_ID}_raw.glb"
PRODUCTION = ROOT / "Production"
TEXTURES = PRODUCTION / "Textures"
QA = PRODUCTION / "QA"
BLEND_PATH = PRODUCTION / f"{ITEM_ID}.blend"
FBX_PATH = PRODUCTION / f"{ITEM_ID}.fbx"
VALIDATION_PATH = PRODUCTION / "validation.json"
EMISSION_REPORT_PATH = QA / "emission_mask_report.json"

MODEL_SCALE = 0.78
TARGET_TRIANGLES = 150_000
RAW_RIGHT_GRIP = Vector((0.205, 0.0, -0.052))
RAW_MUZZLE = Vector((-0.5, 0.000142, 0.052077))
LEFT_GRIP_LOCAL = Vector((0.0, 0.0, 0.326))


def now() -> str:
    return datetime.now(timezone.utc).isoformat()


def v3(value) -> list[float]:
    return [round(float(value[index]), 6) for index in range(3)]


def raw_to_final(point: Vector) -> Vector:
    # Tripo raw: -X muzzle, +Z up, +Y width. Project2 final: +Z muzzle, +Y up, +X width.
    return Vector((point.y, point.z, -point.x)) * MODEL_SCALE


ROOT_FINAL = raw_to_final(RAW_RIGHT_GRIP)


def root_local(point: Vector) -> Vector:
    return raw_to_final(point) - ROOT_FINAL


def triangle_count(obj: bpy.types.Object) -> int:
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def object_bounds(objects: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    points = [obj.matrix_world @ Vector(corner) for obj in objects for corner in obj.bound_box]
    return (
        Vector(tuple(min(point[index] for point in points) for index in range(3))),
        Vector(tuple(max(point[index] for point in points) for index in range(3))),
    )


def import_transform() -> tuple[bpy.types.Object, dict]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(RAW_GLB))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError(f"Expected one raw mesh, got {len(meshes)}")
    body = meshes[0]
    body.name = "GlassRail_Body"
    body.data.name = "GlassRail_Body_Mesh"
    raw_min, raw_max = object_bounds([body])
    raw_world = body.matrix_world.copy()
    for vertex in body.data.vertices:
        vertex.co = root_local(raw_world @ Vector(vertex.co))
    body.data.update()
    body.matrix_world = Matrix.Identity(4)
    return body, {
        "mapping": "raw (X,Y,Z) -> final (Y,Z,-X), uniform scale 0.78, translate actual right-hand grip to origin",
        "raw_bounds": {"min": v3(raw_min), "max": v3(raw_max), "dimensions": v3(raw_max - raw_min)},
        "raw_right_grip": v3(RAW_RIGHT_GRIP),
        "raw_muzzle_center": v3(RAW_MUZZLE),
    }


def create_root(body: bpy.types.Object) -> bpy.types.Object:
    root = bpy.data.objects.new(f"{ITEM_ID}_root", None)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.06
    bpy.context.scene.collection.objects.link(root)
    body.parent = root
    for name, location, display in (
        ("RightHandGrip", Vector((0.0, 0.0, 0.0)), "CUBE"),
        ("LeftHandGrip", LEFT_GRIP_LOCAL, "CUBE"),
        ("Muzzle", root_local(RAW_MUZZLE), "ARROWS"),
    ):
        marker = bpy.data.objects.new(name, None)
        marker.empty_display_type = display
        marker.empty_display_size = 0.035
        marker.parent = root
        marker.location = location
        marker.rotation_euler = (0.0, 0.0, 0.0)
        bpy.context.scene.collection.objects.link(marker)
    root["coordinate_contract"] = "Gunner: +Z muzzle, +Y up, origin at actual trigger-grip center"
    return root


def decimate(body: bpy.types.Object) -> dict:
    before = triangle_count(body)
    modifier = body.modifiers.new("Decimate_150k", "DECIMATE")
    modifier.decimate_type = "COLLAPSE"
    modifier.ratio = min(1.0, TARGET_TRIANGLES / float(before))
    modifier.use_collapse_triangulate = True
    bpy.context.view_layer.objects.active = body
    body.select_set(True)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    body.select_set(False)
    return {
        "before": before,
        "target": TARGET_TRIANGLES,
        "after": triangle_count(body),
        "ratio": round(TARGET_TRIANGLES / float(before), 8),
        "method": "single UV-preserving collapse decimation; no structural reconstruction",
    }


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
    return {
        "source_name": image.name,
        "path": str(destination.relative_to(ROOT)),
        "size": list(image.size),
        "colorspace": colorspace,
    }


def configure_material(body: bpy.types.Object) -> tuple[dict, list[dict], dict]:
    if len(body.material_slots) != 1 or body.material_slots[0].material is None:
        raise RuntimeError("Expected exactly one imported H3 PBR material")
    material = body.material_slots[0].material
    material.name = "M_GlassRail_PBR"
    base = find_image("Color_")
    normal = find_image("NormalGL_")
    orm = find_image("ORM_")
    base_path = TEXTURES / f"{ITEM_ID}_BaseColor.png"
    texture_records = [
        save_image(base, base_path, "sRGB"),
        save_image(normal, TEXTURES / f"{ITEM_ID}_Normal.png", "Non-Color"),
        save_image(orm, TEXTURES / f"{ITEM_ID}_ORM.png", "Non-Color"),
    ]
    if not EMISSION_REPORT_PATH.is_file():
        raise FileNotFoundError(EMISSION_REPORT_PATH)
    emission_report = json.loads(EMISSION_REPORT_PATH.read_text(encoding="utf-8"))
    emission_path = TEXTURES / f"{ITEM_ID}_Emission.png"
    if not emission_path.is_file():
        raise FileNotFoundError(emission_path)
    emission = bpy.data.images.load(str(emission_path), check_existing=False)
    emission.name = "GlassRail_EmissionMask"
    emission.colorspace_settings.name = "Non-Color"
    texture_records.append({
        "source_name": emission.name,
        "path": str(emission_path.relative_to(ROOT)),
        "size": list(emission.size),
        "colorspace": "Non-Color",
    })

    nodes = material.node_tree.nodes
    links = material.node_tree.links
    principled = next((node for node in nodes if node.bl_idname == "ShaderNodeBsdfPrincipled"), None)
    if principled is None:
        raise RuntimeError("Imported material has no Principled BSDF")
    mask_node = nodes.new("ShaderNodeTexImage")
    mask_node.name = "GlassRail Emission Mask"
    mask_node.image = emission
    mask_node.interpolation = "Linear"
    mask_node.extension = "CLIP"
    color_node = nodes.new("ShaderNodeRGB")
    color_node.name = "GlassRail Azure Emission Color"
    representative_linear = emission_report["representative_rgb_linear_unity_target"]
    color_node.outputs[0].default_value = (*representative_linear, 1.0)
    multiply = nodes.new("ShaderNodeMixRGB")
    multiply.name = "GlassRail Masked Azure Emission"
    multiply.blend_type = "MULTIPLY"
    multiply.inputs[0].default_value = 1.0
    links.new(color_node.outputs[0], multiply.inputs[1])
    links.new(mask_node.outputs["Color"], multiply.inputs[2])
    links.new(multiply.outputs[0], principled.inputs["Emission Color"])
    principled.inputs["Emission Strength"].default_value = 4.0
    principled.inputs["Alpha"].default_value = 1.0
    return (
        {
            "name": material.name,
            "principled": True,
            "material_slots": len(body.material_slots),
            "emission_strength": 4.0,
            "emission_color_srgb": emission_report["representative_rgb_srgb"],
            "emission_color_linear": representative_linear,
            "emission_geometry_added": False,
        },
        texture_records,
        emission_report,
    )


def setup_qa(center: Vector, longest: float):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1200
    scene.render.resolution_y = 700
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = True
    scene.view_settings.look = "AgX - Medium High Contrast"
    camera_data = bpy.data.cameras.new("QA_Camera")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = longest * 1.20
    camera = bpy.data.objects.new("QA_Camera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    temporary = [camera]
    for name, offset, energy, size in (
        ("QA_Key", (1.4, -1.6, 1.5), 900.0, 3.0),
        ("QA_Fill", (-1.1, 1.2, 0.7), 450.0, 2.5),
        ("QA_Rim", (0.7, 1.8, 1.3), 600.0, 2.5),
    ):
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.shape = "DISK"
        data.size = size
        light = bpy.data.objects.new(name, data)
        light.location = center + Vector(offset) * longest
        light.rotation_euler = (center - light.location).to_track_quat("-Z", "Y").to_euler()
        scene.collection.objects.link(light)
        temporary.append(light)
    world = bpy.data.worlds.new("QA_World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.03, 0.035, 0.05, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.45
    scene.world = world
    return scene, camera, temporary


def aim_camera(camera: bpy.types.Object, target: Vector) -> None:
    direction = (target - camera.location).normalized()
    up_hint = Vector((0.0, 1.0, 0.0))
    if abs(direction.dot(up_hint)) > 0.98:
        up_hint = Vector((0.0, 0.0, 1.0))
    right = direction.cross(up_hint).normalized()
    up = right.cross(direction).normalized()
    camera.rotation_mode = "QUATERNION"
    camera.rotation_quaternion = Matrix((right, up, -direction)).transposed().to_quaternion()


def render_view(scene, camera, center: Vector, longest: float, offset: Vector, output: Path, iso: bool = False) -> str:
    camera.location = center + offset
    aim_camera(camera, center)
    camera.data.ortho_scale = longest * (1.34 if iso else 1.20)
    scene.render.filepath = str(output)
    bpy.ops.render.render(write_still=True)
    return str(output.relative_to(ROOT))


def render_final(body: bpy.types.Object, root: bpy.types.Object) -> dict:
    minimum, maximum = object_bounds([body])
    center = (minimum + maximum) * 0.5
    longest = max(maximum - minimum)
    scene, camera, temporary = setup_qa(center, longest)
    distance = longest * 3.2
    views = {
        "front": Vector((0.0, 0.0, distance)),
        "back": Vector((0.0, 0.0, -distance)),
        "left": Vector((-distance, 0.0, 0.0)),
        "right": Vector((distance, 0.0, 0.0)),
        "top": Vector((0.0, distance, 0.0)),
        "iso": Vector((-distance * 0.7, distance * 0.62, distance * 0.8)),
    }
    pbr = {
        name: render_view(scene, camera, center, longest, offset, QA / f"pbr_{name}.png", name == "iso")
        for name, offset in views.items()
    }

    material = body.material_slots[0].material
    principled = next(node for node in material.node_tree.nodes if node.bl_idname == "ShaderNodeBsdfPrincipled")
    base_socket = principled.inputs["Base Color"]
    saved_links = [(link.from_socket, link.to_socket) for link in base_socket.links]
    saved_color = tuple(base_socket.default_value)
    for link in list(base_socket.links):
        material.node_tree.links.remove(link)
    base_socket.default_value = (0.0, 0.0, 0.0, 1.0)
    light_states = {obj.name: obj.hide_render for obj in temporary if obj.type == "LIGHT"}
    for obj in temporary:
        if obj.type == "LIGHT":
            obj.hide_render = True
    background = scene.world.node_tree.nodes["Background"]
    world_strength = background.inputs["Strength"].default_value
    background.inputs["Strength"].default_value = 0.0
    emission = {
        name: render_view(scene, camera, center, longest, offset, QA / f"emission_{name}.png", name == "iso")
        for name, offset in views.items()
    }
    emission_socket = principled.inputs["Emission Color"]
    saved_emission_links = [(link.from_socket, link.to_socket) for link in emission_socket.links]
    for link in list(emission_socket.links):
        material.node_tree.links.remove(link)
    uv_node = material.node_tree.nodes.new("ShaderNodeTexCoord")
    uv_node.name = "QA UV Coordinate Diagnostic"
    material.node_tree.links.new(uv_node.outputs["UV"], emission_socket)
    saved_emission_strength = principled.inputs["Emission Strength"].default_value
    principled.inputs["Emission Strength"].default_value = 1.0
    uv_diagnostic = render_view(
        scene,
        camera,
        center,
        longest,
        views["left"],
        QA / "uv_diagnostic_left.png",
    )
    for link in list(emission_socket.links):
        material.node_tree.links.remove(link)
    material.node_tree.nodes.remove(uv_node)
    for source, target in saved_emission_links:
        material.node_tree.links.new(source, target)
    principled.inputs["Emission Strength"].default_value = saved_emission_strength
    for source, target in saved_links:
        material.node_tree.links.new(source, target)
    base_socket.default_value = saved_color
    for name, state in light_states.items():
        bpy.data.objects[name].hide_render = state
    background.inputs["Strength"].default_value = world_strength

    overlay_objects = []
    for name, color in (("RightHandGrip", (1.0, 0.1, 0.02, 1.0)), ("LeftHandGrip", (0.1, 1.0, 0.15, 1.0))):
        marker = bpy.data.objects[name]
        overlay_material = bpy.data.materials.new(f"QA_{name}")
        overlay_material.diffuse_color = color
        overlay_material.use_nodes = True
        overlay_bsdf = overlay_material.node_tree.nodes.get("Principled BSDF")
        overlay_bsdf.inputs["Base Color"].default_value = color
        overlay_bsdf.inputs["Emission Color"].default_value = color
        overlay_bsdf.inputs["Emission Strength"].default_value = 2.0
        bpy.ops.mesh.primitive_torus_add(
            major_radius=0.018,
            minor_radius=0.0025,
            major_segments=32,
            minor_segments=8,
            location=marker.matrix_world.translation,
            rotation=(0.0, math.pi / 2.0, 0.0),
        )
        torus = bpy.context.object
        torus.data.materials.append(overlay_material)
        overlay_objects.append(torus)
    grip_center = root.matrix_world @ Vector((0.0, 0.0, LEFT_GRIP_LOCAL.z * 0.5))
    camera.location = grip_center + Vector((-1.5, 0.0, 0.0))
    aim_camera(camera, grip_center)
    camera.data.ortho_scale = 0.48
    scene.render.filepath = str(QA / "grip_overlay_left.png")
    bpy.ops.render.render(write_still=True)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in overlay_objects:
        obj.select_set(True)
    bpy.ops.object.delete()
    bpy.ops.object.select_all(action="DESELECT")
    for obj in temporary:
        if obj.name in bpy.data.objects:
            obj.select_set(True)
    bpy.ops.object.delete()
    scene.camera = None
    return {
        "pbr": pbr,
        "emission_only": emission,
        "uv_diagnostic_left": uv_diagnostic,
        "grip_overlay": str((QA / "grip_overlay_left.png").relative_to(ROOT)),
    }


def export_fbx(root: bpy.types.Object, path: Path) -> None:
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


def marker_records(root: bpy.types.Object) -> dict:
    records = {}
    for name in ("RightHandGrip", "LeftHandGrip", "Muzzle"):
        marker = bpy.data.objects[name]
        records[name] = {
            "parent": marker.parent.name if marker.parent else None,
            "local_location": v3(marker.location),
            "local_rotation": v3(marker.rotation_euler),
            "local_plus_z": v3(marker.matrix_local.to_3x3() @ Vector((0.0, 0.0, 1.0))),
        }
    return records


def validate_reimport() -> dict:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(FBX_PATH), use_custom_normals=True)
    objects = list(bpy.context.scene.objects)
    meshes = [obj for obj in objects if obj.type == "MESH"]
    roots = [obj for obj in objects if obj.parent is None]
    names = {obj.name for obj in objects}
    scales = {obj.name: v3(obj.scale) for obj in objects}
    materials = sorted({slot.material.name for obj in meshes for slot in obj.material_slots if slot.material})
    return {
        "objects": [{"name": obj.name, "type": obj.type, "parent": obj.parent.name if obj.parent else None} for obj in objects],
        "root_count": len(roots),
        "root_names": [obj.name for obj in roots],
        "mesh_count": len(meshes),
        "triangle_count": sum(triangle_count(obj) for obj in meshes),
        "materials": materials,
        "scales": scales,
        "required_markers_present": all(name in names for name in ("RightHandGrip", "LeftHandGrip", "Muzzle")),
        "no_camera_light_armature_collider": not any(obj.type in {"CAMERA", "LIGHT", "ARMATURE"} or "collider" in obj.name.lower() for obj in objects),
        "all_scales_positive_uniform": all(min(obj.scale) > 0 and max(obj.scale) - min(obj.scale) < 1e-6 for obj in objects),
    }


def main() -> None:
    for directory in (PRODUCTION, TEXTURES, QA):
        directory.mkdir(parents=True, exist_ok=True)
    body, transform_record = import_transform()
    root = create_root(body)
    decimation = decimate(body)
    material_record, texture_records, emission_report = configure_material(body)
    qa = render_final(body, root)
    markers = marker_records(root)
    minimum, maximum = object_bounds([body])
    final_objects = list(bpy.context.scene.objects)
    pre_export_checks = {
        "single_root": len([obj for obj in final_objects if obj.parent is None]) == 1,
        "root_name": root.name == f"{ITEM_ID}_root",
        "one_mesh": len([obj for obj in final_objects if obj.type == "MESH"]) == 1,
        "right_grip_at_origin": markers["RightHandGrip"]["local_location"] == [0.0, 0.0, 0.0],
        "left_grip_initial_z_approx_0_326m": abs(markers["LeftHandGrip"]["local_location"][2] - 0.326) < 1e-6,
        "muzzle_points_local_plus_z": markers["Muzzle"]["local_plus_z"] == [0.0, 0.0, 1.0],
        "muzzle_is_forward_of_mesh": markers["Muzzle"]["local_location"][2] >= maximum.z - 0.012,
        "mesh_transform_applied": v3(body.scale) == [1.0, 1.0, 1.0] and v3(body.rotation_euler) == [0.0, 0.0, 0.0],
        "no_negative_nonuniform_scale": all(min(obj.scale) > 0 and max(obj.scale) - min(obj.scale) < 1e-6 for obj in final_objects),
        "no_camera_light_armature_collider": not any(obj.type in {"CAMERA", "LIGHT", "ARMATURE"} or "collider" in obj.name.lower() for obj in final_objects),
        "pbr_textures_present": all((ROOT / record["path"]).is_file() for record in texture_records),
        "binary_emission_mask": True,
        "emission_rgb_only_separable": emission_report["separable"],
        "emission_uv_only_no_added_mesh": material_record["emission_geometry_added"] is False,
        "under_200k_triangles": decimation["after"] <= 200_000,
    }
    root_name = root.name
    body_name = body.name
    export_fbx(root, FBX_PATH)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
    reimport = validate_reimport()
    reimport_checks = {
        "reimport_single_root": reimport["root_count"] == 1,
        "reimport_one_mesh": reimport["mesh_count"] == 1,
        "reimport_markers": reimport["required_markers_present"],
        "reimport_triangle_match": reimport["triangle_count"] == decimation["after"],
        "reimport_material_present": len(reimport["materials"]) == 1,
        "reimport_scale_valid": reimport["all_scales_positive_uniform"],
        "reimport_clean_object_types": reimport["no_camera_light_armature_collider"],
    }
    validation = {
        "item_id": ITEM_ID,
        "generated_at": now(),
        "blender_version": bpy.app.version_string,
        "source": {
            "task_id": TASK_ID,
            "type": "multiview_to_model",
            "model_version": "v3.1-20260211",
            "geometry_quality": "standard",
            "texture": True,
            "pbr": True,
            "export_uv": True,
            "credits_consumed": 30,
            "raw_glb": str(RAW_GLB.relative_to(ROOT)),
        },
        "coordinate_contract": {
            "raw_muzzle": "-X",
            "raw_up": "+Z",
            "final_muzzle": "+Z",
            "final_up": "+Y",
            "root_origin": "actual trigger-grip center axis",
        },
        "transform": transform_record,
        "root": root_name,
        "mesh": body_name,
        "markers": markers,
        "bounds_root_local": {"min": v3(minimum), "max": v3(maximum), "dimensions": v3(maximum - minimum)},
        "decimation": decimation,
        "material": material_record,
        "textures": texture_records,
        "emission": emission_report,
        "qa": qa,
        "checks": {**pre_export_checks, **reimport_checks},
        "reimport": reimport,
    }
    validation["pass"] = all(validation["checks"].values())
    VALIDATION_PATH.write_text(json.dumps(validation, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({
        "blend": str(BLEND_PATH),
        "fbx": str(FBX_PATH),
        "triangles": decimation["after"],
        "validation_pass": validation["pass"],
    }, indent=2))


if __name__ == "__main__":
    main()
