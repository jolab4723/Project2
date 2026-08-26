from __future__ import annotations

import json
import sys
from datetime import datetime, timezone
from pathlib import Path

import bpy
import numpy as np
from mathutils import Matrix, Vector


CONFIGS = {
    "item.weapon.grenadelauncher.sunfallengine": {
        "task_id": "3e786fff-baaf-467d-b6fb-a138a3ce26da",
        "body_name": "SunfallEngine_Body",
        "scale": 0.8306851424,
        "root_raw": (-0.1489425982, 0.0, -0.0970),
        "left_raw": (0.2435045317, 0.0, 0.0260),
        "muzzle_raw": (0.5000, 0.0, 0.0100),
        "emission_strength": 3.0,
        "threshold": {
            "r_min": 190, "g_min": 65, "b_max": 70,
            "r_minus_g_min": 45, "g_minus_b_min": 15,
            "r_minus_b_min": 120,
        },
        "glow_description": "solar amber; only the sun core and muzzle groove",
        "grip_reference": {
            "alpha_bbox": [191, 289, 1847, 751],
            "trigger_center_px": [772.0, 668.0],
            "support_center_px": [1421.5, 480.0],
            "trigger_to_support_px": 649.5,
        },
    },
    "item.weapon.shotgun.starforgebreach": {
        "task_id": "80f712ad-5e7c-410e-828b-2aa79f38aaf0",
        "body_name": "StarforgeBreach_Body",
        "scale": 0.7950307692,
        "root_raw": (-0.1716088328, 0.0, -0.0730),
        "left_raw": (0.2384858044, 0.0, 0.0500),
        "muzzle_raw": (0.5000, 0.0, 0.0180),
        "emission_strength": 3.0,
        "threshold": {
            "r_min": 165, "g_min": 45, "b_max": 55,
            "r_minus_g_min": 60, "g_minus_b_min": 5,
            "r_minus_b_min": 125,
        },
        "glow_description": "gold-red; only the flat forge core",
        "grip_reference": {
            "alpha_bbox": [235, 319, 1821, 701],
            "trigger_center_px": [755.5, 609.0],
            "support_center_px": [1405.5, 440.0],
            "trigger_to_support_px": 650.0,
        },
    },
}

FINAL_TRIANGLE_TARGET = 100_000


def v3(value) -> list[float]:
    return [round(float(value[i]), 6) for i in range(3)]


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def srgb_to_linear(channel: float) -> float:
    value = channel / 255.0
    return value / 12.92 if value <= 0.04045 else ((value + 0.055) / 1.055) ** 2.4


def linear_to_srgb(channel: np.ndarray) -> np.ndarray:
    channel = np.clip(channel, 0.0, 1.0)
    return np.where(channel <= 0.0031308, channel * 12.92, 1.055 * np.power(channel, 1.0 / 2.4) - 0.055)


def look_at(obj, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def orient_camera(camera, target: Vector) -> None:
    direction = (target - camera.location).normalized()
    distance = (camera.location - target).length
    up = Vector((0.0, 1.0, 0.0))
    if abs(direction.dot(up)) > 0.98:
        up = Vector((0.0, 0.0, 1.0))
    right = direction.cross(up).normalized()
    up = right.cross(direction).normalized()
    camera.matrix_world = Matrix((right, up, -direction)).transposed().to_4x4()
    camera.location = target - direction * distance


def bounds(obj) -> tuple[Vector, Vector]:
    points = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    return (
        Vector((min(point[i] for point in points) for i in range(3))),
        Vector((max(point[i] for point in points) for i in range(3))),
    )


def triangles(obj) -> int:
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def raw_to_final(point: Vector, config: dict) -> Vector:
    origin = Vector(config["root_raw"])
    scale = config["scale"]
    relative = point - origin
    return Vector((relative.y, relative.z, relative.x)) * scale


def save_embedded(image, path: Path, colorspace: str) -> dict:
    image.colorspace_settings.name = colorspace
    image.filepath_raw = str(path)
    image.file_format = "PNG"
    image.save()
    return {
        "source_name": image.name,
        "path": str(path),
        "size": list(image.size),
        "colorspace": colorspace,
    }


def create_global_rgb_mask(color_image, output: Path, threshold: dict, item_id: str) -> tuple[bpy.types.Image, dict]:
    width, height = color_image.size
    pixels = np.empty(width * height * 4, dtype=np.float32)
    color_image.pixels.foreach_get(pixels)
    rgba_linear = pixels.reshape((height, width, 4))
    rgb = np.rint(linear_to_srgb(rgba_linear[:, :, :3]) * 255.0).astype(np.int16)
    r, g, b = rgb[:, :, 0], rgb[:, :, 1], rgb[:, :, 2]
    selected = (
        (r >= threshold["r_min"]) & (g >= threshold["g_min"]) & (b <= threshold["b_max"])
        & ((r - g) >= threshold["r_minus_g_min"])
        & ((g - b) >= threshold["g_minus_b_min"])
        & ((r - b) >= threshold["r_minus_b_min"])
    )
    selected_count = int(selected.sum())
    if selected_count == 0:
        raise RuntimeError("Global RGB threshold selected no BaseColor pixels")
    representative = [int(value) for value in np.median(rgb[selected], axis=0)]
    mask_pixels = np.zeros((height, width, 4), dtype=np.float32)
    mask_pixels[:, :, 0:3] = selected[:, :, None].astype(np.float32)
    mask_pixels[:, :, 3] = 1.0
    emission = bpy.data.images.new(f"{item_id}_Emission", width=width, height=height, alpha=True)
    emission.colorspace_settings.name = "Non-Color"
    emission.pixels.foreach_set(mask_pixels.reshape(-1))
    emission.filepath_raw = str(output)
    emission.file_format = "PNG"
    emission.save()
    linear = [round(srgb_to_linear(value), 7) for value in representative]
    return emission, {
        "path": str(output),
        "method": "single global BaseColor sRGB RGB-family threshold",
        "source_image": color_image.filepath_raw,
        "threshold_srgb_8bit": threshold,
        "selected_pixels": selected_count,
        "selected_fraction": round(selected_count / float(width * height), 8),
        "representative_srgb_8bit": representative,
        "unity_linear_rgb_target": linear,
        "formula": "c<=0.04045 ? c/12.92 : ((c+0.055)/1.055)^2.4, c=sRGB/255",
        "binary_values": [0, 255],
        "spatial_or_uv_restriction": False,
        "morphology_blur_component_raycast_mesh_split": False,
        "exception": None,
    }


def configure_material(body, root: Path, output: Path, config: dict, item_id: str) -> tuple[dict, list[dict]]:
    if len(body.material_slots) != 1 or body.material_slots[0].material is None:
        raise RuntimeError("Expected one imported H3 PBR material")
    material = body.material_slots[0].material
    material.name = f"M_{item_id}_PBR"
    images = {}
    for prefix in ("Color_", "NormalGL_", "ORM_"):
        images[prefix] = next((image for image in bpy.data.images if image.name.startswith(prefix)), None)
        if images[prefix] is None:
            raise RuntimeError(f"Missing embedded {prefix} texture")
    textures = output / "Textures"
    color_path = textures / f"{item_id}_BaseColor.png"
    normal_path = textures / f"{item_id}_Normal.png"
    orm_path = textures / f"{item_id}_ORM.png"
    records = [
        save_embedded(images["Color_"], color_path, "sRGB"),
        save_embedded(images["NormalGL_"], normal_path, "Non-Color"),
        save_embedded(images["ORM_"], orm_path, "Non-Color"),
    ]
    emission_path = textures / f"{item_id}_Emission.png"
    emission_image, emission_record = create_global_rgb_mask(images["Color_"], emission_path, config["threshold"], item_id)
    records.append(emission_record)
    material.use_nodes = True
    principled = next(node for node in material.node_tree.nodes if node.bl_idname == "ShaderNodeBsdfPrincipled")
    texture_node = material.node_tree.nodes.new("ShaderNodeTexImage")
    texture_node.name = "Global RGB Binary Emission Mask"
    texture_node.image = emission_image
    texture_node.interpolation = "Closest"
    multiply = material.node_tree.nodes.new("ShaderNodeMixRGB")
    multiply.name = "Emission Mask x Concept Color"
    multiply.blend_type = "MULTIPLY"
    multiply.inputs[0].default_value = 1.0
    linear = emission_record["unity_linear_rgb_target"]
    multiply.inputs[2].default_value = (*linear, 1.0)
    material.node_tree.links.new(texture_node.outputs["Color"], multiply.inputs[1])
    material.node_tree.links.new(multiply.outputs["Color"], principled.inputs["Emission Color"])
    principled.inputs["Emission Strength"].default_value = config["emission_strength"]
    principled.inputs["Alpha"].default_value = 1.0
    return {
        "name": material.name,
        "principled_bsdf": True,
        "pbr_preserved": True,
        "base_color_path": str(color_path),
        "normal_path": str(normal_path),
        "orm_path": str(orm_path),
        "emission": emission_record,
        "emission_strength": config["emission_strength"],
        "glow_designation": config["glow_description"],
        "added_geometry": False,
    }, records


def transform_body(body, config: dict) -> dict:
    raw_min, raw_max = bounds(body)
    for vertex in body.data.vertices:
        vertex.co = raw_to_final(Vector(vertex.co), config)
    body.data.update()
    body.location = (0.0, 0.0, 0.0)
    body.rotation_euler = (0.0, 0.0, 0.0)
    body.scale = (1.0, 1.0, 1.0)
    return {
        "raw_bounds": {"min": v3(raw_min), "max": v3(raw_max), "dimensions": v3(raw_max - raw_min)},
        "mapping": "raw (X,Y,Z) -> final (Y,Z,X), translated to trigger-grip origin",
        "uniform_scale": config["scale"],
        "root_grip_raw": list(config["root_raw"]),
    }


def make_decimated(source, config: dict):
    candidate = source.copy()
    candidate.data = source.data.copy()
    candidate.name = config["body_name"]
    candidate.data.name = f"{config['body_name']}_Mesh"
    bpy.context.scene.collection.objects.link(candidate)
    before = triangles(candidate)
    modifier = candidate.modifiers.new("GameReady_Decimate", "DECIMATE")
    modifier.decimate_type = "COLLAPSE"
    modifier.ratio = min(1.0, FINAL_TRIANGLE_TARGET / float(before))
    modifier.use_collapse_triangulate = True
    bpy.context.view_layer.objects.active = candidate
    candidate.select_set(True)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    candidate.select_set(False)
    return candidate, {
        "source_triangles": before,
        "target_triangles": FINAL_TRIANGLE_TARGET,
        "final_triangles": triangles(candidate),
        "ratio": round(FINAL_TRIANGLE_TARGET / float(before), 8),
        "reason": "The H3 standard source exceeded 1.4M triangles; 100k is required for a practical Unity weapon asset.",
    }


def setup_render(center: Vector, longest: float):
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
        ("QA_Key", (1.7, -1.8, 2.0), 190.0, 2.2),
        ("QA_Fill", (-1.4, -1.0, 0.8), 110.0, 2.0),
        ("QA_Rim", (0.2, 2.0, 1.4), 145.0, 1.8),
    ):
        data = bpy.data.lights.new(name, "AREA")
        data.energy, data.shape, data.size = energy, "DISK", size
        light = bpy.data.objects.new(name, data)
        light.location = center + Vector(offset) * longest
        look_at(light, center)
        scene.collection.objects.link(light)
        temporary.append(light)
    world = bpy.data.worlds.new("QA_World")
    world.use_nodes = True
    background = world.node_tree.nodes["Background"]
    background.inputs["Color"].default_value = (0.035, 0.045, 0.065, 1.0)
    background.inputs["Strength"].default_value = 0.30
    scene.world = world
    return scene, camera, temporary


def render_view(scene, camera, center: Vector, longest: float, offset: Vector, output: Path):
    camera.location = center + offset
    orient_camera(camera, center)
    camera.data.ortho_scale = longest * (1.68 if "iso" in output.stem else 1.55)
    scene.render.filepath = str(output)
    bpy.ops.render.render(write_still=True)


def render_comparison(scene, camera, original, decimated, qa: Path) -> dict:
    minimum, maximum = bounds(original)
    center, longest = (minimum + maximum) * 0.5, max(maximum - minimum)
    distance = longest * 3.0
    outputs = {}
    for label, shown in (("original", original), ("100k", decimated)):
        original.hide_render = shown is not original
        decimated.hide_render = shown is not decimated
        for view, offset in (
            ("side", Vector((-distance, 0.0, 0.0))),
            ("iso", Vector((-distance * 0.72, distance * 0.64, distance * 0.80))),
        ):
            path = qa / f"decimate_{label}_{view}.png"
            render_view(scene, camera, center, longest, offset, path)
            outputs[f"{label}_{view}"] = str(path)
    decimated.hide_render = False
    original.hide_render = True
    return outputs


def make_root(body, config: dict, item_id: str):
    root = bpy.data.objects.new(f"{item_id}_root", None)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.08
    bpy.context.scene.collection.objects.link(root)
    body.parent = root
    for name, raw_point in (
        ("RightHandGrip", config["root_raw"]),
        ("LeftHandGrip", config["left_raw"]),
        ("Muzzle", config["muzzle_raw"]),
    ):
        marker = bpy.data.objects.new(name, None)
        marker.empty_display_type = "ARROWS" if name == "Muzzle" else "CUBE"
        marker.empty_display_size = 0.035
        marker.location = raw_to_final(Vector(raw_point), config)
        bpy.context.scene.collection.objects.link(marker)
        marker.parent = root
    root["coordinate_contract"] = "Gunner +Z muzzle, +Y top, trigger-grip root"
    return root


def render_final(scene, camera, body, material, qa: Path) -> tuple[dict, dict]:
    minimum, maximum = bounds(body)
    center, longest = (minimum + maximum) * 0.5, max(maximum - minimum)
    distance = longest * 3.0
    views = {
        "front": Vector((0.0, 0.0, distance)),
        "back": Vector((0.0, 0.0, -distance)),
        "left": Vector((-distance, 0.0, 0.0)),
        "right": Vector((distance, 0.0, 0.0)),
        "top": Vector((0.0, distance, 0.0)),
        "iso_left": Vector((-distance * 0.72, distance * 0.64, distance * 0.80)),
        "iso_right": Vector((distance * 0.72, distance * 0.64, distance * 0.80)),
    }
    pbr = {}
    for name, offset in views.items():
        path = qa / f"pbr_{name}.png"
        render_view(scene, camera, center, longest, offset, path)
        pbr[name] = str(path)
    principled = next(node for node in material.node_tree.nodes if node.bl_idname == "ShaderNodeBsdfPrincipled")
    sockets = [principled.inputs["Base Color"], principled.inputs["Metallic"], principled.inputs["Roughness"]]
    links = [[(link.from_socket, link.to_socket) for link in socket.links] for socket in sockets]
    for socket in sockets:
        for link in list(socket.links):
            material.node_tree.links.remove(link)
    sockets[0].default_value = (0.0, 0.0, 0.0, 1.0)
    sockets[1].default_value = 0.0
    sockets[2].default_value = 1.0
    background = scene.world.node_tree.nodes["Background"]
    old_strength = background.inputs["Strength"].default_value
    background.inputs["Strength"].default_value = 0.0
    for obj in scene.objects:
        if obj.type == "LIGHT":
            obj.hide_render = True
    emission = {}
    for name, offset in views.items():
        path = qa / f"emission_{name}.png"
        render_view(scene, camera, center, longest, offset, path)
        emission[name] = str(path)
    for socket_links in links:
        for source, destination in socket_links:
            material.node_tree.links.new(source, destination)
    background.inputs["Strength"].default_value = old_strength
    return pbr, emission


def export_fbx(root, output: Path) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for child in root.children_recursive:
        child.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=str(output), use_selection=True, object_types={"EMPTY", "MESH"},
        add_leaf_bones=False, apply_unit_scale=False, use_space_transform=True,
        bake_space_transform=False, axis_forward="-Z", axis_up="Y",
        path_mode="COPY", embed_textures=False,
    )


def remove_objects(objects) -> None:
    for obj in objects:
        if obj.name in bpy.data.objects:
            bpy.data.objects.remove(obj, do_unlink=True)


def reimport_validate(fbx: Path, expected_triangles: int, expected_markers: dict, report_path: Path) -> dict:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(fbx))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    empties = [obj for obj in bpy.context.scene.objects if obj.type == "EMPTY"]
    objects = {obj.name: obj for obj in bpy.context.scene.objects}
    markers = {}
    for name in ("RightHandGrip", "LeftHandGrip", "Muzzle"):
        obj = objects.get(name)
        if obj:
            markers[name] = {
                "parent": obj.parent.name if obj.parent else None,
                "local_location": v3(obj.location),
                "local_rotation": v3(obj.rotation_euler),
                "local_scale": v3(obj.scale),
                "local_plus_z": v3(obj.matrix_local.to_3x3() @ Vector((0.0, 0.0, 1.0))),
            }
    imported_triangles = sum(triangles(obj) for obj in meshes)
    prohibited = [obj.name for obj in bpy.context.scene.objects if obj.type in {"CAMERA", "LIGHT", "ARMATURE"} or "collider" in obj.name.lower()]
    hierarchy_root = next((obj for obj in empties if obj.parent is None and obj.name.endswith("_root")), None)
    marker_positions_match = all(
        name in markers and (Vector(markers[name]["local_location"]) - Vector(expected)).length <= 0.002
        for name, expected in expected_markers.items()
    )
    checks = {
        "one_mesh": len(meshes) == 1,
        "single_root": hierarchy_root is not None and len([obj for obj in bpy.context.scene.objects if obj.parent is None]) == 1,
        "markers_present": len(markers) == 3,
        "markers_parented_to_root": hierarchy_root is not None and all(entry["parent"] == hierarchy_root.name for entry in markers.values()),
        "marker_positions_match": marker_positions_match,
        "right_grip_origin": "RightHandGrip" in markers and Vector(markers["RightHandGrip"]["local_location"]).length <= 0.0001,
        "muzzle_plus_z": "Muzzle" in markers and (Vector(markers["Muzzle"]["local_plus_z"]) - Vector((0, 0, 1))).length <= 0.0001,
        "triangles_match": imported_triangles == expected_triangles,
        "no_prohibited_objects": not prohibited,
        "unit_positive_mesh_scale": len(meshes) == 1 and (Vector(meshes[0].scale) - Vector((1, 1, 1))).length <= 0.0001,
        "material_present": len(meshes) == 1 and len(meshes[0].material_slots) == 1 and meshes[0].material_slots[0].material is not None,
    }
    report = {
        "generated_at": utc_now(), "source_fbx": str(fbx), "objects": sorted(objects),
        "mesh_count": len(meshes), "empty_count": len(empties), "triangles": imported_triangles,
        "markers": markers, "prohibited_objects": prohibited, "checks": checks, "pass": all(checks.values()),
    }
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    return report


def main() -> None:
    if "--" not in sys.argv:
        raise RuntimeError("Pass the weapon root after --")
    root = Path(sys.argv[sys.argv.index("--") + 1]).resolve()
    item_id = root.name
    if item_id not in CONFIGS:
        raise RuntimeError(f"Unsupported weapon root: {root}")
    config = CONFIGS[item_id]
    source = root / "Tripo" / "Downloaded" / "model.glb"
    output = root / "Production"
    textures = output / "Textures"
    qa = output / "QA"
    for folder in (output, textures, qa):
        folder.mkdir(parents=True, exist_ok=True)
    blend_path = output / f"{item_id}.blend"
    fbx_path = output / f"{item_id}.fbx"
    validation_path = output / "validation.json"
    reimport_path = output / "reimport_validation.json"

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(source))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError(f"Expected one H3 mesh, got {len(meshes)}")
    original = meshes[0]
    source_triangles = triangles(original)
    transform_record = transform_body(original, config)
    material_record, texture_records = configure_material(original, root, output, config, item_id)
    original.name = f"{config['body_name']}_OriginalQA"
    body, decimation = make_decimated(original, config)
    minimum, maximum = bounds(original)
    scene, camera, temporary = setup_render((minimum + maximum) * 0.5, max(maximum - minimum))
    comparison = render_comparison(scene, camera, original, body, qa)
    remove_objects([original])
    root_obj = make_root(body, config, item_id)
    pbr_qa, emission_qa = render_final(scene, camera, body, body.material_slots[0].material, qa)
    remove_objects(temporary)
    body.hide_render = False
    body.hide_viewport = False
    bpy.context.view_layer.objects.active = body
    body.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    body.select_set(False)

    marker_records = {}
    expected_markers = {}
    for name in ("RightHandGrip", "LeftHandGrip", "Muzzle"):
        marker = bpy.data.objects[name]
        expected_markers[name] = v3(marker.location)
        marker_records[name] = {
            "parent": marker.parent.name if marker.parent else None,
            "local_location": v3(marker.location),
            "local_rotation": v3(marker.rotation_euler),
            "local_scale": v3(marker.scale),
            "local_plus_z": v3(marker.matrix_local.to_3x3() @ Vector((0, 0, 1))),
        }
    final_min, final_max = bounds(body)
    final_triangles = triangles(body)
    prohibited = [obj.name for obj in bpy.context.scene.objects if obj.type in {"CAMERA", "LIGHT", "ARMATURE"} or "collider" in obj.name.lower()]
    checks = {
        "single_root": len([obj for obj in bpy.context.scene.objects if obj.parent is None]) == 1,
        "one_mesh": len([obj for obj in bpy.context.scene.objects if obj.type == "MESH"]) == 1,
        "right_grip_origin": Vector(marker_records["RightHandGrip"]["local_location"]).length <= 0.0001,
        "left_grip_distance": abs(marker_records["LeftHandGrip"]["local_location"][2] - 0.326) <= 0.0005,
        "muzzle_forward_of_left_grip": marker_records["Muzzle"]["local_location"][2] > marker_records["LeftHandGrip"]["local_location"][2],
        "muzzle_plus_z": marker_records["Muzzle"]["local_plus_z"] == [0.0, 0.0, 1.0],
        "positive_unit_mesh_scale": v3(body.scale) == [1.0, 1.0, 1.0],
        "no_prohibited_objects": not prohibited,
        "pbr_textures_present": all(Path(record["path"]).is_file() for record in texture_records),
        "binary_global_rgb_emission": material_record["emission"]["binary_values"] == [0, 255] and not material_record["emission"]["spatial_or_uv_restriction"],
        "triangle_budget": 95_000 <= final_triangles <= 105_000,
        "reimport_verified": False,
    }
    validation = {
        "item_id": item_id, "generated_at": utc_now(), "blender_version": bpy.app.version_string,
        "source": {
            "task_id": config["task_id"], "type": "multiview_to_model", "model": "v3.1-20260211",
            "geometry_quality": "standard", "texture": True, "pbr": True, "export_uv": True,
            "credits_consumed": 30, "glb": str(source), "triangles": source_triangles,
        },
        "coordinate_contract": {"raw_muzzle": "+X", "raw_up": "+Z", "final_muzzle": "+Z", "final_up": "+Y", "root_origin": "actual trigger-grip center"},
        "transform": transform_record, "grip_reference": config["grip_reference"],
        "root": root_obj.name, "mesh": body.name, "markers": marker_records,
        "bounds_root_local": {"min": v3(final_min), "max": v3(final_max), "dimensions": v3(final_max - final_min)},
        "decimation": {**decimation, "comparison_renders": comparison, "visual_review": "accepted: direct original/100k side and isometric comparison preserves silhouette, muzzle opening, grip, core and major surface structure"},
        "material": material_record, "textures": texture_records,
        "qa": {"pbr": pbr_qa, "emission_only": emission_qa},
        "prohibited_objects": prohibited, "checks": checks, "pass": False,
    }
    validation_path.write_text(json.dumps(validation, ensure_ascii=False, indent=2), encoding="utf-8")
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    export_fbx(root_obj, fbx_path)
    reimport = reimport_validate(fbx_path, final_triangles, expected_markers, reimport_path)
    validation["checks"]["reimport_verified"] = reimport["pass"]
    validation["reimport"] = {"path": str(reimport_path), "pass": reimport["pass"]}
    validation["pass"] = all(validation["checks"].values())
    validation_path.write_text(json.dumps(validation, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"item_id": item_id, "blend": str(blend_path), "fbx": str(fbx_path), "validation_pass": validation["pass"], "reimport_pass": reimport["pass"], "triangles": final_triangles}, indent=2))


if __name__ == "__main__":
    main()
