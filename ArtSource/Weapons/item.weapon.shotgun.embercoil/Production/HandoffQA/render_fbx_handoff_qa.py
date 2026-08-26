from __future__ import annotations

import hashlib
import json
import math
import os
from collections import defaultdict
from pathlib import Path

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree


ITEM_ID = "item.weapon.shotgun.embercoil"
QA_ROOT = Path(__file__).resolve().parent
PRODUCTION_ROOT = QA_ROOT.parent
FBX_PATH = PRODUCTION_ROOT / f"{ITEM_ID}.fbx"
BUILD_REPORT_PATH = PRODUCTION_ROOT / "QA" / "production_build.json"
TEXTURE_DIR = PRODUCTION_ROOT / "Textures"
BASE_COLOR_PATH = TEXTURE_DIR / f"{ITEM_ID}_BaseColor.png"
NORMAL_PATH = TEXTURE_DIR / f"{ITEM_ID}_Normal.png"
ORM_PATH = TEXTURE_DIR / f"{ITEM_ID}_MetallicRoughness.png"
EMISSION_PATH = TEXTURE_DIR / f"{ITEM_ID}_Emission.png"
REPORT_PATH = QA_ROOT / "fbx_geometry_normals_validation.json"
RENDER_ROOT = QA_ROOT / "Renders"

DESIGNATED_LINEAR = (1.0, 0.06847816984440017, 0.010329823029626936, 1.0)
EMISSION_STRENGTH = 6.0
RENDER_SIZE = 768


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def vector_list(value: Vector) -> list[float]:
    return [float(component) for component in value]


def load_image(path: Path, colorspace: str) -> bpy.types.Image:
    image = bpy.data.images.load(os.fspath(path), check_existing=True)
    image.name = path.stem
    image.colorspace_settings.name = colorspace
    return image


def clear_materials() -> None:
    for material in list(bpy.data.materials):
        bpy.data.materials.remove(material)


def build_pbr_material() -> bpy.types.Material:
    material = bpy.data.materials.new("QA_EmberCoil_PBR_Emission")
    material.use_nodes = True
    material.use_backface_culling = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()

    output = nodes.new("ShaderNodeOutputMaterial")
    principled = nodes.new("ShaderNodeBsdfPrincipled")
    links.new(principled.outputs["BSDF"], output.inputs["Surface"])

    base = nodes.new("ShaderNodeTexImage")
    base.image = load_image(BASE_COLOR_PATH, "sRGB")
    links.new(base.outputs["Color"], principled.inputs["Base Color"])

    orm = nodes.new("ShaderNodeTexImage")
    orm.image = load_image(ORM_PATH, "Non-Color")
    separate = nodes.new("ShaderNodeSeparateColor")
    links.new(orm.outputs["Color"], separate.inputs["Color"])
    links.new(separate.outputs["Green"], principled.inputs["Roughness"])
    links.new(separate.outputs["Blue"], principled.inputs["Metallic"])

    normal_image = nodes.new("ShaderNodeTexImage")
    normal_image.image = load_image(NORMAL_PATH, "Non-Color")
    normal_map = nodes.new("ShaderNodeNormalMap")
    normal_map.space = "TANGENT"
    links.new(normal_image.outputs["Color"], normal_map.inputs["Color"])
    links.new(normal_map.outputs["Normal"], principled.inputs["Normal"])

    emission = nodes.new("ShaderNodeTexImage")
    emission.image = load_image(EMISSION_PATH, "Non-Color")
    emission.interpolation = "Closest"
    strength = nodes.new("ShaderNodeMath")
    strength.operation = "MULTIPLY"
    strength.inputs[1].default_value = EMISSION_STRENGTH
    links.new(emission.outputs["Color"], strength.inputs[0])
    links.new(strength.outputs[0], principled.inputs["Emission Strength"])
    principled.inputs["Emission Color"].default_value = DESIGNATED_LINEAR
    return material


def build_emission_only_material() -> bpy.types.Material:
    material = bpy.data.materials.new("QA_EmissionOnly_GlobalBinary")
    material.use_nodes = True
    material.use_backface_culling = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()

    output = nodes.new("ShaderNodeOutputMaterial")
    emission_shader = nodes.new("ShaderNodeEmission")
    emission_shader.inputs["Color"].default_value = DESIGNATED_LINEAR
    emission_shader.inputs["Strength"].default_value = 2.0
    transparent = nodes.new("ShaderNodeBsdfTransparent")
    mix = nodes.new("ShaderNodeMixShader")
    mask = nodes.new("ShaderNodeTexImage")
    mask.image = load_image(EMISSION_PATH, "Non-Color")
    mask.interpolation = "Closest"
    links.new(mask.outputs["Color"], mix.inputs[0])
    links.new(transparent.outputs["BSDF"], mix.inputs[1])
    links.new(emission_shader.outputs["Emission"], mix.inputs[2])
    links.new(mix.outputs["Shader"], output.inputs["Surface"])
    return material


def build_normal_material() -> bpy.types.Material:
    material = bpy.data.materials.new("QA_WorldNormal_BackfaceCulled")
    material.use_nodes = True
    material.use_backface_culling = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()

    output = nodes.new("ShaderNodeOutputMaterial")
    geometry = nodes.new("ShaderNodeNewGeometry")
    scale = nodes.new("ShaderNodeVectorMath")
    scale.operation = "SCALE"
    scale.inputs[3].default_value = 0.5
    add = nodes.new("ShaderNodeVectorMath")
    add.operation = "ADD"
    add.inputs[1].default_value = (0.5, 0.5, 0.5)
    emission_shader = nodes.new("ShaderNodeEmission")
    emission_shader.inputs["Strength"].default_value = 1.0
    links.new(geometry.outputs["Normal"], scale.inputs[0])
    links.new(scale.outputs["Vector"], add.inputs[0])
    links.new(add.outputs["Vector"], emission_shader.inputs["Color"])
    links.new(emission_shader.outputs["Emission"], output.inputs["Surface"])
    return material


def build_flat_material(cull: bool) -> bpy.types.Material:
    material = bpy.data.materials.new(f"QA_Flat_Culling_{'On' if cull else 'Off'}")
    material.use_nodes = True
    material.use_backface_culling = cull
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    emission_shader = nodes.new("ShaderNodeEmission")
    emission_shader.inputs["Color"].default_value = (1.0, 1.0, 1.0, 1.0)
    emission_shader.inputs["Strength"].default_value = 1.0
    links.new(emission_shader.outputs["Emission"], output.inputs["Surface"])
    return material


def assign_material(model: bpy.types.Object, material: bpy.types.Material) -> None:
    model.data.materials.clear()
    model.data.materials.append(material)


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def fit_orthographic(
    camera: bpy.types.Object,
    target: Vector,
    direction: Vector,
    points: list[Vector],
    margin: float = 1.16,
) -> None:
    direction = direction.normalized()
    camera.location = target + direction * 3.5
    look_at(camera, target)
    bpy.context.view_layer.update()
    rotation = camera.matrix_world.to_quaternion()
    right = rotation @ Vector((1.0, 0.0, 0.0))
    up = rotation @ Vector((0.0, 1.0, 0.0))
    half_width = max(abs((point - target).dot(right)) for point in points)
    half_height = max(abs((point - target).dot(up)) for point in points)
    camera.data.ortho_scale = max(2.0 * half_height, 2.0 * half_width) * margin


def render_views(
    scene: bpy.types.Scene,
    camera: bpy.types.Object,
    model: bpy.types.Object,
    material: bpy.types.Material,
    output_dir: Path,
    views: dict[str, Vector],
    center: Vector,
    points: list[Vector],
    transparent: bool,
) -> list[str]:
    assign_material(model, material)
    scene.render.film_transparent = transparent
    output_dir.mkdir(parents=True, exist_ok=True)
    results = []
    for name, direction in views.items():
        fit_orthographic(camera, center, direction, points)
        output_path = output_dir / f"{name}.png"
        scene.render.filepath = os.fspath(output_path)
        bpy.ops.render.render(write_still=True)
        results.append(str(output_path.resolve()))
    return results


for required in (
    FBX_PATH,
    BUILD_REPORT_PATH,
    BASE_COLOR_PATH,
    NORMAL_PATH,
    ORM_PATH,
    EMISSION_PATH,
):
    if not required.is_file():
        raise FileNotFoundError(required)

build_report = json.loads(BUILD_REPORT_PATH.read_text(encoding="utf-8"))
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=os.fspath(FBX_PATH), use_anim=False)
bpy.context.view_layer.update()

objects_before_qa = list(bpy.context.scene.objects)
top_level = [obj for obj in objects_before_qa if obj.parent is None]
meshes = [obj for obj in objects_before_qa if obj.type == "MESH"]
if len(top_level) != 1 or len(meshes) != 1:
    raise RuntimeError(f"Unexpected FBX hierarchy: top={len(top_level)} meshes={len(meshes)}")
asset_root = top_level[0]
model = meshes[0]
anchors = {name: bpy.data.objects.get(name) for name in ("RightHandGrip", "LeftHandGrip", "Muzzle")}
if any(anchor is None for anchor in anchors.values()):
    raise RuntimeError(f"Missing FBX anchors: {anchors}")

points = [model.matrix_world @ Vector(corner) for corner in model.bound_box]
minimum = Vector(tuple(min(point[index] for point in points) for index in range(3)))
maximum = Vector(tuple(max(point[index] for point in points) for index in range(3)))
dimensions = maximum - minimum
center = (minimum + maximum) * 0.5

mesh = model.data
mesh.calc_loop_triangles()
triangles = len(mesh.loop_triangles)
vertices = len(mesh.vertices)

edge_faces: dict[tuple[int, int], list[int]] = defaultdict(list)
for polygon in mesh.polygons:
    polygon_vertices = list(polygon.vertices)
    for index, start in enumerate(polygon_vertices):
        end = polygon_vertices[(index + 1) % len(polygon_vertices)]
        key = (min(start, end), max(start, end))
        direction = 1 if (start, end) == key else -1
        edge_faces[key].append(direction)

boundary_edges = sum(len(directions) == 1 for directions in edge_faces.values())
manifold_edges = sum(len(directions) == 2 for directions in edge_faces.values())
nonmanifold_edges = sum(len(directions) > 2 for directions in edge_faces.values())
shared_edge_winding_conflicts = sum(
    len(directions) == 2 and directions[0] == directions[1]
    for directions in edge_faces.values()
)
zero_area_faces = sum(polygon.area <= 1e-14 for polygon in mesh.polygons)
invalid_normal_faces = sum(
    (not all(math.isfinite(value) for value in polygon.normal)) or polygon.normal.length <= 1e-8
    for polygon in mesh.polygons
)

right_world = anchors["RightHandGrip"].matrix_world.translation
left_world = anchors["LeftHandGrip"].matrix_world.translation
muzzle_world = anchors["Muzzle"].matrix_world.translation
muzzle_forward = anchors["Muzzle"].matrix_world.to_quaternion() @ Vector((0.0, 0.0, 1.0))
top_forward = asset_root.matrix_world.to_quaternion() @ Vector((0.0, 1.0, 0.0))

bvh = BVHTree.FromObject(model, bpy.context.evaluated_depsgraph_get())
ray_origin = muzzle_world + muzzle_forward * 0.02
hit_location, hit_normal, hit_face, hit_distance = bvh.ray_cast(ray_origin, -muzzle_forward, 1.2)
muzzle_depth = None if hit_location is None else float(hit_distance - 0.02)

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = RENDER_SIZE
scene.render.resolution_y = RENDER_SIZE
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.image_settings.color_depth = "8"
scene.render.film_transparent = False
scene.render.image_settings.color_mode = "RGBA"
scene.view_settings.look = "AgX - Medium High Contrast"
scene.world.color = (0.008, 0.010, 0.016)

camera_data = bpy.data.cameras.new("HandoffQACamera")
camera_data.type = "ORTHO"
camera = bpy.data.objects.new("HandoffQACamera", camera_data)
scene.collection.objects.link(camera)
scene.camera = camera

maximum_dimension = max(dimensions)
for name, offset, energy, size, color in (
    ("Handoff_Key", (1.6, 1.8, 1.4), 220.0, 1.2, (1.0, 0.78, 0.58)),
    ("Handoff_Fill", (-1.5, 0.8, 0.2), 100.0, 1.8, (0.40, 0.56, 1.0)),
    ("Handoff_Rim", (0.2, 1.3, -1.5), 150.0, 1.0, (0.68, 0.82, 1.0)),
):
    light_data = bpy.data.lights.new(name=name, type="AREA")
    light_data.energy = energy
    light_data.shape = "DISK"
    light_data.size = size
    light_object = bpy.data.objects.new(name, light_data)
    scene.collection.objects.link(light_object)
    light_object.location = center + Vector(offset) * maximum_dimension
    look_at(light_object, center)

views = {
    "left_x_pos": Vector((1.0, 0.0, 0.0)),
    "right_x_neg": Vector((-1.0, 0.0, 0.0)),
    "top_y_pos": Vector((0.0, 1.0, 0.0)),
    "muzzle_z_pos": Vector((0.0, 0.0, 1.0)),
    "iso_muzzle": Vector((1.0, 0.8, 1.0)),
}

clear_materials()
pbr_paths = render_views(
    scene, camera, model, build_pbr_material(), RENDER_ROOT / "PBR_Emission", views, center, points, False
)
emission_paths = render_views(
    scene, camera, model, build_emission_only_material(), RENDER_ROOT / "EmissionOnly", views, center, points, True
)
normal_paths = render_views(
    scene, camera, model, build_normal_material(), RENDER_ROOT / "NormalsBackfaceCulled", views, center, points, False
)
culling_off_paths = render_views(
    scene, camera, model, build_flat_material(False), RENDER_ROOT / "CullingOff", views, center, points, True
)
culling_on_paths = render_views(
    scene, camera, model, build_flat_material(True), RENDER_ROOT / "CullingOn", views, center, points, True
)

expected_dimensions = Vector(build_report["dimensions_m"])
dimension_delta = dimensions - expected_dimensions
required_children = {"Model", "RightHandGrip", "LeftHandGrip", "Muzzle"}
root_children = {child.name for child in asset_root.children}
asset_objects = [asset_root, model, *anchors.values()]
unit_scales = all(
    all(abs(component - 1.0) <= 1e-5 for component in obj.scale)
    for obj in asset_objects
)
positive_uniform_world_scales = all(
    min(obj.matrix_world.to_scale()) > 0.0
    and max(obj.matrix_world.to_scale()) - min(obj.matrix_world.to_scale()) <= 1e-5
    for obj in asset_objects
)

checks = {
    "one_top_level_root_before_qa": len(top_level) == 1,
    "root_name_matches_item_id": asset_root.name == ITEM_ID,
    "required_direct_children": required_children.issubset(root_children),
    "one_mesh": len(meshes) == 1,
    "triangles_match_production": triangles == build_report["final_triangles"],
    "bounds_match_production_within_0.1mm": max(abs(value) for value in dimension_delta) <= 0.0001,
    "unit_local_scales": unit_scales,
    "positive_uniform_world_scales": positive_uniform_world_scales,
    "right_hand_grip_at_origin": right_world.length <= 1e-5,
    "left_hand_forward_distance_0.326m": abs((left_world - right_world).z - 0.326) <= 1e-4,
    "muzzle_anchor_at_forward_bound": muzzle_world.z >= maximum.z - 0.001,
    "muzzle_axis_plus_z": muzzle_forward.dot(Vector((0.0, 0.0, 1.0))) >= 0.9999,
    "top_axis_plus_y": top_forward.dot(Vector((0.0, 1.0, 0.0))) >= 0.9999,
    "muzzle_center_probe_open_at_least_0.045m": muzzle_depth is not None and muzzle_depth >= 0.045,
    "no_camera_light_armature_in_fbx": not any(
        obj.type in {"CAMERA", "LIGHT", "ARMATURE"} for obj in objects_before_qa
    ),
    "no_zero_area_faces": zero_area_faces == 0,
    "all_face_normals_finite_nonzero": invalid_normal_faces == 0,
    "shared_manifold_edge_winding_consistent": shared_edge_winding_conflicts == 0,
    "no_edges_with_more_than_two_faces": nonmanifold_edges == 0,
}

report = {
    "item_id": ITEM_ID,
    "blender_version": bpy.app.version_string,
    "fbx_path": str(FBX_PATH.resolve()),
    "fbx_sha256": sha256(FBX_PATH),
    "hierarchy": {
        "top_level": [obj.name for obj in top_level],
        "root_children": sorted(root_children),
        "object_types_before_qa": {obj.name: obj.type for obj in objects_before_qa},
        "object_parents_before_qa": {
            obj.name: obj.parent.name if obj.parent else None for obj in objects_before_qa
        },
    },
    "geometry": {
        "triangles": triangles,
        "vertices": vertices,
        "uv_layers": [layer.name for layer in mesh.uv_layers],
        "bounds_min_m": vector_list(minimum),
        "bounds_max_m": vector_list(maximum),
        "dimensions_m": vector_list(dimensions),
        "dimension_delta_from_production_m": vector_list(dimension_delta),
        "boundary_edges_single_face": boundary_edges,
        "watertight": boundary_edges == 0,
        "manifold_edges": manifold_edges,
        "nonmanifold_edges_more_than_two_faces": nonmanifold_edges,
        "shared_edge_winding_conflicts": shared_edge_winding_conflicts,
        "zero_area_faces": zero_area_faces,
        "invalid_normal_faces": invalid_normal_faces,
    },
    "anchors_world": {
        "RightHandGrip": vector_list(right_world),
        "LeftHandGrip": vector_list(left_world),
        "Muzzle": vector_list(muzzle_world),
        "Muzzle_forward": vector_list(muzzle_forward),
        "Top_forward": vector_list(top_forward),
    },
    "muzzle_probe": {
        "origin": vector_list(ray_origin),
        "direction": vector_list(-muzzle_forward),
        "first_hit": vector_list(hit_location) if hit_location else None,
        "first_hit_normal": vector_list(hit_normal) if hit_normal else None,
        "face_index": hit_face,
        "depth_behind_muzzle_plane_m": muzzle_depth,
        "open_threshold_m": 0.045,
    },
    "render_contract": {
        "size": [RENDER_SIZE, RENDER_SIZE],
        "views": list(views),
        "pbr_emission_backface_culling": True,
        "emission_only_uses_exact_global_binary_mask": True,
        "normal_view": "world normal remapped from [-1,1] to [0,1], backface culling enabled",
        "culling_comparison": "matched flat unlit RGBA renders with culling off/on",
    },
    "topology_note": (
        "The Tripo-derived realtime mesh is not watertight and retains single-face boundary edges. "
        "No edge has more than two incident faces, shared-edge winding is consistent, and matched "
        "five-view culling renders are the handoff visibility gate."
    ),
    "rendered_views": {
        "PBR_Emission": pbr_paths,
        "EmissionOnly": emission_paths,
        "NormalsBackfaceCulled": normal_paths,
        "CullingOff": culling_off_paths,
        "CullingOn": culling_on_paths,
    },
    "checks": checks,
    "all_geometry_checks_passed": all(checks.values()),
}
REPORT_PATH.write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps(report, indent=2))
if not report["all_geometry_checks_passed"]:
    raise RuntimeError("FBX geometry/normals validation failed; inspect report")
