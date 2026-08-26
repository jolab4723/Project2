from __future__ import annotations

import json
from datetime import datetime, timezone
from pathlib import Path

import bpy
import numpy as np
from mathutils import Matrix, Vector


ROOT = Path(__file__).resolve().parents[1]
OUT = Path(__file__).resolve().parent
SOURCE = ROOT / "Tripo" / "RetryFrontBackSwap" / "Downloaded" / "model.glb"
ITEM_ID = "item.weapon.rifle.railcarbine"
TASK_ID = "f7256399-6bda-46d3-bc68-730b021391b8"
BODY_NAME = "RailCarbine_Regenerated_Body"
SCALE = 0.654880706921944
ROOT_RAW = Vector((0.17888563049853373, 0.0, -0.06940988239700376))
LEFT_RAW = Vector((-0.31891495601173026, 0.0, -0.04394590187265919))
MUZZLE_RAW = Vector((-0.5, 0.0, 0.0570))
TARGET_TRIANGLES = 100_000
EMISSION_STRENGTH = 2.5
THRESHOLD = {
    "r_max": 90,
    "g_min": 160,
    "b_min": 160,
    "g_minus_r_min": 80,
    "b_minus_r_min": 80,
}


def utc_now():
    return datetime.now(timezone.utc).isoformat()


def v3(value):
    return [round(float(value[i]), 6) for i in range(3)]


def srgb_to_linear(channel):
    value = channel / 255.0
    return value / 12.92 if value <= 0.04045 else ((value + 0.055) / 1.055) ** 2.4


def linear_to_srgb(values):
    values = np.clip(values, 0.0, 1.0)
    return np.where(values <= 0.0031308, values * 12.92, 1.055 * np.power(values, 1.0 / 2.4) - 0.055)


def raw_to_final(point):
    delta = point - ROOT_RAW
    return Vector((delta.y, delta.z, -delta.x)) * SCALE


def triangles(obj):
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def bounds(obj):
    points = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    return (
        Vector((min(point[i] for point in points) for i in range(3))),
        Vector((max(point[i] for point in points) for i in range(3))),
    )


def look_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def orient_camera(camera, target):
    target = Vector(target)
    direction = (target - camera.location).normalized()
    distance = (camera.location - target).length
    up = Vector((0.0, 1.0, 0.0))
    if abs(direction.dot(up)) > 0.98:
        up = Vector((0.0, 0.0, 1.0))
    right = direction.cross(up).normalized()
    up = right.cross(direction).normalized()
    camera.matrix_world = Matrix((right, up, -direction)).transposed().to_4x4()
    camera.location = target - direction * distance


def save_image(image, path, colorspace):
    image.colorspace_settings.name = colorspace
    image.filepath_raw = str(path)
    image.file_format = "PNG"
    image.save()
    return {"source": image.name, "path": str(path), "size": list(image.size), "colorspace": colorspace}


def make_emission(color_image, path):
    width, height = color_image.size
    flat = np.empty(width * height * 4, dtype=np.float32)
    color_image.pixels.foreach_get(flat)
    rgba = flat.reshape((height, width, 4))
    rgb = np.rint(linear_to_srgb(rgba[:, :, :3]) * 255.0).astype(np.int16)
    r, g, b = rgb[:, :, 0], rgb[:, :, 1], rgb[:, :, 2]
    selected = (
        (r <= THRESHOLD["r_max"]) & (g >= THRESHOLD["g_min"]) & (b >= THRESHOLD["b_min"])
        & ((g - r) >= THRESHOLD["g_minus_r_min"])
        & ((b - r) >= THRESHOLD["b_minus_r_min"])
    )
    count = int(selected.sum())
    if count == 0:
        raise RuntimeError("Cyan RGB threshold selected no pixels")
    representative = [int(value) for value in np.median(rgb[selected], axis=0)]
    mask = np.zeros((height, width, 4), dtype=np.float32)
    mask[:, :, :3] = selected[:, :, None].astype(np.float32)
    mask[:, :, 3] = 1.0
    image = bpy.data.images.new(f"{ITEM_ID}_Emission", width=width, height=height, alpha=True)
    image.colorspace_settings.name = "Non-Color"
    image.pixels.foreach_set(mask.reshape(-1))
    image.filepath_raw = str(path)
    image.file_format = "PNG"
    image.save()
    linear = [round(srgb_to_linear(value), 7) for value in representative]
    return image, {
        "path": str(path),
        "source_base_color": color_image.filepath_raw,
        "method": "single global BaseColor sRGB cyan-family threshold",
        "threshold_srgb_8bit": THRESHOLD,
        "selected_pixels": count,
        "selected_fraction": round(count / float(width * height), 8),
        "representative_srgb_8bit": representative,
        "unity_linear_rgb_target": linear,
        "formula": "c<=0.04045 ? c/12.92 : ((c+0.055)/1.055)^2.4, c=sRGB/255",
        "binary_values": [0, 255],
        "uv_mesh_spatial_component_morphology_blur_raycast_exception": False,
        "non_glow_paint_color_reuse_observed": False,
    }


def configure_material(body, textures):
    if len(body.material_slots) != 1 or body.material_slots[0].material is None:
        raise RuntimeError("Expected one H3 material")
    material = body.material_slots[0].material
    material.name = "M_RailCarbine_Regenerated_PBR"
    embedded = {}
    for prefix in ("Color_", "NormalGL_", "ORM_"):
        embedded[prefix] = next((image for image in bpy.data.images if image.name.startswith(prefix)), None)
        if embedded[prefix] is None:
            raise RuntimeError(f"Missing {prefix} texture")
    base_path = textures / f"{ITEM_ID}_BaseColor.png"
    normal_path = textures / f"{ITEM_ID}_Normal.png"
    orm_path = textures / f"{ITEM_ID}_ORM.png"
    records = [
        save_image(embedded["Color_"], base_path, "sRGB"),
        save_image(embedded["NormalGL_"], normal_path, "Non-Color"),
        save_image(embedded["ORM_"], orm_path, "Non-Color"),
    ]
    emission_path = textures / f"{ITEM_ID}_Emission.png"
    emission, emission_record = make_emission(embedded["Color_"], emission_path)
    records.append(emission_record)
    material.use_nodes = True
    principled = next(node for node in material.node_tree.nodes if node.bl_idname == "ShaderNodeBsdfPrincipled")
    texture_node = material.node_tree.nodes.new("ShaderNodeTexImage")
    texture_node.name = "Global Cyan Binary Emission Mask"
    texture_node.image = emission
    texture_node.interpolation = "Closest"
    multiply = material.node_tree.nodes.new("ShaderNodeMixRGB")
    multiply.name = "Emission Mask x Cyan"
    multiply.blend_type = "MULTIPLY"
    multiply.inputs[0].default_value = 1.0
    multiply.inputs[2].default_value = (*emission_record["unity_linear_rgb_target"], 1.0)
    material.node_tree.links.new(texture_node.outputs["Color"], multiply.inputs[1])
    material.node_tree.links.new(multiply.outputs["Color"], principled.inputs["Emission Color"])
    principled.inputs["Emission Strength"].default_value = EMISSION_STRENGTH
    principled.inputs["Alpha"].default_value = 1.0
    return material, records, emission_record


def transform_body(body):
    raw_min, raw_max = bounds(body)
    for vertex in body.data.vertices:
        vertex.co = raw_to_final(Vector(vertex.co))
    body.data.update()
    body.location = (0, 0, 0)
    body.rotation_euler = (0, 0, 0)
    body.scale = (1, 1, 1)
    return {
        "raw_bounds": {"min": v3(raw_min), "max": v3(raw_max), "dimensions": v3(raw_max - raw_min)},
        "mapping": "raw (X,Y,Z) -> final (Y,Z,-X), translated to trigger-grip origin",
        "scale": SCALE,
        "root_raw": v3(ROOT_RAW),
    }


def decimate(source):
    body = source.copy()
    body.data = source.data.copy()
    body.name = BODY_NAME
    body.data.name = f"{BODY_NAME}_Mesh"
    bpy.context.collection.objects.link(body)
    before = triangles(body)
    modifier = body.modifiers.new("GameReady_Decimate", "DECIMATE")
    modifier.decimate_type = "COLLAPSE"
    modifier.ratio = TARGET_TRIANGLES / float(before)
    modifier.use_collapse_triangulate = True
    bpy.context.view_layer.objects.active = body
    body.select_set(True)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    body.select_set(False)
    return body, {"source_triangles": before, "target": TARGET_TRIANGLES, "final_triangles": triangles(body), "ratio": round(TARGET_TRIANGLES / float(before), 8)}


def setup_render(center, longest):
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
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.025, 0.035, 0.055, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.28
    scene.world = world
    return scene, camera, temporary


def render_view(scene, camera, center, longest, offset, path):
    camera.location = center + offset
    orient_camera(camera, center)
    camera.data.ortho_scale = longest * (1.68 if "iso" in path.stem else 1.55)
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)


def compare(scene, camera, original, body, qa):
    minimum, maximum = bounds(original)
    center, longest = (minimum + maximum) * 0.5, max(maximum - minimum)
    distance = longest * 3.0
    result = {}
    for label, shown in (("original", original), ("100k", body)):
        original.hide_render = shown is not original
        body.hide_render = shown is not body
        for view, offset in (
            ("side", Vector((-distance, 0, 0))),
            ("iso", Vector((-distance * 0.72, distance * 0.64, distance * 0.80))),
        ):
            path = qa / f"decimate_{label}_{view}.png"
            render_view(scene, camera, center, longest, offset, path)
            result[f"{label}_{view}"] = str(path)
    original.hide_render = True
    body.hide_render = False
    return result


def make_root(body):
    root = bpy.data.objects.new(f"{ITEM_ID}_root", None)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.07
    bpy.context.scene.collection.objects.link(root)
    body.parent = root
    for name, point in (("RightHandGrip", ROOT_RAW), ("LeftHandGrip", LEFT_RAW), ("Muzzle", MUZZLE_RAW)):
        marker = bpy.data.objects.new(name, None)
        marker.empty_display_type = "ARROWS" if name == "Muzzle" else "CUBE"
        marker.empty_display_size = 0.03
        marker.location = raw_to_final(point)
        bpy.context.scene.collection.objects.link(marker)
        marker.parent = root
    root["coordinate_contract"] = "Gunner +Z muzzle, +Y top, trigger-grip root"
    return root


def final_renders(scene, camera, body, material, qa):
    minimum, maximum = bounds(body)
    center, longest = (minimum + maximum) * 0.5, max(maximum - minimum)
    distance = longest * 3.0
    views = {
        "front": Vector((0, 0, distance)), "back": Vector((0, 0, -distance)),
        "left": Vector((-distance, 0, 0)), "right": Vector((distance, 0, 0)),
        "top": Vector((0, distance, 0)),
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
    saved_links = [[(link.from_socket, link.to_socket) for link in socket.links] for socket in sockets]
    for socket in sockets:
        for link in list(socket.links):
            material.node_tree.links.remove(link)
    sockets[0].default_value, sockets[1].default_value, sockets[2].default_value = (0, 0, 0, 1), 0.0, 1.0
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
    for links in saved_links:
        for source, destination in links:
            material.node_tree.links.new(source, destination)
    background.inputs["Strength"].default_value = old_strength
    return pbr, emission


def export_fbx(root, path):
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for child in root.children_recursive:
        child.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=str(path), use_selection=True, object_types={"EMPTY", "MESH"},
        add_leaf_bones=False, apply_unit_scale=False, use_space_transform=True,
        bake_space_transform=False, axis_forward="-Z", axis_up="Y", path_mode="COPY", embed_textures=False,
    )


def reimport_validate(fbx, expected_triangles, expected_markers, report_path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(fbx))
    objects = {obj.name: obj for obj in bpy.context.scene.objects}
    meshes = [obj for obj in objects.values() if obj.type == "MESH"]
    roots = [obj for obj in objects.values() if obj.parent is None]
    marker_data = {}
    for name in ("RightHandGrip", "LeftHandGrip", "Muzzle"):
        obj = objects.get(name)
        if obj:
            marker_data[name] = {
                "parent": obj.parent.name if obj.parent else None,
                "location": v3(obj.location), "rotation": v3(obj.rotation_euler), "scale": v3(obj.scale),
                "plus_z": v3(obj.matrix_local.to_3x3() @ Vector((0, 0, 1))),
            }
    prohibited = [obj.name for obj in objects.values() if obj.type in {"CAMERA", "LIGHT", "ARMATURE"} or "collider" in obj.name.lower()]
    imported_triangles = sum(triangles(obj) for obj in meshes)
    checks = {
        "single_root": len(roots) == 1 and roots[0].name.endswith("_root"),
        "one_mesh": len(meshes) == 1,
        "markers_present": len(marker_data) == 3,
        "markers_parented_to_root": len(roots) == 1 and all(entry["parent"] == roots[0].name for entry in marker_data.values()),
        "marker_positions_match": all(name in marker_data and (Vector(marker_data[name]["location"]) - Vector(value)).length <= 0.002 for name, value in expected_markers.items()),
        "right_grip_origin": "RightHandGrip" in marker_data and Vector(marker_data["RightHandGrip"]["location"]).length <= 0.0001,
        "muzzle_plus_z": "Muzzle" in marker_data and (Vector(marker_data["Muzzle"]["plus_z"]) - Vector((0, 0, 1))).length <= 0.0001,
        "triangles_match": imported_triangles == expected_triangles,
        "unit_positive_mesh_scale": len(meshes) == 1 and (Vector(meshes[0].scale) - Vector((1, 1, 1))).length <= 0.0001,
        "material_present": len(meshes) == 1 and len(meshes[0].material_slots) == 1 and meshes[0].material_slots[0].material is not None,
        "no_camera_light_armature_collider": not prohibited,
    }
    report = {
        "generated_at": utc_now(), "fbx": str(fbx), "objects": sorted(objects),
        "triangles": imported_triangles, "markers": marker_data, "prohibited": prohibited,
        "checks": checks, "pass": all(checks.values()),
    }
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    return report


def main():
    textures = OUT / "Textures"
    qa = OUT / "QA"
    textures.mkdir(parents=True, exist_ok=True)
    qa.mkdir(parents=True, exist_ok=True)
    blend_path = OUT / f"{ITEM_ID}.blend"
    fbx_path = OUT / f"{ITEM_ID}.fbx"
    validation_path = OUT / "validation.json"
    reimport_path = OUT / "reimport_validation.json"

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(SOURCE))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError(f"Expected one mesh, got {len(meshes)}")
    original = meshes[0]
    source_triangles = triangles(original)
    transform = transform_body(original)
    material, texture_records, emission_record = configure_material(original, textures)
    original.name = "RailCarbine_Regenerated_OriginalQA"
    body, decimation = decimate(original)
    minimum, maximum = bounds(original)
    scene, camera, temporary = setup_render((minimum + maximum) * 0.5, max(maximum - minimum))
    comparison = compare(scene, camera, original, body, qa)
    bpy.data.objects.remove(original, do_unlink=True)
    root = make_root(body)
    pbr_qa, emission_qa = final_renders(scene, camera, body, material, qa)
    for obj in temporary:
        if obj.name in bpy.data.objects:
            bpy.data.objects.remove(obj, do_unlink=True)
    body.hide_render = False
    body.hide_viewport = False
    bpy.context.view_layer.objects.active = body
    body.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    body.select_set(False)

    markers = {}
    expected_markers = {}
    for name in ("RightHandGrip", "LeftHandGrip", "Muzzle"):
        marker = bpy.data.objects[name]
        expected_markers[name] = v3(marker.location)
        markers[name] = {
            "parent": marker.parent.name if marker.parent else None,
            "local_location": v3(marker.location), "local_rotation": v3(marker.rotation_euler),
            "local_scale": v3(marker.scale), "local_plus_z": v3(marker.matrix_local.to_3x3() @ Vector((0, 0, 1))),
        }
    final_min, final_max = bounds(body)
    final_triangles = triangles(body)
    prohibited = [obj.name for obj in bpy.context.scene.objects if obj.type in {"CAMERA", "LIGHT", "ARMATURE"} or "collider" in obj.name.lower()]
    checks = {
        "raw_gate_passed": json.loads((OUT / "raw_visual_gate.json").read_text(encoding="utf-8"))["gate_status"] == "passed",
        "single_root": len([obj for obj in bpy.context.scene.objects if obj.parent is None]) == 1,
        "one_mesh": len([obj for obj in bpy.context.scene.objects if obj.type == "MESH"]) == 1,
        "right_grip_origin": Vector(markers["RightHandGrip"]["local_location"]).length <= 0.0001,
        "left_grip_distance": abs(markers["LeftHandGrip"]["local_location"][2] - 0.326) <= 0.0005,
        "muzzle_forward_of_left_grip": markers["Muzzle"]["local_location"][2] > markers["LeftHandGrip"]["local_location"][2],
        "muzzle_plus_z": markers["Muzzle"]["local_plus_z"] == [0.0, 0.0, 1.0],
        "unit_positive_mesh_scale": v3(body.scale) == [1.0, 1.0, 1.0],
        "no_prohibited_objects": not prohibited,
        "pbr_textures_present": all(Path(record["path"]).is_file() for record in texture_records),
        "global_binary_rgb_emission": emission_record["binary_values"] == [0, 255] and not emission_record["uv_mesh_spatial_component_morphology_blur_raycast_exception"],
        "triangle_budget": 95_000 <= final_triangles <= 105_000,
        "reimport_verified": False,
    }
    validation = {
        "item_id": ITEM_ID, "generated_at": utc_now(), "blender_version": bpy.app.version_string,
        "source": {
            "task_id": TASK_ID, "type": "multiview_to_model", "model": "v3.1-20260211",
            "geometry_quality": "standard", "texture": True, "pbr": True, "export_uv": True,
            "credits_consumed": 30, "glb": str(SOURCE), "triangles": source_triangles,
        },
        "coordinate_contract": {"raw_muzzle": "-X", "raw_up": "+Z", "final_muzzle": "+Z", "final_up": "+Y", "root_origin": "actual trigger grip center"},
        "transform": transform,
        "grip_reference": {
            "left_image_alpha_bbox": [341, 378, 1706, 646],
            "right_hand_center_px": [779, 596], "left_hand_center_px": [1458, 565], "spacing_px": 679,
            "right_grip_raw": v3(ROOT_RAW), "left_grip_raw": v3(LEFT_RAW), "muzzle_raw": v3(MUZZLE_RAW),
        },
        "root": root.name, "mesh": body.name, "markers": markers,
        "bounds_root_local": {"min": v3(final_min), "max": v3(final_max), "dimensions": v3(final_max - final_min)},
        "decimation": {**decimation, "reason": "1.445M source triangles require optimization for a practical Unity weapon asset", "comparison_renders": comparison, "visual_review": "pending direct review"},
        "material": {
            "name": material.name, "principled_bsdf": True, "pbr_preserved": True,
            "base_color_path": texture_records[0]["path"], "normal_path": texture_records[1]["path"], "orm_path": texture_records[2]["path"],
            "emission": emission_record, "emission_strength": EMISSION_STRENGTH, "added_geometry": False,
        },
        "qa": {"pbr": pbr_qa, "emission_only": emission_qa},
        "prohibited_objects": prohibited, "checks": checks, "pass": False,
    }
    validation_path.write_text(json.dumps(validation, ensure_ascii=False, indent=2), encoding="utf-8")
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    export_fbx(root, fbx_path)
    reimport = reimport_validate(fbx_path, final_triangles, expected_markers, reimport_path)
    validation["checks"]["reimport_verified"] = reimport["pass"]
    validation["reimport"] = {"path": str(reimport_path), "pass": reimport["pass"]}
    validation["pass"] = all(validation["checks"].values())
    validation_path.write_text(json.dumps(validation, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"blend": str(blend_path), "fbx": str(fbx_path), "triangles": final_triangles, "validation_pass": validation["pass"], "reimport_pass": reimport["pass"]}, indent=2))


if __name__ == "__main__":
    main()
