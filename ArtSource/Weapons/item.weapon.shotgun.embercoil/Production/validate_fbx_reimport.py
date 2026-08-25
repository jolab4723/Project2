from __future__ import annotations

import hashlib
import json
import math
import os
from pathlib import Path

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree


ITEM_ID = "item.weapon.shotgun.embercoil"
ROOT = Path(__file__).resolve().parent
FBX_PATH = ROOT / f"{ITEM_ID}.fbx"
BUILD_REPORT = ROOT / "QA" / "production_build.json"
REIMPORT_DIR = ROOT / "QA" / "Reimport"
REIMPORT_BLEND = REIMPORT_DIR / f"{ITEM_ID}_reimport.blend"
REPORT_PATH = REIMPORT_DIR / "fbx_reimport_validation.json"
RENDER_DIR = REIMPORT_DIR / "Renders"
TEXTURES = {
    "BaseColor_sRGB": ROOT / "Textures" / f"{ITEM_ID}_BaseColor.png",
    "NormalGL_NonColor": ROOT / "Textures" / f"{ITEM_ID}_Normal.png",
    "ORM_NonColor": ROOT / "Textures" / f"{ITEM_ID}_MetallicRoughness.png",
    "Emission_GlobalBinary_NonColor": ROOT / "Textures" / f"{ITEM_ID}_Emission.png",
}


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def triangle_count(obj: bpy.types.Object) -> int:
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def vector_list(vector: Vector) -> list[float]:
    return [float(value) for value in vector]


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def fit_orthographic(
    camera: bpy.types.Object,
    target: Vector,
    direction: Vector,
    points: list[Vector],
    margin: float = 1.18,
) -> None:
    direction.normalize()
    camera.location = target + direction * 3.5
    look_at(camera, target)
    bpy.context.view_layer.update()
    right = camera.matrix_world.to_quaternion() @ Vector((1.0, 0.0, 0.0))
    up = camera.matrix_world.to_quaternion() @ Vector((0.0, 1.0, 0.0))
    half_width = max(abs((point - target).dot(right)) for point in points)
    half_height = max(abs((point - target).dot(up)) for point in points)
    camera.data.ortho_scale = max(half_height * 2.0, half_width * 2.0) * margin


if not FBX_PATH.is_file():
    raise FileNotFoundError(FBX_PATH)
build_report = json.loads(BUILD_REPORT.read_text(encoding="utf-8"))

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=os.fspath(FBX_PATH), use_anim=False)
bpy.context.view_layer.update()

objects = list(bpy.context.scene.objects)
top_level = [obj for obj in objects if obj.parent is None]
meshes = [obj for obj in objects if obj.type == "MESH"]
empties = [obj for obj in objects if obj.type == "EMPTY"]
cameras = [obj for obj in objects if obj.type == "CAMERA"]
lights = [obj for obj in objects if obj.type == "LIGHT"]
armatures = [obj for obj in objects if obj.type == "ARMATURE"]

if len(top_level) != 1:
    raise RuntimeError(f"Expected one top-level root after FBX import, found {len(top_level)}")
asset_root = top_level[0]
if len(meshes) != 1:
    raise RuntimeError(f"Expected one mesh after FBX import, found {len(meshes)}")
model = meshes[0]

anchors = {}
for name in ("RightHandGrip", "LeftHandGrip", "Muzzle"):
    obj = bpy.data.objects.get(name)
    if obj is None:
        raise RuntimeError(f"Missing FBX anchor: {name}")
    anchors[name] = obj

world_corners = [model.matrix_world @ Vector(corner) for corner in model.bound_box]
minimum = Vector((min(p.x for p in world_corners), min(p.y for p in world_corners), min(p.z for p in world_corners)))
maximum = Vector((max(p.x for p in world_corners), max(p.y for p in world_corners), max(p.z for p in world_corners)))
dimensions = maximum - minimum
triangles = triangle_count(model)

all_scales = {
    obj.name: vector_list(obj.scale)
    for obj in (asset_root, model, *anchors.values())
}
unit_positive_scales = all(
    all(abs(component - 1.0) <= 1e-5 for component in obj.scale)
    for obj in (asset_root, model, *anchors.values())
)
no_negative_or_nonuniform_world_scales = all(
    min(obj.matrix_world.to_scale()) > 0.0
    and max(obj.matrix_world.to_scale()) - min(obj.matrix_world.to_scale()) <= 1e-5
    for obj in (asset_root, model, *anchors.values())
)

right_world = anchors["RightHandGrip"].matrix_world.translation
left_world = anchors["LeftHandGrip"].matrix_world.translation
muzzle_world = anchors["Muzzle"].matrix_world.translation
muzzle_forward = anchors["Muzzle"].matrix_world.to_quaternion() @ Vector((0.0, 0.0, 1.0))
top_axis = asset_root.matrix_world.to_quaternion() @ Vector((0.0, 1.0, 0.0))

depsgraph = bpy.context.evaluated_depsgraph_get()
bvh = BVHTree.FromObject(model, depsgraph)
ray_origin = muzzle_world + muzzle_forward * 0.02
location, normal, face_index, ray_distance = bvh.ray_cast(
    ray_origin, -muzzle_forward, 1.2
)
muzzle_depth = None if location is None else float(ray_distance - 0.02)
muzzle_open = muzzle_depth is not None and muzzle_depth >= 0.045

material_report = []
for material in model.data.materials:
    node_images = []
    if material and material.use_nodes and material.node_tree:
        for node in material.node_tree.nodes:
            if node.type == "TEX_IMAGE" and node.image:
                node_images.append(
                    {
                        "node": node.name,
                        "image": node.image.name,
                        "filepath": node.image.filepath,
                        "resolved_filepath": bpy.path.abspath(node.image.filepath),
                        "colorspace": node.image.colorspace_settings.name,
                    }
                )
    material_report.append(
        {
            "name": material.name if material else None,
            "use_nodes": bool(material and material.use_nodes),
            "node_images": node_images,
        }
    )

REIMPORT_DIR.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=os.fspath(REIMPORT_BLEND), check_existing=False)

# Add QA-only render objects after saving so the preserved reimport blend remains
# a clean asset-only scene.
RENDER_DIR.mkdir(parents=True, exist_ok=True)
scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1024
scene.render.resolution_y = 1024
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.film_transparent = False
scene.view_settings.look = "AgX - Medium High Contrast"
scene.world.color = (0.012, 0.014, 0.020)

center = (minimum + maximum) * 0.5
maximum_dimension = max(dimensions)
camera_data = bpy.data.cameras.new("ReimportQACamera")
camera_data.type = "ORTHO"
camera = bpy.data.objects.new("ReimportQACamera", camera_data)
scene.collection.objects.link(camera)
scene.camera = camera

for name, location_values, energy, size, color in (
    ("ReimportQA_Key", (1.6, 1.8, 1.4), 180.0, 1.2, (1.0, 0.80, 0.63)),
    ("ReimportQA_Fill", (-1.5, 0.7, 0.2), 95.0, 1.6, (0.42, 0.58, 1.0)),
    ("ReimportQA_Rim", (0.3, 1.2, -1.4), 130.0, 1.0, (0.72, 0.84, 1.0)),
):
    data = bpy.data.lights.new(name=name, type="AREA")
    data.energy = energy
    data.shape = "DISK"
    data.size = size
    data.color = color
    light = bpy.data.objects.new(name, data)
    scene.collection.objects.link(light)
    light.location = center + Vector(location_values) * maximum_dimension
    look_at(light, center)

render_views = {
    "side_x_pos": Vector((1.0, 0.0, 0.0)),
    "side_x_neg": Vector((-1.0, 0.0, 0.0)),
    "muzzle_z_pos": Vector((0.0, 0.0, 1.0)),
    "iso_muzzle": Vector((1.0, 0.8, 1.0)),
}
for name, direction in render_views.items():
    fit_orthographic(camera, center, direction, world_corners)
    scene.render.filepath = os.fspath(RENDER_DIR / f"{name}.png")
    bpy.ops.render.render(write_still=True)

expected_dimensions = Vector(build_report["dimensions_m"])
dimension_delta = dimensions - expected_dimensions
required_children = {"Model", "RightHandGrip", "LeftHandGrip", "Muzzle"}
root_children = {child.name for child in asset_root.children}
texture_report = {
    role: {
        "path": str(path.resolve()),
        "exists": path.is_file(),
        "sha256": sha256(path) if path.is_file() else None,
    }
    for role, path in TEXTURES.items()
}

checks = {
    "one_top_level_root": len(top_level) == 1,
    "root_name_matches_item_id": asset_root.name == ITEM_ID,
    "root_has_required_direct_children": required_children.issubset(root_children),
    "one_mesh": len(meshes) == 1,
    "triangles_match": triangles == build_report["final_triangles"],
    "dimensions_match_within_0.1mm": max(abs(value) for value in dimension_delta) <= 0.0001,
    "unit_local_scales": unit_positive_scales,
    "no_negative_or_nonuniform_world_scales": no_negative_or_nonuniform_world_scales,
    "right_hand_grip_at_origin": right_world.length <= 1e-5,
    "left_hand_forward_distance_matches": abs((left_world - right_world).z - 0.326) <= 1e-4,
    "muzzle_is_forward": muzzle_world.z > maximum.z - 0.001,
    "muzzle_axis_is_plus_z": muzzle_forward.dot(Vector((0.0, 0.0, 1.0))) >= 0.9999,
    "top_axis_is_plus_y": top_axis.dot(Vector((0.0, 1.0, 0.0))) >= 0.9999,
    "muzzle_open_center_probe": muzzle_open,
    "no_camera_light_armature_in_preserved_reimport": not cameras and not lights and not armatures,
    "all_external_pbr_textures_exist": all(path.is_file() for path in TEXTURES.values()),
    "single_material_slot_non_null": len(model.data.materials) == 1 and model.data.materials[0] is not None,
}

report = {
    "item_id": ITEM_ID,
    "fbx_path": str(FBX_PATH.resolve()),
    "fbx_sha256": sha256(FBX_PATH),
    "reimport_blend_path": str(REIMPORT_BLEND.resolve()),
    "blender_version": bpy.app.version_string,
    "top_level_objects": [obj.name for obj in top_level],
    "root_children": sorted(root_children),
    "object_types": {obj.name: obj.type for obj in objects},
    "object_parents": {obj.name: obj.parent.name if obj.parent else None for obj in objects},
    "local_scales": all_scales,
    "triangles": triangles,
    "vertices": len(model.data.vertices),
    "uv_layers": [layer.name for layer in model.data.uv_layers],
    "bounds_min_m": vector_list(minimum),
    "bounds_max_m": vector_list(maximum),
    "dimensions_m": vector_list(dimensions),
    "expected_dimensions_m": vector_list(expected_dimensions),
    "dimension_delta_m": vector_list(dimension_delta),
    "anchors_world": {
        "RightHandGrip": vector_list(right_world),
        "LeftHandGrip": vector_list(left_world),
        "Muzzle": vector_list(muzzle_world),
        "Muzzle_forward": vector_list(muzzle_forward),
    },
    "muzzle_probe": {
        "origin": vector_list(ray_origin),
        "direction": vector_list(-muzzle_forward),
        "first_hit": vector_list(location) if location else None,
        "first_hit_normal": vector_list(normal) if normal else None,
        "face_index": face_index,
        "depth_behind_muzzle_plane_m": muzzle_depth,
        "open_pass_threshold_m": 0.045,
    },
    "materials": material_report,
    "external_pbr_textures": texture_report,
    "rendered_views": [str((RENDER_DIR / f"{name}.png").resolve()) for name in render_views],
    "checks": checks,
    "all_checks_passed": all(checks.values()),
}
REPORT_PATH.write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps(report, indent=2))
if not report["all_checks_passed"]:
    raise RuntimeError("FBX reimport validation failed; inspect report checks")
