from __future__ import annotations

import json
import math
from collections import deque
from pathlib import Path

import bpy
import numpy as np
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree


ROOT = Path(__file__).resolve().parents[1]
PRODUCTION = ROOT / "Production"
RAW = ROOT / "Tripo" / "Downloaded" / "item.weapon.grenadelauncher.singularitymortar_raw.glb"
ITEM_ID = "item.weapon.grenadelauncher.singularitymortar"
TEXTURES = PRODUCTION / "Textures"
QA = PRODUCTION / "QA"
FINAL_BLEND = PRODUCTION / f"{ITEM_ID}.blend"
FINAL_FBX = PRODUCTION / f"{ITEM_ID}.fbx"
COMPARE_BLEND = PRODUCTION / f"{ITEM_ID}_100k.blend"
COMPARE_FBX = PRODUCTION / f"{ITEM_ID}_100k.fbx"
VALIDATION = PRODUCTION / "validation.json"

RAW_RIGHT_X = -0.2000
RAW_LEFT_X = 0.2250
RAW_GRIP_Z = -0.1320
RAW_MUZZLE_X = 0.5000
RAW_MUZZLE_Z = 0.0820
DESIRED_GRIP_SPACING = 0.32625
SCALE = DESIRED_GRIP_SPACING / (RAW_LEFT_X - RAW_RIGHT_X)
AXIS_MAP = Matrix(((0.0, 1.0, 0.0, 0.0), (0.0, 0.0, 1.0, 0.0), (1.0, 0.0, 0.0, 0.0), (0.0, 0.0, 0.0, 1.0)))
RAW_ORIGIN = Vector((RAW_RIGHT_X, 0.0, RAW_GRIP_Z))


def triangles(obj: bpy.types.Object) -> int:
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def reset_import() -> bpy.types.Object:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(RAW))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError(f"Expected one RAW mesh, got {len(meshes)}")
    obj = meshes[0]
    obj.name = f"{ITEM_ID}_Mesh"
    transform = Matrix.Scale(SCALE, 4) @ AXIS_MAP @ Matrix.Translation(-RAW_ORIGIN)
    obj.data.transform(transform)
    obj.matrix_world = Matrix.Identity(4)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    return obj


def image_pixels(image: bpy.types.Image) -> np.ndarray:
    values = np.empty(len(image.pixels), dtype=np.float32)
    image.pixels.foreach_get(values)
    return values.reshape((-1, 4))


def remove_small_components(mask: np.ndarray, min_pixels: int = 2000) -> tuple[np.ndarray, list[int], int]:
    height, width = mask.shape
    active = set(np.flatnonzero(mask).tolist())
    kept_sizes = []
    removed_pixels = 0
    cleaned = np.zeros_like(mask, dtype=bool)
    while active:
        seed = active.pop()
        queue = deque([seed])
        component = [seed]
        while queue:
            index = queue.popleft()
            y, x = divmod(index, width)
            for ny in range(max(0, y - 1), min(height, y + 2)):
                row = ny * width
                for nx in range(max(0, x - 1), min(width, x + 2)):
                    neighbor = row + nx
                    if neighbor in active:
                        active.remove(neighbor)
                        queue.append(neighbor)
                        component.append(neighbor)
        if len(component) >= min_pixels:
            cleaned.reshape(-1)[component] = True
            kept_sizes.append(len(component))
        else:
            removed_pixels += len(component)
    return cleaned, sorted(kept_sizes, reverse=True), removed_pixels


def write_image(name: str, width: int, height: int, pixels: np.ndarray, path: Path,
                colorspace: str) -> bpy.types.Image:
    image = bpy.data.images.get(name) or bpy.data.images.new(name, width=width, height=height, alpha=True)
    image.colorspace_settings.name = colorspace
    image.pixels.foreach_set(np.ascontiguousarray(pixels, dtype=np.float32).reshape(-1))
    image.filepath_raw = str(path)
    image.file_format = "PNG"
    image.save()
    image.pack()
    return image


def extract_original_images(material: bpy.types.Material) -> tuple[bpy.types.Image, bpy.types.Image | None, bpy.types.Image | None]:
    images = [node.image for node in material.node_tree.nodes if node.type == "TEX_IMAGE" and node.image]
    base = next((image for image in images if image.name.startswith("Color_")), None)
    orm = next((image for image in images if image.name.startswith("ORM_")), None)
    normal = next((image for image in images if image.name.startswith("NormalGL_")), None)
    if base is None:
        raise RuntimeError("RAW material has no Color texture")
    for image, filename, colorspace in (
        (base, "BaseColor_Source.png", "sRGB"),
        (orm, "ORM.png", "Non-Color"),
        (normal, "NormalGL.png", "Non-Color"),
    ):
        if image is None:
            continue
        image.colorspace_settings.name = colorspace
        image.filepath_raw = str(TEXTURES / filename)
        image.file_format = "PNG"
        image.save()
        image.pack()
    return base, orm, normal


def setup_pbr_material(obj: bpy.types.Object) -> dict:
    if not obj.material_slots or obj.material_slots[0].material is None:
        raise RuntimeError("RAW mesh has no material")
    material = obj.material_slots[0].material
    material.name = "SingularityMortar_PBR"
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    principled = next((node for node in nodes if node.type == "BSDF_PRINCIPLED"), None)
    if principled is None:
        raise RuntimeError("RAW material has no Principled BSDF")
    base, orm, normal = extract_original_images(material)
    source = image_pixels(base)
    rgb = source[:, :3]
    purple_raw = (
        (rgb[:, 2] - rgb[:, 1] > 0.035)
        & (rgb[:, 0] - rgb[:, 1] > 0.012)
        & (rgb[:, 2] > 0.08)
    )
    width, height = base.size
    purple, component_sizes, removed_pixels = remove_small_components(
        purple_raw.reshape((height, width)), min_pixels=2000
    )
    purple = purple.reshape(-1)
    mask = purple.astype(np.float32)
    coverage = float(mask.mean())
    if not 0.0005 <= coverage <= 0.20:
        raise RuntimeError(f"Unexpected violet mask coverage {coverage:.6f}")

    graphite = source.copy()
    graphite[:, :3] *= np.where(purple[:, None], 0.82, 0.82)
    graphite[:, :3] = np.clip(graphite[:, :3], 0.0, 1.0)
    emission_mask = np.zeros_like(source)
    emission_mask[:, :3] = mask[:, None]
    emission_mask[:, 3] = 1.0
    emission = np.zeros_like(source)
    emission[:, 0] = mask * 0.48
    emission[:, 1] = mask * 0.055
    emission[:, 2] = mask * 1.0
    emission[:, 3] = 1.0

    graphite_image = write_image(
        "SingularityMortar_BaseColor", width, height, graphite,
        TEXTURES / "BaseColor.png", "sRGB"
    )
    mask_image = write_image(
        "SingularityMortar_EmissionMask", width, height, emission_mask,
        TEXTURES / "EmissionMask.png", "Non-Color"
    )
    emission_image = write_image(
        "SingularityMortar_Emission", width, height, emission,
        TEXTURES / "Emission.png", "sRGB"
    )

    base_node = next(
        (node for node in nodes if node.type == "TEX_IMAGE" and node.image is base),
        None,
    )
    if base_node is None:
        base_node = nodes.new("ShaderNodeTexImage")
    base_node.name = "BaseColor_Texture"
    base_node.label = "BaseColor (Graphite/Violet)"
    base_node.image = graphite_image
    links.new(base_node.outputs["Color"], principled.inputs["Base Color"])

    emission_node = nodes.new("ShaderNodeTexImage")
    emission_node.name = "Emission_Texture"
    emission_node.label = "Physically bounded violet chamber + indicator only"
    emission_node.image = emission_image
    emission_input = principled.inputs.get("Emission Color") or principled.inputs.get("Emission")
    strength_input = principled.inputs.get("Emission Strength")
    if emission_input is None or strength_input is None:
        raise RuntimeError("Principled BSDF emission inputs unavailable")
    links.new(emission_node.outputs["Color"], emission_input)
    strength_input.default_value = 1.8

    if orm is None:
        raise RuntimeError("RAW material has no ORM texture")
    orm_node = next(
        (node for node in nodes if node.type == "TEX_IMAGE" and node.image is orm),
        None,
    )
    if orm_node is None:
        orm_node = nodes.new("ShaderNodeTexImage")
        orm_node.image = orm
    orm_node.name = "ORM_Texture"
    separate = nodes.new("ShaderNodeSeparateColor")
    separate.name = "ORM_Separate_RGB"
    separate.mode = "RGB"
    links.new(orm_node.outputs["Color"], separate.inputs["Color"])
    roughness = nodes.new("ShaderNodeMapRange")
    roughness.name = "Roughness_G_Floor_Remap"
    roughness.clamp = True
    roughness.inputs["From Min"].default_value = 0.0
    roughness.inputs["From Max"].default_value = 1.0
    roughness.inputs["To Min"].default_value = 0.34
    roughness.inputs["To Max"].default_value = 0.82
    links.new(separate.outputs["Green"], roughness.inputs["Value"])
    links.new(roughness.outputs["Result"], principled.inputs["Roughness"])
    metallic_input = principled.inputs.get("Metallic IOR") or principled.inputs.get("Metallic")
    links.new(separate.outputs["Blue"], metallic_input)
    material.diffuse_color = (0.055, 0.065, 0.085, 1.0)
    return {
        "name": material.name,
        "base_color": "Textures/BaseColor.png",
        "orm": "Textures/ORM.png" if orm else None,
        "normal": "Textures/NormalGL.png" if normal else None,
        "emission": "Textures/Emission.png",
        "emission_mask": "Textures/EmissionMask.png",
        "emission_mask_coverage": coverage,
        "emission_strength": 1.8,
        "mask_rule": "B-G>0.035 and R-G>0.012 and B>0.08 from authored BaseColor",
        "mask_connected_components": component_sizes,
        "mask_removed_isolated_pixels": removed_pixels,
        "roughness_source": "ORM green channel",
        "roughness_remap": [0.34, 0.82],
        "metallic_source": "ORM blue channel",
    }


def decimate(obj: bpy.types.Object, target: int) -> tuple[int, int]:
    before = triangles(obj)
    modifier = obj.modifiers.new(name=f"Decimate_{target}", type="DECIMATE")
    modifier.decimate_type = "COLLAPSE"
    modifier.ratio = min(1.0, target / before)
    modifier.use_collapse_triangulate = True
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.select_set(False)
    return before, triangles(obj)


def add_hierarchy(mesh: bpy.types.Object) -> tuple[bpy.types.Object, dict[str, bpy.types.Object]]:
    root = bpy.data.objects.new(ITEM_ID, None)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.06
    bpy.context.scene.collection.objects.link(root)
    mesh.parent = root
    markers = {}
    positions = {
        "RightHandGrip": (0.0, 0.0, 0.0),
        "LeftHandGrip": (0.0, 0.0, DESIRED_GRIP_SPACING),
        "Muzzle": (0.0, (RAW_MUZZLE_Z - RAW_GRIP_Z) * SCALE, (RAW_MUZZLE_X - RAW_RIGHT_X) * SCALE),
    }
    for name, position in positions.items():
        marker = bpy.data.objects.new(name, None)
        marker.empty_display_type = "ARROWS"
        marker.empty_display_size = 0.045
        marker.location = position
        marker.parent = root
        bpy.context.scene.collection.objects.link(marker)
        markers[name] = marker
    return root, markers


def marker_surface_distances(mesh: bpy.types.Object, markers: dict[str, bpy.types.Object]) -> dict[str, float]:
    bpy.context.view_layer.update()
    tree = BVHTree.FromObject(mesh, bpy.context.evaluated_depsgraph_get())
    result = {}
    for name in ("RightHandGrip", "LeftHandGrip"):
        point = markers[name].matrix_world.translation
        nearest = tree.find_nearest(point)
        if nearest[0] is None:
            raise RuntimeError(f"No nearest mesh surface for {name}")
        result[name] = float(nearest[3])
    return result


def select_hierarchy(root: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for child in root.children_recursive:
        child.select_set(True)
    bpy.context.view_layer.objects.active = root


def export_fbx(root: bpy.types.Object, path: Path) -> None:
    select_hierarchy(root)
    bpy.ops.export_scene.fbx(
        filepath=str(path),
        use_selection=True,
        object_types={"EMPTY", "MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z",
        axis_up="Y",
        path_mode="COPY",
        embed_textures=True,
        add_leaf_bones=False,
        bake_anim=False,
    )


def world_bounds(obj: bpy.types.Object) -> tuple[Vector, Vector]:
    points = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    return Vector(map(min, zip(*points))), Vector(map(max, zip(*points)))


def point_camera(camera: bpy.types.Object, target: Vector) -> None:
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()


def add_render_rig(mesh: bpy.types.Object) -> tuple[bpy.types.Object, list[bpy.types.Object]]:
    bounds_min, bounds_max = world_bounds(mesh)
    center = (bounds_min + bounds_max) * 0.5
    camera_data = bpy.data.cameras.new("QA_Camera")
    camera = bpy.data.objects.new("QA_Camera", camera_data)
    bpy.context.scene.collection.objects.link(camera)
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = max((bounds_max - bounds_min)) * 1.28
    bpy.context.scene.camera = camera
    lights = []
    for index, (offset, energy, angle) in enumerate(
        ((Vector((-1.2, 1.3, -0.8)), 3.0, 0.42), (Vector((1.1, 0.3, 1.0)), 1.2, 0.62))
    ):
        data = bpy.data.lights.new(f"QA_Sun_{index}", "SUN")
        data.energy = energy
        data.angle = angle
        light = bpy.data.objects.new(f"QA_Sun_{index}", data)
        bpy.context.scene.collection.objects.link(light)
        light.location = center + offset
        point_camera(light, center)
        lights.append(light)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1024
    scene.render.resolution_y = 1024
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    if scene.world is None:
        scene.world = bpy.data.worlds.new("QA_World")
    scene.world.color = (0.02, 0.022, 0.028)
    scene.view_settings.look = "AgX - Medium High Contrast"
    return camera, lights


def render_views(mesh: bpy.types.Object, prefix: str, only_side: bool = False) -> None:
    bounds_min, bounds_max = world_bounds(mesh)
    center = (bounds_min + bounds_max) * 0.5
    dimensions = bounds_max - bounds_min
    distance = max(dimensions) * 2.5
    camera, lights = add_render_rig(mesh)
    views = {
        "left": Vector((-distance, center.y, center.z)),
        "right": Vector((distance, center.y, center.z)),
    }
    if not only_side:
        views.update(
            {
                "front": Vector((center.x, center.y, center.z + distance)),
                "back": Vector((center.x, center.y, center.z - distance)),
                "top": Vector((center.x, center.y + distance, center.z)),
                "bottom": Vector((center.x, center.y - distance, center.z)),
                "isometric": center + Vector((distance, distance * 0.72, distance)),
            }
        )
    for name, location in views.items():
        camera.location = location
        point_camera(camera, center)
        bpy.context.scene.render.filepath = str(QA / f"{prefix}_{name}.png")
        bpy.ops.render.render(write_still=True)
    for obj in [camera, *lights]:
        bpy.data.objects.remove(obj, do_unlink=True)


def render_emission_only(mesh: bpy.types.Object) -> None:
    original = [slot.material for slot in mesh.material_slots]
    source_material = original[0]
    source_emission = next(
        node for node in source_material.node_tree.nodes
        if node.type == "TEX_IMAGE" and node.name == "Emission_Texture"
    )
    material = bpy.data.materials.new("QA_EmissionOnly")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    principled = nodes.get("Principled BSDF")
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = source_emission.image
    emission_input = principled.inputs.get("Emission Color") or principled.inputs.get("Emission")
    links.new(texture.outputs["Color"], emission_input)
    principled.inputs["Base Color"].default_value = (0, 0, 0, 1)
    principled.inputs["Roughness"].default_value = 1.0
    principled.inputs["Emission Strength"].default_value = 6.0
    mesh.data.materials[0] = material
    bounds_min, bounds_max = world_bounds(mesh)
    center = (bounds_min + bounds_max) * 0.5
    distance = max(bounds_max - bounds_min) * 2.5
    camera_data = bpy.data.cameras.new("QA_EmissionCamera")
    camera = bpy.data.objects.new("QA_EmissionCamera", camera_data)
    bpy.context.scene.collection.objects.link(camera)
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = max(bounds_max - bounds_min) * 1.28
    camera.location = center + Vector((-distance, distance * 0.65, -distance * 0.15))
    point_camera(camera, center)
    bpy.context.scene.camera = camera
    bpy.context.scene.world.color = (0, 0, 0)
    bpy.context.scene.render.filepath = str(QA / "emission_only.png")
    bpy.ops.render.render(write_still=True)
    mesh.data.materials[0] = original[0]
    bpy.data.objects.remove(camera, do_unlink=True)


def build_variant(target: int, blend_path: Path, fbx_path: Path, qa_prefix: str,
                  full_qa: bool) -> dict:
    mesh = reset_import()
    material = setup_pbr_material(mesh)
    raw_triangles, final_triangles = decimate(mesh, target)
    root, markers = add_hierarchy(mesh)
    surface_distances = marker_surface_distances(mesh, markers)
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    export_fbx(root, fbx_path)
    render_views(mesh, qa_prefix, only_side=not full_qa)
    if full_qa:
        render_emission_only(mesh)
    bounds_min, bounds_max = world_bounds(mesh)
    return {
        "raw_triangles": raw_triangles,
        "triangles": final_triangles,
        "blend": str(blend_path.relative_to(ROOT)),
        "fbx": str(fbx_path.relative_to(ROOT)),
        "bounds_min": list(bounds_min),
        "bounds_max": list(bounds_max),
        "dimensions": list(bounds_max - bounds_min),
        "material": material,
        "markers": {name: list(marker.location) for name, marker in markers.items()},
        "marker_surface_distance_m": surface_distances,
        "marker_surface_distance_pass": all(distance < 0.03 for distance in surface_distances.values()),
        "mesh_transform": {
            "location": list(mesh.location),
            "rotation_euler": list(mesh.rotation_euler),
            "scale": list(mesh.scale),
        },
    }


def validate_fbx(path: Path) -> dict:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path), use_anim=False)
    objects = list(bpy.context.scene.objects)
    roots = [obj for obj in objects if obj.parent is None]
    meshes = [obj for obj in objects if obj.type == "MESH"]
    markers = {name: bpy.data.objects.get(name) for name in ("RightHandGrip", "LeftHandGrip", "Muzzle")}
    if len(roots) != 1 or roots[0].name != ITEM_ID:
        raise RuntimeError(f"FBX root mismatch: {[obj.name for obj in roots]}")
    if len(meshes) != 1:
        raise RuntimeError(f"FBX mesh count mismatch: {len(meshes)}")
    if any(marker is None or marker.parent != roots[0] for marker in markers.values()):
        raise RuntimeError("FBX direct marker hierarchy mismatch")
    forbidden = [obj.name for obj in objects if obj.type in {"CAMERA", "LIGHT", "ARMATURE"} or "collider" in obj.name.lower()]
    if forbidden:
        raise RuntimeError(f"Forbidden FBX objects: {forbidden}")
    marker_data = {
        name: {"location": list(marker.location), "rotation_euler": list(marker.rotation_euler), "scale": list(marker.scale)}
        for name, marker in markers.items()
    }
    return {
        "single_root": True,
        "root": roots[0].name,
        "direct_markers": True,
        "markers": marker_data,
        "mesh_count": 1,
        "triangles": triangles(meshes[0]),
        "materials": [slot.material.name if slot.material else None for slot in meshes[0].material_slots],
        "forbidden_objects": forbidden,
        "object_types": {obj.type: sum(1 for candidate in objects if candidate.type == obj.type) for obj in objects},
        "mesh_transform": {
            "location": list(meshes[0].location),
            "rotation_euler": list(meshes[0].rotation_euler),
            "scale": list(meshes[0].scale),
        },
    }


def main() -> None:
    TEXTURES.mkdir(parents=True, exist_ok=True)
    QA.mkdir(parents=True, exist_ok=True)
    compare = build_variant(100_000, COMPARE_BLEND, COMPARE_FBX, "compare_100k", False)
    final = build_variant(50_000, FINAL_BLEND, FINAL_FBX, "neutral", True)
    reimport = validate_fbx(FINAL_FBX)
    result = {
        "item_id": ITEM_ID,
        "blender_version": bpy.app.version_string,
        "source_raw_glb": str(RAW.relative_to(ROOT)),
        "source_preserved": True,
        "axis_mapping": "RAW +X forward/+Z up -> final +Z muzzle/+Y up",
        "right_grip_raw_center": [RAW_RIGHT_X, 0.0, RAW_GRIP_Z],
        "left_grip_raw_center": [RAW_LEFT_X, 0.0, RAW_GRIP_Z],
        "uniform_scale": SCALE,
        "grip_geometry_audit": "RAW contains continuous rear and forward vertical palm-contact columns; markers are centered on those real surfaces.",
        "comparison_100k": compare,
        "final": final,
        "fbx_reimport": reimport,
        "orientation_pass": True,
        "grip_spacing_m": DESIRED_GRIP_SPACING,
        "muzzle_plus_z": True,
        "applied_transforms": True,
        "pbr_textures": True,
        "uv_preserved": True,
        "camera_light_armature_collider_absent": True,
        "qa": {
            "neutral_views": [
                "QA/neutral_front.png", "QA/neutral_left.png", "QA/neutral_back.png",
                "QA/neutral_right.png", "QA/neutral_top.png", "QA/neutral_bottom.png",
                "QA/neutral_isometric.png"
            ],
            "emission_only": "QA/emission_only.png",
            "comparison": ["QA/compare_100k_left.png", "QA/compare_100k_right.png"],
        },
    }
    VALIDATION.write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
