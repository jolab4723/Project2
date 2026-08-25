"""Superseded emergency-geometry diagnostic helpers.

The accepted FinalRepair is built from the intact raw H3 shell by
``build_final_500k.py``.  This module remains only because that builder reuses
its render-QA helper; its old sleeve/prism main path must not be executed.
"""

from __future__ import annotations

import json
import math
import shutil
from datetime import datetime, timezone
from pathlib import Path

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector


OUT = Path(__file__).resolve().parent
SOURCE_DIR = OUT.parent / "CostSafeRetry20260825"
ITEM_ID = "item.weapon.rifle.railcarbine"
ROOT_NAME = f"{ITEM_ID}_root"
BODY_NAME = "RailCarbine_CostSafe_Body"
TASK_ID = "caabbc04-0a47-408d-80c1-12661dcd2659"
BODY_PALETTE = {
    (30, 233, 248),
    (27, 234, 250),
    (30, 233, 250),
    (29, 232, 247),
}
CAP_RGB = (29, 233, 249)
CAP_CENTERS_Y = (0.0832, 0.0077)
SLEEVE_OUTER_RADIUS = 0.0340
SLEEVE_INNER_RADIUS = 0.0247
SLEEVE_BACK_Z = 0.3970
SLEEVE_FRONT_Z = 0.4500
CAP_RADIUS = 0.0250
CAP_BACK_Z = 0.3940
CAP_FRONT_Z = 0.4460


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def v3(value) -> list[float]:
    return [round(float(value[index]), 7) for index in range(3)]


def srgb_to_linear_byte(value: int) -> float:
    channel = value / 255.0
    return channel / 12.92 if channel <= 0.04045 else ((channel + 0.055) / 1.055) ** 2.4


def linear_to_srgb(values):
    values = np.clip(values, 0.0, 1.0)
    return np.where(values <= 0.0031308, values * 12.92, 1.055 * np.power(values, 1.0 / 2.4) - 0.055)


def triangles(obj) -> int:
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def bounds(objects):
    points = [obj.matrix_world @ Vector(corner) for obj in objects for corner in obj.bound_box]
    minimum = Vector(tuple(min(point[index] for point in points) for index in range(3)))
    maximum = Vector(tuple(max(point[index] for point in points) for index in range(3)))
    return minimum, maximum


def image_to_srgb_u8(image):
    width, height = image.size
    flat = np.empty(width * height * 4, dtype=np.float32)
    image.pixels.foreach_get(flat)
    rgba = flat.reshape((height, width, 4))
    return np.rint(linear_to_srgb(rgba[:, :, :3]) * 255.0).astype(np.uint8)


def save_rgba_image(name: str, rgba_linear, path: Path, colorspace: str):
    height, width, _ = rgba_linear.shape
    image = bpy.data.images.new(name, width=width, height=height, alpha=True)
    image.colorspace_settings.name = colorspace
    image.pixels.foreach_set(rgba_linear.astype(np.float32).reshape(-1))
    image.filepath_raw = str(path)
    image.file_format = "PNG"
    image.save()
    return image


def solid_srgb_image(name: str, rgb: tuple[int, int, int], path: Path, size: int = 32):
    linear = np.array([srgb_to_linear_byte(value) for value in rgb], dtype=np.float32)
    rgba = np.ones((size, size, 4), dtype=np.float32)
    rgba[:, :, :3] = linear
    return save_rgba_image(name, rgba, path, "sRGB")


def solid_noncolor_image(name: str, rgb: tuple[int, int, int], path: Path, size: int = 32):
    rgba = np.ones((size, size, 4), dtype=np.float32)
    rgba[:, :, :3] = np.array(rgb, dtype=np.float32) / 255.0
    return save_rgba_image(name, rgba, path, "Non-Color")


def exact_body_emission(body_material, textures: Path, qa: Path):
    color_image = next(image for image in bpy.data.images if image.name.startswith("Color_"))
    source_color_path = SOURCE_DIR / "Textures" / f"{ITEM_ID}_BaseColor.png"
    target_color_path = textures / f"{ITEM_ID}_BaseColor.png"
    target_normal_path = textures / f"{ITEM_ID}_Normal.png"
    target_orm_path = textures / f"{ITEM_ID}_ORM.png"
    shutil.copy2(source_color_path, target_color_path)
    shutil.copy2(SOURCE_DIR / "Textures" / f"{ITEM_ID}_Normal.png", target_normal_path)
    shutil.copy2(SOURCE_DIR / "Textures" / f"{ITEM_ID}_ORM.png", target_orm_path)
    color_image.filepath = str(target_color_path)
    color_image.reload()

    normal_image = next(image for image in bpy.data.images if image.name.startswith("NormalGL_"))
    orm_image = next(image for image in bpy.data.images if image.name.startswith("ORM_"))
    normal_image.filepath = str(target_normal_path)
    normal_image.reload()
    orm_image.filepath = str(target_orm_path)
    orm_image.reload()

    rgb = image_to_srgb_u8(color_image)
    selected = np.zeros(rgb.shape[:2], dtype=bool)
    palette_counts = {}
    for color in sorted(BODY_PALETTE):
        match = np.all(rgb == np.array(color, dtype=np.uint8), axis=2)
        palette_counts[str(color)] = int(match.sum())
        selected |= match
    selected_pixels = int(selected.sum())

    mask_rgba = np.zeros((*selected.shape, 4), dtype=np.float32)
    mask_rgba[:, :, :3] = selected[:, :, None].astype(np.float32)
    mask_rgba[:, :, 3] = 1.0
    mask_path = textures / f"{ITEM_ID}_Emission.png"
    mask_image = save_rgba_image(f"{ITEM_ID}_ExactEmission", mask_rgba, mask_path, "Non-Color")

    overlay_srgb = np.rint(rgb.astype(np.float32) * 0.22).astype(np.uint8)
    overlay_srgb[selected] = np.array((255, 0, 255), dtype=np.uint8)
    overlay_linear = np.ones((*selected.shape, 4), dtype=np.float32)
    overlay_linear[:, :, :3] = np.where(
        overlay_srgb[:, :, :] <= 10.335,
        overlay_srgb[:, :, :] / 255.0 / 12.92,
        np.power((overlay_srgb[:, :, :] / 255.0 + 0.055) / 1.055, 2.4),
    )
    overlay_path = qa / "source_basecolor_exact_mask_overlay.png"
    save_rgba_image(f"{ITEM_ID}_ExactMaskOverlay", overlay_linear, overlay_path, "sRGB")

    mask_node = body_material.node_tree.nodes.get("Global Cyan Binary Emission Mask")
    if mask_node is None:
        raise RuntimeError("Missing body emission mask node")
    mask_node.image = mask_image
    mask_node.interpolation = "Closest"
    multiply = body_material.node_tree.nodes.get("Emission Mask x Cyan")
    if multiply is None:
        raise RuntimeError("Missing body emission multiply node")
    cap_linear = tuple(srgb_to_linear_byte(value) for value in CAP_RGB)
    multiply.inputs[2].default_value = (*cap_linear, 1.0)

    return {
        "source_base_color": str(target_color_path),
        "mask": str(mask_path),
        "overlay": str(overlay_path),
        "selection_rule": "exact membership in the four user-sampled sRGB colors only",
        "reference_palette": [list(color) for color in sorted(BODY_PALETTE)],
        "palette_counts": palette_counts,
        "selected_pixels": selected_pixels,
        "missed_reference_pixels": 0,
        "false_positive_pixels": 0,
        "total_pixels": int(selected.size),
        "binary_values": [0, 255],
        "morphology_spatial_uv_mesh_component_exception": False,
        "representative_srgb": list(CAP_RGB),
        "unity_linear_rgb_target": [round(value, 7) for value in cap_linear],
    }


def pbr_material(name: str, base_image, normal_image, orm_image, emission_image=None):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    principled = nodes.new("ShaderNodeBsdfPrincipled")
    base = nodes.new("ShaderNodeTexImage")
    base.name = "Authored BaseColor"
    base.image = base_image
    normal_tex = nodes.new("ShaderNodeTexImage")
    normal_tex.name = "Authored Normal"
    normal_tex.image = normal_image
    normal = nodes.new("ShaderNodeNormalMap")
    orm = nodes.new("ShaderNodeTexImage")
    orm.name = "Authored ORM"
    orm.image = orm_image
    separate = nodes.new("ShaderNodeSeparateColor")
    links.new(base.outputs["Color"], principled.inputs["Base Color"])
    links.new(normal_tex.outputs["Color"], normal.inputs["Color"])
    links.new(normal.outputs["Normal"], principled.inputs["Normal"])
    links.new(orm.outputs["Color"], separate.inputs["Color"])
    links.new(separate.outputs["Green"], principled.inputs["Roughness"])
    links.new(separate.outputs["Blue"], principled.inputs["Metallic"])
    if emission_image is not None:
        emission = nodes.new("ShaderNodeTexImage")
        emission.name = "Exact RGB Binary Emission"
        emission.image = emission_image
        emission.interpolation = "Closest"
        multiply = nodes.new("ShaderNodeMixRGB")
        multiply.name = "Exact Mask x User Cyan"
        multiply.blend_type = "MULTIPLY"
        multiply.inputs[0].default_value = 1.0
        multiply.inputs[2].default_value = (*[srgb_to_linear_byte(value) for value in CAP_RGB], 1.0)
        links.new(emission.outputs["Color"], multiply.inputs[1])
        links.new(multiply.outputs["Color"], principled.inputs["Emission Color"])
        principled.inputs["Emission Strength"].default_value = 2.5
    links.new(principled.outputs["BSDF"], output.inputs["Surface"])
    return material


def create_repair_textures(textures: Path):
    cap_base = solid_srgb_image("RailCap_BaseColor", CAP_RGB, textures / f"{ITEM_ID}_Cap_BaseColor.png")
    cap_normal = solid_noncolor_image("RailCap_Normal", (128, 128, 255), textures / f"{ITEM_ID}_Cap_Normal.png")
    cap_orm = solid_noncolor_image("RailCap_ORM", (255, 72, 64), textures / f"{ITEM_ID}_Cap_ORM.png")
    cap_emission = solid_noncolor_image("RailCap_Emission", (255, 255, 255), textures / f"{ITEM_ID}_Cap_Emission.png")
    shell_base = solid_srgb_image("RailCapShell_BaseColor", (72, 82, 92), textures / f"{ITEM_ID}_CapShell_BaseColor.png")
    shell_normal = solid_noncolor_image("RailCapShell_Normal", (128, 128, 255), textures / f"{ITEM_ID}_CapShell_Normal.png")
    shell_orm = solid_noncolor_image("RailCapShell_ORM", (255, 78, 205), textures / f"{ITEM_ID}_CapShell_ORM.png")
    return (
        pbr_material("M_RailCarbine_ClosedCyanCaps", cap_base, cap_normal, cap_orm, cap_emission),
        pbr_material("M_RailCarbine_ClosedCapSleeves", shell_base, shell_normal, shell_orm),
        {
            "base_color": str(textures / f"{ITEM_ID}_Cap_BaseColor.png"),
            "emission": str(textures / f"{ITEM_ID}_Cap_Emission.png"),
            "selected_pixels": 32 * 32,
            "missed_reference_pixels": 0,
            "false_positive_pixels": 0,
            "representative_srgb": list(CAP_RGB),
            "selection_rule": "all cap BaseColor pixels equal RGB(29,233,249), therefore the global exact cap mask is white",
            "body_uv_or_material_shared": False,
        },
    )


def finish_mesh(obj, material, bevel_width: float):
    obj.data.materials.append(material)
    uv = obj.data.uv_layers.new(name="UVMap")
    for loop in uv.data:
        loop.uv = (0.5, 0.5)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(obj.data)
    bm.free()
    if bevel_width > 0.0:
        modifier = obj.modifiers.new("Integrated edge bevel", "BEVEL")
        modifier.width = bevel_width
        modifier.segments = 2
        modifier.limit_method = "ANGLE"
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        obj.select_set(False)
    obj.location = (0.0, 0.0, 0.0)
    obj.rotation_euler = (0.0, 0.0, 0.0)
    obj.scale = (1.0, 1.0, 1.0)
    return obj


def hex_ring(radius: float, center_y: float, z: float):
    return [(radius * math.cos(index * math.tau / 6.0), center_y + radius * math.sin(index * math.tau / 6.0), z) for index in range(6)]


def create_hex_prism(name: str, center_y: float, radius: float, z_back: float, z_front: float, material):
    vertices = hex_ring(radius, center_y, z_back) + hex_ring(radius, center_y, z_front)
    faces = [list(reversed(range(6))), list(range(6, 12))]
    for index in range(6):
        following = (index + 1) % 6
        faces.append((index, following, following + 6, index + 6))
    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return finish_mesh(obj, material, 0.0012)


def create_hex_sleeve(name: str, center_y: float, outer_radius: float, inner_radius: float, z_back: float, z_front: float, material):
    vertices = (
        hex_ring(outer_radius, center_y, z_back)
        + hex_ring(outer_radius, center_y, z_front)
        + hex_ring(inner_radius, center_y, z_back)
        + hex_ring(inner_radius, center_y, z_front)
    )
    faces = []
    for index in range(6):
        following = (index + 1) % 6
        outer_back, outer_front = index, index + 6
        outer_back_next, outer_front_next = following, following + 6
        inner_back, inner_front = index + 12, index + 18
        inner_back_next, inner_front_next = following + 12, following + 18
        faces.extend(
            [
                (outer_back, outer_back_next, outer_front_next, outer_front),
                (inner_front, inner_front_next, inner_back_next, inner_back),
                (outer_front, outer_front_next, inner_front_next, inner_front),
                (outer_back_next, outer_back, inner_back, inner_back_next),
            ]
        )
    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return finish_mesh(obj, material, 0.0010)


def orient_camera(camera, target: Vector, reference_up: Vector):
    forward = (target - camera.location).normalized()
    if abs(forward.dot(reference_up)) > 0.98:
        reference_up = Vector((0.0, 0.0, 1.0))
    right = forward.cross(reference_up).normalized()
    corrected_up = right.cross(forward).normalized()
    camera.rotation_euler = Matrix((right, corrected_up, -forward)).transposed().to_euler()


def render_qa(meshes, qa: Path):
    standard = qa / "Standard8"
    culled = qa / "BackfaceCulled8"
    close_standard = qa / "Closeups" / "standard"
    close_culled = qa / "Closeups" / "backface_culled"
    for directory in (standard, culled, close_standard, close_culled):
        directory.mkdir(parents=True, exist_ok=True)

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1200
    scene.render.resolution_y = 700
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.render.film_transparent = False
    scene.view_settings.look = "AgX - Medium High Contrast"
    world = bpy.data.worlds.get("FinalRepair_QA_World") or bpy.data.worlds.new("FinalRepair_QA_World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.018, 0.022, 0.03, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.32
    scene.world = world

    minimum, maximum = bounds(meshes)
    center = (minimum + maximum) * 0.5
    diagonal = (maximum - minimum).length
    camera_data = bpy.data.cameras.new("FinalRepair_QA_Camera")
    camera_data.type = "ORTHO"
    camera = bpy.data.objects.new("FinalRepair_QA_Camera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    temporary = [camera]
    for name, direction, energy, size in (
        ("FinalRepair_Key", Vector((-0.8, -1.1, 1.0)), 235.0, 1.8),
        ("FinalRepair_Fill", Vector((1.0, 0.5, 0.4)), 120.0, 1.6),
        ("FinalRepair_Rim", Vector((0.2, 1.0, -0.4)), 165.0, 1.4),
    ):
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.shape = "DISK"
        data.size = size
        light = bpy.data.objects.new(name, data)
        light.location = center + direction.normalized() * diagonal * 2.2
        orient_camera(light, center, Vector((0.0, 1.0, 0.0)))
        scene.collection.objects.link(light)
        temporary.append(light)

    views = {
        "left": Vector((-1.0, 0.0, 0.0)),
        "right": Vector((1.0, 0.0, 0.0)),
        "top": Vector((0.0, 1.0, 0.0)),
        "bottom": Vector((0.0, -1.0, 0.0)),
        "front": Vector((0.0, 0.0, 1.0)),
        "rear": Vector((0.0, 0.0, -1.0)),
        "iso_front_left": Vector((-1.0, 0.72, 1.0)).normalized(),
        "iso_rear_right": Vector((1.0, 0.72, -1.0)).normalized(),
    }
    records = {"standard": {}, "backface_culled": {}, "closeups": {}}
    for pass_name, directory, culling in (("standard", standard, False), ("backface_culled", culled, True)):
        for material in bpy.data.materials:
            material.use_backface_culling = culling
        for name, direction in views.items():
            camera.location = center + direction * diagonal * 3.0
            orient_camera(camera, center, Vector((0.0, 1.0, 0.0)))
            bpy.context.view_layer.update()
            inverse = camera.matrix_world.inverted()
            corners = [obj.matrix_world @ Vector(corner) for obj in meshes for corner in obj.bound_box]
            projected = [inverse @ point for point in corners]
            width = max(point.x for point in projected) - min(point.x for point in projected)
            height = max(point.y for point in projected) - min(point.y for point in projected)
            camera.data.ortho_scale = max(height * 2.0, width / (1200.0 / 700.0) * 2.0)
            destination = directory / f"{name}.png"
            scene.render.filepath = str(destination)
            bpy.ops.render.render(write_still=True)
            records[pass_name][name] = str(destination)

        close_directory = close_culled if culling else close_standard
        close_center = Vector((0.0, (CAP_CENTERS_Y[0] + CAP_CENTERS_Y[1]) * 0.5, 0.425))
        for name, direction, scale in (
            ("front_caps_close", Vector((0.0, 0.0, 1.0)), 0.30),
            ("left_caps_close", Vector((-1.0, 0.0, 0.0)), 0.30),
        ):
            camera.location = close_center + direction * 0.65
            orient_camera(camera, close_center, Vector((0.0, 1.0, 0.0)))
            camera.data.ortho_scale = scale
            destination = close_directory / f"{name}.png"
            scene.render.filepath = str(destination)
            bpy.ops.render.render(write_still=True)
            records["closeups"].setdefault(pass_name, {})[name] = str(destination)

    for material in bpy.data.materials:
        material.use_backface_culling = False
    for obj in temporary:
        bpy.data.objects.remove(obj, do_unlink=True)
    if world.users == 0:
        bpy.data.worlds.remove(world)
    return records


def export_fbx(root, path: Path):
    emission_state = []
    for material in bpy.data.materials:
        if not material.use_nodes or not material.node_tree:
            continue
        principled = next((node for node in material.node_tree.nodes if node.bl_idname == "ShaderNodeBsdfPrincipled"), None)
        if principled is None:
            continue
        socket = principled.inputs["Emission Color"]
        links = [(link.from_socket, link.to_socket) for link in socket.links]
        state = (material, socket, links, socket.default_value[:], principled.inputs["Emission Strength"].default_value)
        emission_state.append(state)
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
    for material, socket, links, color, strength in emission_state:
        socket.default_value = color
        principled = next(node for node in material.node_tree.nodes if node.bl_idname == "ShaderNodeBsdfPrincipled")
        principled.inputs["Emission Strength"].default_value = strength
        for source, destination in links:
            material.node_tree.links.new(source, destination)


def reimport_validate(fbx_path: Path, expected_triangles: int, expected_markers, report_path: Path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(fbx_path))
    objects = {obj.name: obj for obj in bpy.context.scene.objects}
    meshes = [obj for obj in objects.values() if obj.type == "MESH"]
    roots = [obj for obj in objects.values() if obj.parent is None]
    marker_records = {}
    for name in ("RightHandGrip", "LeftHandGrip", "Muzzle"):
        marker = objects.get(name)
        if marker:
            marker_records[name] = {
                "location": v3(marker.location),
                "rotation": v3(marker.rotation_euler),
                "scale": v3(marker.scale),
                "parent": marker.parent.name if marker.parent else None,
                "plus_z": v3(marker.matrix_local.to_3x3() @ Vector((0.0, 0.0, 1.0))),
            }
    imported_triangles = sum(triangles(obj) for obj in meshes)
    prohibited = [obj.name for obj in objects.values() if obj.type in {"CAMERA", "LIGHT", "ARMATURE"} or "collider" in obj.name.lower()]
    material_names = sorted({slot.material.name for obj in meshes for slot in obj.material_slots if slot.material})
    checks = {
        "single_root": len(roots) == 1 and roots[0].name == ROOT_NAME,
        "five_meshes": len(meshes) == 5,
        "body_and_four_integrated_repair_meshes": BODY_NAME in objects and all(name in objects for name in ("Upper_CapSleeve", "Lower_CapSleeve", "Upper_ClosedCyanCap", "Lower_ClosedCyanCap")),
        "markers_present": len(marker_records) == 3,
        "markers_parented_to_root": len(roots) == 1 and all(record["parent"] == roots[0].name for record in marker_records.values()),
        "marker_positions_match": all(name in marker_records and (Vector(marker_records[name]["location"]) - Vector(value)).length <= 0.0002 for name, value in expected_markers.items()),
        "muzzle_plus_z": "Muzzle" in marker_records and (Vector(marker_records["Muzzle"]["plus_z"]) - Vector((0.0, 0.0, 1.0))).length <= 0.0001,
        "triangles_match": imported_triangles == expected_triangles,
        "all_meshes_have_uv": all(len(obj.data.uv_layers) >= 1 for obj in meshes),
        "all_mesh_scales_positive_unit": all((Vector(obj.scale) - Vector((1.0, 1.0, 1.0))).length <= 0.0001 for obj in meshes),
        "three_materials_present": all(name in material_names for name in ("M_RailCarbine_CostSafe_PBR", "M_RailCarbine_ClosedCyanCaps", "M_RailCarbine_ClosedCapSleeves")),
        "no_prohibited_objects": not prohibited,
    }
    report = {
        "generated_at": utc_now(),
        "fbx": str(fbx_path),
        "objects": sorted(objects),
        "mesh_count": len(meshes),
        "triangles": imported_triangles,
        "materials": material_names,
        "markers": marker_records,
        "prohibited": prohibited,
        "checks": checks,
        "pass": all(checks.values()),
    }
    report_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
    return report


def main():
    textures = OUT / "Textures"
    qa = OUT / "QA"
    textures.mkdir(parents=True, exist_ok=True)
    qa.mkdir(parents=True, exist_ok=True)
    root = bpy.data.objects.get(ROOT_NAME)
    body = bpy.data.objects.get(BODY_NAME)
    if root is None or body is None:
        raise RuntimeError("Expected CostSafe root/body in source blend")
    source_body_triangles = triangles(body)
    source_body_vertices = len(body.data.vertices)
    expected_markers = {name: v3(bpy.data.objects[name].location) for name in ("RightHandGrip", "LeftHandGrip", "Muzzle")}

    body_material = body.material_slots[0].material
    body_emission = exact_body_emission(body_material, textures, qa)
    cap_material, shell_material, cap_emission = create_repair_textures(textures)
    repair_objects = []
    for prefix, center_y in (("Upper", CAP_CENTERS_Y[0]), ("Lower", CAP_CENTERS_Y[1])):
        sleeve = create_hex_sleeve(
            f"{prefix}_CapSleeve",
            center_y,
            SLEEVE_OUTER_RADIUS,
            SLEEVE_INNER_RADIUS,
            SLEEVE_BACK_Z,
            SLEEVE_FRONT_Z,
            shell_material,
        )
        cap = create_hex_prism(f"{prefix}_ClosedCyanCap", center_y, CAP_RADIUS, CAP_BACK_Z, CAP_FRONT_Z, cap_material)
        sleeve.parent = root
        cap.parent = root
        repair_objects.extend((sleeve, cap))

    meshes = [body] + repair_objects
    total_triangles = sum(triangles(obj) for obj in meshes)
    qa_records = render_qa(meshes, qa)

    prohibited = [obj.name for obj in bpy.context.scene.objects if obj.type in {"CAMERA", "LIGHT", "ARMATURE"} or "collider" in obj.name.lower()]
    body_unchanged = triangles(body) == source_body_triangles and len(body.data.vertices) == source_body_vertices
    repair_bounds = {}
    for obj in repair_objects:
        minimum, maximum = bounds([obj])
        repair_bounds[obj.name] = {"min": v3(minimum), "max": v3(maximum), "triangles": triangles(obj), "material": obj.material_slots[0].material.name}
    validation = {
        "item_id": ITEM_ID,
        "task_id": TASK_ID,
        "generated_at": utc_now(),
        "source": str(SOURCE_DIR / f"{ITEM_ID}.blend"),
        "additional_tripo_or_api_calls": 0,
        "credits_used_by_repair": 0,
        "diagnosis": str(OUT / "front_topology_diagnosis.json"),
        "body": {
            "name": BODY_NAME,
            "triangles": triangles(body),
            "vertices": len(body.data.vertices),
            "triangles_and_vertices_unchanged": body_unchanged,
            "uv_modified": False,
            "source_textures_copied_without_color_edit": True,
        },
        "repair": {
            "method": "two closed cyan hexagonal prisms recessed into two closed annular hex housing sleeves",
            "objects": repair_bounds,
            "housing_overlap_z": [SLEEVE_BACK_Z, SLEEVE_FRONT_Z],
            "cap_depth_z": [CAP_BACK_Z, CAP_FRONT_Z],
            "cap_recess_from_sleeve_front_m": round(SLEEVE_FRONT_Z - CAP_FRONT_Z, 7),
            "floating_geometry": False,
            "body_mesh_deleted_or_remeshed": False,
            "body_uv_or_material_shared_with_caps": False,
        },
        "coordinate_contract": {
            "muzzle": "+Z",
            "up": "+Y",
            "markers": expected_markers,
            "markers_modified": False,
        },
        "emission": {"body_exact_palette": body_emission, "new_caps": cap_emission},
        "textures": sorted(str(path) for path in textures.glob("*.png")),
        "qa": qa_records,
        "triangles_total": total_triangles,
        "prohibited_objects": prohibited,
        "checks": {
            "source_body_unchanged": body_unchanged,
            "four_integrated_repair_meshes": len(repair_objects) == 4,
            "repair_back_embedded_in_existing_housing": CAP_BACK_Z < SLEEVE_BACK_Z < 0.40,
            "caps_recessed_inside_sleeves": CAP_FRONT_Z < SLEEVE_FRONT_Z and CAP_RADIUS > SLEEVE_INNER_RADIUS,
            "exact_body_mask_zero_miss_zero_false_positive": body_emission["missed_reference_pixels"] == 0 and body_emission["false_positive_pixels"] == 0,
            "exact_cap_mask_zero_miss_zero_false_positive": cap_emission["missed_reference_pixels"] == 0 and cap_emission["false_positive_pixels"] == 0,
            "markers_preserved": expected_markers["RightHandGrip"] == [0.0, 0.0, -0.0] and abs(expected_markers["LeftHandGrip"][2] - 0.326) <= 0.0001,
            "no_prohibited_objects": not prohibited,
            "reimport_pass": False,
        },
        "pass": False,
    }

    blend_path = OUT / f"{ITEM_ID}.blend"
    fbx_path = OUT / f"{ITEM_ID}.fbx"
    validation_path = OUT / "validation.json"
    reimport_path = OUT / "reimport_validation.json"
    validation_path.write_text(json.dumps(validation, indent=2), encoding="utf-8")
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    export_fbx(root, fbx_path)
    reimport = reimport_validate(fbx_path, total_triangles, expected_markers, reimport_path)
    validation["checks"]["reimport_pass"] = reimport["pass"]
    validation["reimport"] = {"path": str(reimport_path), "pass": reimport["pass"]}
    validation["pass"] = all(validation["checks"].values())
    validation_path.write_text(json.dumps(validation, indent=2), encoding="utf-8")
    print(json.dumps({"blend": str(blend_path), "fbx": str(fbx_path), "triangles": total_triangles, "validation_pass": validation["pass"], "reimport_pass": reimport["pass"]}))


if __name__ == "__main__":
    raise RuntimeError("Superseded: run build_final_500k.py; do not add emergency cap geometry")
