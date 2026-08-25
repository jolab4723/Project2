from __future__ import annotations

import json
from datetime import datetime, timezone
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


ROOT = Path(__file__).resolve().parents[1]
RAW_GLB = ROOT / "Tripo" / "RetryAfterExpired20260825" / "Downloaded" / "item.weapon.rifle.smilesignal_raw.glb"
PRODUCTION = ROOT / "Production"
TEXTURES = PRODUCTION / "Textures"
QA = PRODUCTION / "QA"
ITEM_ID = "item.weapon.rifle.smilesignal"
BLEND_PATH = PRODUCTION / f"{ITEM_ID}.blend"
FBX_PATH = PRODUCTION / f"{ITEM_ID}.fbx"
VALIDATION_PATH = PRODUCTION / "validation.json"

BASE_PATH = TEXTURES / f"{ITEM_ID}_BaseColor.png"
NORMAL_PATH = TEXTURES / f"{ITEM_ID}_Normal.png"
ORM_PATH = TEXTURES / f"{ITEM_ID}_ORM.png"
OCCLUSION_PATH = TEXTURES / f"{ITEM_ID}_Occlusion.png"
METALLIC_SMOOTHNESS_PATH = TEXTURES / f"{ITEM_ID}_MetallicSmoothness.png"
EMISSION_PATH = TEXTURES / f"{ITEM_ID}_Emission.png"

MODEL_SCALE = 0.82
TARGET_TRIANGLES = 250_000
RIGHT_HAND_RAW = Vector((0.24, 0.0, -0.105))
LEFT_HAND_LOCAL = Vector((0.0, -0.03, 0.27))
MUZZLE_LOCAL = Vector((0.0, 0.0861, 0.6068))
ORIENTATION = Matrix(
    (
        (0.0, -1.0, 0.0, 0.0),
        (0.0, 0.0, 1.0, 0.0),
        (-1.0, 0.0, 0.0, 0.0),
        (0.0, 0.0, 0.0, 1.0),
    )
)
TRANSFORM = Matrix.Scale(MODEL_SCALE, 4) @ ORIENTATION @ Matrix.Translation(-RIGHT_HAND_RAW)


def now():
    return datetime.now(timezone.utc).isoformat()


def v3(value):
    return [round(float(value[index]), 6) for index in range(3)]


def triangle_count(obj):
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def mesh_bounds(obj):
    points = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
    minimum = Vector(tuple(min(point[index] for point in points) for index in range(3)))
    maximum = Vector(tuple(max(point[index] for point in points) for index in range(3)))
    return minimum, maximum


def stable_orient(camera, target):
    direction = (target - camera.location).normalized()
    world_up = Vector((0.0, 1.0, 0.0))
    if abs(direction.dot(world_up)) > 0.995:
        world_up = Vector((0.0, 0.0, 1.0))
    right = direction.cross(world_up).normalized()
    up = right.cross(direction).normalized()
    camera.rotation_euler = Matrix((right, up, -direction)).transposed().to_euler()


def import_transform_decimate():
    bpy.ops.import_scene.gltf(filepath=str(RAW_GLB))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError(f"Expected one H3 mesh, got {len(meshes)}")
    body = meshes[0]
    body.name = "SmileSignal_Body"
    body.data.name = "SmileSignal_Body_Mesh"
    source_triangles = triangle_count(body)
    body.data.transform(TRANSFORM)
    body.data.update()
    body.location = (0.0, 0.0, 0.0)
    body.rotation_euler = (0.0, 0.0, 0.0)
    body.scale = (1.0, 1.0, 1.0)

    modifier = body.modifiers.new(name="PreservationFirst_250k", type="DECIMATE")
    modifier.decimate_type = "COLLAPSE"
    modifier.ratio = min(1.0, TARGET_TRIANGLES / source_triangles)
    modifier.use_collapse_triangulate = True
    modifier.use_symmetry = True
    modifier.symmetry_axis = "X"
    bpy.context.view_layer.objects.active = body
    body.select_set(True)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    body.select_set(False)
    for polygon in body.data.polygons:
        polygon.use_smooth = True
    body.data.update()
    return body, source_triangles, triangle_count(body)


def load_image(path, name, colorspace):
    image = bpy.data.images.load(str(path), check_existing=False)
    image.name = name
    image.colorspace_settings.name = colorspace
    return image


def configure_material(body):
    base = load_image(BASE_PATH, "SmileSignal_BaseColor", "sRGB")
    normal = load_image(NORMAL_PATH, "SmileSignal_Normal", "Non-Color")
    orm = load_image(ORM_PATH, "SmileSignal_ORM", "Non-Color")
    emission = load_image(EMISSION_PATH, "SmileSignal_Emission", "sRGB")

    material = bpy.data.materials.new("M_SmileSignal_PBR")
    material.use_nodes = True
    material.use_backface_culling = True
    material.diffuse_color = (0.86, 0.84, 0.82, 1.0)
    material.metallic = 0.15
    material.roughness = 0.42
    material["unity_shader"] = "Universal Render Pipeline/Lit"
    material["emission_family"] = "red RGB only"
    material["emission_color_hdr_hint"] = [1.0, 0.03, 0.06, 2.5]
    material["white_surfaces_emissive"] = False
    material["emission_mask_method"] = "global RGB-family binary; no spatial/UV restriction or dilation"

    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    output.location = (760, 0)
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.location = (480, 0)
    bsdf.inputs["Base Color"].default_value = (1.0, 1.0, 1.0, 1.0)
    bsdf.inputs["Metallic"].default_value = 0.1
    bsdf.inputs["Roughness"].default_value = 0.42
    bsdf.inputs["Emission Strength"].default_value = 2.5
    links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])

    base_node = nodes.new("ShaderNodeTexImage")
    base_node.name = "BaseColor"
    base_node.label = "Base Color (sRGB)"
    base_node.image = base
    base_node.location = (-680, 260)
    links.new(base_node.outputs["Color"], bsdf.inputs["Base Color"])

    orm_node = nodes.new("ShaderNodeTexImage")
    orm_node.name = "ORM"
    orm_node.label = "Occlusion(R) Roughness(G) Metallic(B)"
    orm_node.image = orm
    orm_node.location = (-680, -40)
    separate = nodes.new("ShaderNodeSeparateColor")
    separate.mode = "RGB"
    separate.location = (-350, -40)
    links.new(orm_node.outputs["Color"], separate.inputs["Color"])
    links.new(separate.outputs["Green"], bsdf.inputs["Roughness"])
    links.new(separate.outputs["Blue"], bsdf.inputs["Metallic"])

    normal_node = nodes.new("ShaderNodeTexImage")
    normal_node.name = "Normal"
    normal_node.label = "NormalGL (Non-Color)"
    normal_node.image = normal
    normal_node.location = (-680, -340)
    normal_map = nodes.new("ShaderNodeNormalMap")
    normal_map.location = (-340, -320)
    normal_map.inputs["Strength"].default_value = 1.0
    links.new(normal_node.outputs["Color"], normal_map.inputs["Color"])
    links.new(normal_map.outputs["Normal"], bsdf.inputs["Normal"])

    emission_node = nodes.new("ShaderNodeTexImage")
    emission_node.name = "Emission"
    emission_node.label = "Binary Red Emission (sRGB)"
    emission_node.image = emission
    emission_node.location = (-40, -430)
    links.new(emission_node.outputs["Color"], bsdf.inputs["Emission Color"])

    body.data.materials.clear()
    body.data.materials.append(material)
    return material, {"base": base, "normal": normal, "orm": orm, "emission": emission}


def make_root_and_markers(body):
    root = bpy.data.objects.new(ITEM_ID, None)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.06
    root["coordinate_contract"] = "Gunner: +Z muzzle, +Y up, RightHandGrip/root at trigger-grip center"
    root["item_id"] = ITEM_ID
    bpy.context.scene.collection.objects.link(root)
    body.parent = root
    body.matrix_parent_inverse = root.matrix_world.inverted()

    locations = {
        "RightHandGrip": Vector((0.0, 0.0, 0.0)),
        "LeftHandGrip": LEFT_HAND_LOCAL,
        "Muzzle": MUZZLE_LOCAL,
    }
    for name, location in locations.items():
        marker = bpy.data.objects.new(name, None)
        marker.empty_display_type = "ARROWS" if name == "Muzzle" else "CUBE"
        marker.empty_display_size = 0.035
        marker.location = location
        marker.rotation_euler = (0.0, 0.0, 0.0)
        marker["purpose"] = {
            "RightHandGrip": "root/trigger grip center",
            "LeftHandGrip": "clean lower fore-end contact, 27 cm forward",
            "Muzzle": "+Z firing origin at open bore",
        }[name]
        bpy.context.scene.collection.objects.link(marker)
        marker.parent = root
        marker.matrix_parent_inverse = root.matrix_world.inverted()
    return root


def setup_qa(center, longest):
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
        ("QA_Key", (1.8, -1.8, 2.2), 1150.0, 2.5),
        ("QA_Fill", (-1.5, -1.0, 0.9), 650.0, 2.3),
        ("QA_Rim", (0.3, 2.0, 1.5), 850.0, 2.0),
        ("QA_Top", (0.0, 0.2, 2.7), 420.0, 1.7),
    ):
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.size = size * longest
        light = bpy.data.objects.new(name, data)
        scene.collection.objects.link(light)
        light.location = center + Vector(offset) * longest
        stable_orient(light, center)
        temporary.append(light)

    world = bpy.data.worlds.new("SmileSignal_QA_World")
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (0.025, 0.03, 0.045, 1.0)
    background.inputs["Strength"].default_value = 0.25
    scene.world = world
    return scene, camera, temporary


def render_views(scene, camera, center, longest, prefix):
    distance = longest * 3.2
    views = {
        "front": Vector((0.0, 0.0, distance)),
        "back": Vector((0.0, 0.0, -distance)),
        "left": Vector((-distance, 0.0, 0.0)),
        "right": Vector((distance, 0.0, 0.0)),
        "top": Vector((0.0, distance, 0.0)),
        "bottom": Vector((0.0, -distance, 0.0)),
        "iso_left": Vector((-distance, distance * 0.72, distance)),
        "iso_right": Vector((distance, distance * 0.72, distance)),
    }
    records = {}
    for name, offset in views.items():
        camera.location = center + offset
        stable_orient(camera, center)
        camera.data.ortho_scale = longest * (0.82 if name in {"front", "back"} else 1.10)
        output = QA / f"{prefix}_{name}.png"
        scene.render.filepath = str(output)
        bpy.ops.render.render(write_still=True)
        records[name] = str(output.relative_to(ROOT))
    return records


def render_marker_overlay(scene, camera, center, longest, root):
    colors = {
        "RightHandGrip": (1.0, 0.04, 0.02, 1.0),
        "LeftHandGrip": (0.05, 1.0, 0.12, 1.0),
        "Muzzle": (0.02, 0.75, 1.0, 1.0),
    }
    overlays = []
    for name, color in colors.items():
        bpy.ops.mesh.primitive_uv_sphere_add(
            segments=24,
            ring_count=12,
            radius=0.018,
            location=bpy.data.objects[name].matrix_world.translation,
        )
        sphere = bpy.context.object
        sphere.name = f"QA_{name}"
        material = bpy.data.materials.new(f"QA_{name}_Material")
        material.diffuse_color = color
        material.use_nodes = True
        bsdf = material.node_tree.nodes.get("Principled BSDF")
        bsdf.inputs["Base Color"].default_value = color
        bsdf.inputs["Emission Color"].default_value = color
        bsdf.inputs["Emission Strength"].default_value = 3.0
        sphere.data.materials.append(material)
        overlays.append(sphere)
    camera.location = center + Vector((-longest * 3.2, 0.0, 0.0))
    stable_orient(camera, center)
    camera.data.ortho_scale = longest * 1.10
    output = QA / "marker_overlay_left.png"
    scene.render.filepath = str(output)
    bpy.ops.render.render(write_still=True)
    for obj in overlays:
        bpy.data.objects.remove(obj, do_unlink=True)
    return str(output.relative_to(ROOT))


def export_fbx(root, path):
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


def main():
    PRODUCTION.mkdir(parents=True, exist_ok=True)
    TEXTURES.mkdir(parents=True, exist_ok=True)
    QA.mkdir(parents=True, exist_ok=True)
    for required in (RAW_GLB, BASE_PATH, NORMAL_PATH, ORM_PATH, OCCLUSION_PATH, METALLIC_SMOOTHNESS_PATH, EMISSION_PATH):
        if not required.is_file():
            raise FileNotFoundError(required)
    emission_report = json.loads((QA / "emission_mask_report.json").read_text(encoding="utf-8"))
    if emission_report.get("status") != "PASS":
        raise RuntimeError("Emission mask report is not PASS")

    bpy.ops.wm.read_factory_settings(use_empty=True)
    body, source_triangles, final_triangles = import_transform_decimate()
    material, images = configure_material(body)
    root = make_root_and_markers(body)
    minimum, maximum = mesh_bounds(body)
    center = (minimum + maximum) * 0.5
    longest = max(maximum - minimum)

    scene, camera, temporary = setup_qa(center, longest)
    pbr_views = render_views(scene, camera, center, longest, "pbr")

    original_material = body.data.materials[0]
    emission_qa = bpy.data.materials.new("QA_EmissionOnly")
    emission_qa.use_nodes = True
    qa_bsdf = emission_qa.node_tree.nodes.get("Principled BSDF")
    qa_bsdf.inputs["Base Color"].default_value = (0.005, 0.005, 0.005, 1.0)
    qa_bsdf.inputs["Metallic"].default_value = 0.0
    qa_bsdf.inputs["Roughness"].default_value = 0.7
    qa_bsdf.inputs["Emission Strength"].default_value = 5.0
    qa_image = emission_qa.node_tree.nodes.new("ShaderNodeTexImage")
    qa_image.image = images["emission"]
    emission_qa.node_tree.links.new(qa_image.outputs["Color"], qa_bsdf.inputs["Emission Color"])
    body.data.materials[0] = emission_qa
    emission_views = render_views(scene, camera, center, longest, "emission")
    body.data.materials[0] = original_material
    marker_overlay = render_marker_overlay(scene, camera, center, longest, root)

    for obj in temporary:
        if obj.name in bpy.data.objects:
            bpy.data.objects.remove(obj, do_unlink=True)
    if emission_qa.name in bpy.data.materials:
        bpy.data.materials.remove(emission_qa)

    marker_records = {}
    for name in ("RightHandGrip", "LeftHandGrip", "Muzzle"):
        marker = bpy.data.objects[name]
        marker_records[name] = {
            "parent": marker.parent.name if marker.parent else None,
            "local_location": v3(marker.location),
            "local_rotation": v3(marker.rotation_euler),
            "local_plus_z": v3(marker.matrix_local.to_3x3() @ Vector((0.0, 0.0, 1.0))),
        }

    root_objects = [obj for obj in bpy.context.scene.objects if obj.parent is None]
    prohibited = [obj.name for obj in bpy.context.scene.objects if obj.type in {"CAMERA", "LIGHT", "ARMATURE"}]
    texture_paths = [BASE_PATH, NORMAL_PATH, ORM_PATH, OCCLUSION_PATH, METALLIC_SMOOTHNESS_PATH, EMISSION_PATH]
    checks = {
        "single_root": len(root_objects) == 1 and root_objects[0] == root,
        "single_mesh": len([obj for obj in bpy.context.scene.objects if obj.type == "MESH"]) == 1,
        "root_identity_transform": v3(root.location) == [0.0, 0.0, 0.0] and v3(root.rotation_euler) == [0.0, 0.0, 0.0] and v3(root.scale) == [1.0, 1.0, 1.0],
        "mesh_identity_transform": v3(body.location) == [0.0, 0.0, 0.0] and v3(body.rotation_euler) == [0.0, 0.0, 0.0] and v3(body.scale) == [1.0, 1.0, 1.0],
        "right_hand_grip_at_root": marker_records["RightHandGrip"]["local_location"] == [0.0, 0.0, 0.0],
        "left_hand_grip_27cm_forward": abs(marker_records["LeftHandGrip"]["local_location"][2] - 0.27) < 1e-6,
        "muzzle_forward_positive_z": marker_records["Muzzle"]["local_location"][2] > 0.6 and marker_records["Muzzle"]["local_plus_z"] == [0.0, 0.0, 1.0],
        "no_camera_light_armature": not prohibited,
        "preservation_selected_250k": final_triangles == 249999,
        "pbr_texture_set_complete": all(path.is_file() for path in texture_paths),
        "emission_binary_global_rgb_family": emission_report["status"] == "PASS" and not emission_report["spatial_or_uv_restriction"] and not emission_report["dilation_or_expansion"],
        "white_not_emissive": not emission_report["white_body_can_match"] and not emission_report["white_sticker_hair_can_match"],
        "no_added_emission_geometry": len([obj for obj in bpy.context.scene.objects if obj.type == "MESH"]) == 1,
    }

    validation = {
        "item_id": ITEM_ID,
        "generated_at": now(),
        "blender_version": bpy.app.version_string,
        "source": {
            "task_id": "1fa3dd59-189c-4160-b050-36cd85e529d4",
            "status": "success",
            "consumed_credit": 30,
            "raw_glb": str(RAW_GLB.relative_to(ROOT)),
            "raw_gate": "Tripo/RetryAfterExpired20260825/RawQA/raw_gate.json",
            "source_triangles": source_triangles,
        },
        "coordinate_contract": {
            "raw_muzzle": "-X",
            "raw_up": "+Z",
            "production_muzzle": "+Z",
            "production_up": "+Y",
            "root_origin": "RightHandGrip / trigger grip center",
        },
        "scale_meters": MODEL_SCALE,
        "root": root.name,
        "mesh": body.name,
        "markers": marker_records,
        "bounds_root_local": {"min": v3(minimum), "max": v3(maximum), "dimensions": v3(maximum - minimum)},
        "decimation": {
            "source_triangles": source_triangles,
            "selected_triangles": final_triangles,
            "candidate_targets": [600000, 400000, 250000],
            "fixed_100k_target_used": False,
            "selection_report": "Production/decimation_selection.json",
        },
        "material": {
            "name": material.name,
            "shader": "Principled BSDF / Unity URP Lit handoff",
            "textures": [str(path.relative_to(ROOT)) for path in texture_paths],
            "emission_strength_blender_qa": 2.5,
            "emission_report": "Production/QA/emission_mask_report.json",
        },
        "qa": {"pbr_8_view": pbr_views, "emission_8_view": emission_views, "marker_overlay": marker_overlay},
        "prohibited_objects": prohibited,
        "checks": checks,
        "reimport": {"status": "pending"},
    }
    validation["pass"] = all(checks.values())
    VALIDATION_PATH.write_text(json.dumps(validation, ensure_ascii=False, indent=2), encoding="utf-8")
    if not validation["pass"]:
        raise RuntimeError("Production validation failed before save/export")

    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
    export_fbx(root, FBX_PATH)
    print(json.dumps({"blend": str(BLEND_PATH), "fbx": str(FBX_PATH), "triangles": final_triangles, "checks": checks}, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
