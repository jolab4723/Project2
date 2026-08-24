import hashlib
import json
from pathlib import Path

import bpy
import numpy as np
from mathutils import Matrix, Vector


ITEM_ID = "item.weapon.shotgun.thermobarrel"
ITEM_DIR = Path(__file__).resolve().parents[1]
RAW_GLB = ITEM_DIR / "Tripo" / "Downloaded" / f"{ITEM_ID}_raw.glb"
PRODUCTION_DIR = ITEM_DIR / "Production"
TEXTURE_DIR = PRODUCTION_DIR / "Textures"
QA_DIR = PRODUCTION_DIR / "QA"
BLEND_PATH = PRODUCTION_DIR / f"{ITEM_ID}.blend"
FBX_PATH = PRODUCTION_DIR / f"{ITEM_ID}.fbx"
VALIDATION_PATH = PRODUCTION_DIR / "blender_validation.json"

BASE_COLOR_PATH = TEXTURE_DIR / f"{ITEM_ID}_BaseColor.png"
NORMAL_PATH = TEXTURE_DIR / f"{ITEM_ID}_NormalGL.png"
ORM_PATH = TEXTURE_DIR / f"{ITEM_ID}_ORM.png"
EMISSION_PATH = TEXTURE_DIR / f"{ITEM_ID}_Emission.png"

RAW_RIGHT_HAND_CENTER = Vector((0.196956, -0.000647, -0.075454))
RAW_LEFT_HAND_CENTER = Vector((-0.145230, -0.000304, -0.037766))
RAW_MUZZLE_CENTER = Vector((-0.500000, -0.001279, 0.056657))
GRIP_SPACING = 0.32625
RAW_GRIP_SPACING = RAW_RIGHT_HAND_CENTER.x - RAW_LEFT_HAND_CENTER.x
MODEL_SCALE = GRIP_SPACING / RAW_GRIP_SPACING
TARGET_TRIANGLES = 60000


def transformed_point(point):
    local = point - RAW_RIGHT_HAND_CENTER
    return Vector((-local.y, local.z, -local.x)) * MODEL_SCALE


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def point_at(obj, target):
    forward = (Vector(target) - obj.location).normalized()
    world_up = Vector((0.0, 1.0, 0.0))
    right = forward.cross(world_up).normalized()
    corrected_up = right.cross(forward).normalized()
    rotation = Matrix((right, corrected_up, -forward)).transposed()
    obj.rotation_euler = rotation.to_euler()


def create_external_image(path, colorspace):
    image = bpy.data.images.load(str(path), check_existing=False)
    image.reload()
    _ = image.pixels[0]
    image.name = path.stem
    image.colorspace_settings.name = colorspace
    image.filepath = str(path)
    return image


def nearest_vertex_distance(mesh_obj, location):
    return min((vertex.co - location).length for vertex in mesh_obj.data.vertices)


def rasterize_uv_triangle(mask, uv_points):
    height, width = mask.shape
    points = np.asarray(
        [[uv.x * (width - 1), uv.y * (height - 1)] for uv in uv_points],
        dtype=np.float32,
    )
    min_x = max(0, int(np.floor(points[:, 0].min())))
    max_x = min(width - 1, int(np.ceil(points[:, 0].max())))
    min_y = max(0, int(np.floor(points[:, 1].min())))
    max_y = min(height - 1, int(np.ceil(points[:, 1].max())))
    if min_x > max_x or min_y > max_y:
        return
    x0, y0 = points[0]
    x1, y1 = points[1]
    x2, y2 = points[2]
    denominator = (y1 - y2) * (x0 - x2) + (x2 - x1) * (y0 - y2)
    if abs(float(denominator)) < 1e-8:
        return
    grid_y, grid_x = np.mgrid[min_y : max_y + 1, min_x : max_x + 1]
    weight_a = ((y1 - y2) * (grid_x - x2) + (x2 - x1) * (grid_y - y2)) / denominator
    weight_b = ((y2 - y0) * (grid_x - x2) + (x0 - x2) * (grid_y - y2)) / denominator
    weight_c = 1.0 - weight_a - weight_b
    inside = (weight_a >= -1e-4) & (weight_b >= -1e-4) & (weight_c >= -1e-4)
    mask[min_y : max_y + 1, min_x : max_x + 1] |= inside


def build_heat_surface_uv_mask(mesh_obj, width, height):
    mesh = mesh_obj.data
    uv_layer = mesh.uv_layers.active
    if uv_layer is None:
        raise RuntimeError("Emission region mask requires a UV layer")
    allowed = np.zeros((height, width), dtype=bool)
    allowed_faces = 0
    for polygon in mesh.polygons:
        center = polygon.center
        in_heat_rail = 0.10 <= center.y <= 0.125 and 0.14 <= center.z <= 0.50
        in_sealed_chamber = 0.055 <= center.y <= 0.165 and 0.045 <= center.z <= 0.19
        if not (in_heat_rail or in_sealed_chamber):
            continue
        loop_indices = list(polygon.loop_indices)
        if len(loop_indices) != 3:
            continue
        rasterize_uv_triangle(allowed, [uv_layer.data[index].uv for index in loop_indices])
        allowed_faces += 1
    return allowed, allowed_faces


PRODUCTION_DIR.mkdir(parents=True, exist_ok=True)
TEXTURE_DIR.mkdir(parents=True, exist_ok=True)
QA_DIR.mkdir(parents=True, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(RAW_GLB))
scene = bpy.context.scene
scene.unit_settings.system = "METRIC"
scene.unit_settings.length_unit = "METERS"
scene.unit_settings.scale_length = 1.0

mesh_objects = [obj for obj in scene.objects if obj.type == "MESH"]
if len(mesh_objects) != 1:
    raise RuntimeError(f"Expected one RAW mesh, found {len(mesh_objects)}")
mesh_obj = mesh_objects[0]
mesh_obj.name = f"{ITEM_ID}_Mesh"
mesh_obj.data.name = f"{ITEM_ID}_MeshData"

mesh_obj.data.calc_loop_triangles()
raw_triangles = len(mesh_obj.data.loop_triangles)
decimate = mesh_obj.modifiers.new(name="ProductionDecimate", type="DECIMATE")
decimate.decimate_type = "COLLAPSE"
decimate.ratio = min(1.0, TARGET_TRIANGLES / raw_triangles)
decimate.use_collapse_triangulate = True
bpy.context.view_layer.objects.active = mesh_obj
mesh_obj.select_set(True)
bpy.ops.object.modifier_apply(modifier=decimate.name)
mesh_obj.data.calc_loop_triangles()
final_triangles = len(mesh_obj.data.loop_triangles)

for vertex in mesh_obj.data.vertices:
    vertex.co = transformed_point(vertex.co)
for polygon in mesh_obj.data.polygons:
    polygon.use_smooth = True
mesh_obj.data.update()
mesh_obj.location = (0.0, 0.0, 0.0)
mesh_obj.rotation_euler = (0.0, 0.0, 0.0)
mesh_obj.scale = (1.0, 1.0, 1.0)

if not all(path.exists() for path in (BASE_COLOR_PATH, NORMAL_PATH, ORM_PATH)):
    raise RuntimeError("Extracted PBR textures are missing")

base_image = create_external_image(BASE_COLOR_PATH, "sRGB")
normal_image = create_external_image(NORMAL_PATH, "Non-Color")
orm_image = create_external_image(ORM_PATH, "Non-Color")

width, height = base_image.size
base_pixels = np.asarray(base_image.pixels[:], dtype=np.float32).reshape((height, width, 4))
red = base_pixels[:, :, 0]
green = base_pixels[:, :, 1]
blue = base_pixels[:, :, 2]
amber_mask = (
    (red >= 0.18)
    & ((red - blue) >= 0.16)
    & (blue <= red * 0.12 + 0.005)
    & (green >= red * 0.10)
    & (green <= red * 0.62)
)
heat_surface_uv_mask, emission_allowed_faces = build_heat_surface_uv_mask(mesh_obj, width, height)
amber_mask &= heat_surface_uv_mask
# Match only the sampled baked-brown artifact color (about sRGB 204,119,27).
# Restrict it to the two core UV regions so identical copper elsewhere stays dark.
base_srgb = base_pixels[:, :, 0:3]
target_srgb = np.asarray((204.0, 119.0, 27.0), dtype=np.float32) / 255.0
tolerance_srgb = np.asarray((5.0, 5.0, 7.0), dtype=np.float32) / 255.0
sampled_color_mask = np.all(np.abs(base_srgb - target_srgb) <= tolerance_srgb, axis=2)
core_uv_regions = np.zeros((height, width), dtype=bool)
core_uv_regions[387:578, 1300:1591] = True
core_uv_regions[917:1068, 1790:1936] = True
core_repair_mask = sampled_color_mask & core_uv_regions
repaired_core_pixels = int(np.count_nonzero(core_repair_mask & ~amber_mask))
amber_mask |= core_repair_mask
if repaired_core_pixels <= 0:
    raise RuntimeError("Thermobarrel chamber-hole repair did not affect the emission mask")
emission_pixels = np.zeros_like(base_pixels)
emission_pixels[:, :, 0:3] = amber_mask[:, :, None].astype(np.float32)
emission_pixels[:, :, 3] = 1.0
emission_image = bpy.data.images.new(
    name=EMISSION_PATH.stem,
    width=width,
    height=height,
    alpha=True,
    float_buffer=False,
)
emission_image.colorspace_settings.name = "Non-Color"
emission_image.pixels.foreach_set(emission_pixels.ravel())
emission_image.filepath_raw = str(EMISSION_PATH)
emission_image.file_format = "PNG"
emission_image.save()
emission_image.filepath = str(EMISSION_PATH)

material = mesh_obj.data.materials[0] if mesh_obj.data.materials else None
if material is None:
    material = bpy.data.materials.new(name=f"{ITEM_ID}_PBR")
    material.use_nodes = True
    mesh_obj.data.materials.append(material)
material.name = f"{ITEM_ID}_PBR"
material.diffuse_color = (0.12, 0.12, 0.12, 1.0)
material.metallic = 0.65
material.roughness = 0.34
try:
    material.surface_render_method = "DITHERED"
except (AttributeError, TypeError):
    pass

nodes = material.node_tree.nodes
links = material.node_tree.links
for node in list(nodes):
    nodes.remove(node)

output = nodes.new("ShaderNodeOutputMaterial")
output.name = "Material Output"
principled = nodes.new("ShaderNodeBsdfPrincipled")
principled.name = "Principled BSDF"
principled.inputs["Metallic"].default_value = 0.65
principled.inputs["Roughness"].default_value = 0.34
principled.inputs["Emission Strength"].default_value = 1.2
links.new(principled.outputs["BSDF"], output.inputs["Surface"])

base_node = nodes.new("ShaderNodeTexImage")
base_node.name = "BaseColor"
base_node.label = "BaseColor sRGB"
base_node.image = base_image
links.new(base_node.outputs["Color"], principled.inputs["Base Color"])

orm_node = nodes.new("ShaderNodeTexImage")
orm_node.name = "ORM"
orm_node.label = "Occlusion Roughness Metallic"
orm_node.image = orm_image
separate = nodes.new("ShaderNodeSeparateColor")
separate.name = "Separate ORM"
links.new(orm_node.outputs["Color"], separate.inputs["Color"])
links.new(separate.outputs["Green"], principled.inputs["Roughness"])
links.new(separate.outputs["Blue"], principled.inputs["Metallic"])

normal_node = nodes.new("ShaderNodeTexImage")
normal_node.name = "NormalGL"
normal_node.label = "Normal OpenGL"
normal_node.image = normal_image
normal_map = nodes.new("ShaderNodeNormalMap")
normal_map.name = "Normal Map"
normal_map.space = "TANGENT"
normal_map.inputs["Strength"].default_value = 1.0
links.new(normal_node.outputs["Color"], normal_map.inputs["Color"])
links.new(normal_map.outputs["Normal"], principled.inputs["Normal"])

emission_node = nodes.new("ShaderNodeTexImage")
emission_node.name = "EmissionMask"
emission_node.label = "Hard-bounded amber heat tube/chamber only"
emission_node.image = emission_image
emission_tint = nodes.new("ShaderNodeMixRGB")
emission_tint.name = "AmberEmissionTint"
emission_tint.blend_type = "MULTIPLY"
emission_tint.inputs["Fac"].default_value = 1.0
emission_tint.inputs[2].default_value = (1.0, 0.18, 0.0, 1.0)
links.new(emission_node.outputs["Color"], emission_tint.inputs[1])
links.new(emission_tint.outputs["Color"], principled.inputs["Emission Color"])

root = bpy.data.objects.new(ITEM_ID, None)
root.empty_display_type = "PLAIN_AXES"
root.empty_display_size = 0.06
scene.collection.objects.link(root)
root.location = (0.0, 0.0, 0.0)
root.rotation_euler = (0.0, 0.0, 0.0)
root.scale = (1.0, 1.0, 1.0)
mesh_obj.parent = root

right_grip = bpy.data.objects.new("RightHandGrip", None)
right_grip.empty_display_type = "ARROWS"
right_grip.empty_display_size = 0.05
scene.collection.objects.link(right_grip)
right_grip.parent = root
right_grip.location = (0.0, 0.0, 0.0)

left_grip_location = transformed_point(RAW_LEFT_HAND_CENTER)
left_grip_location.z = GRIP_SPACING
left_grip = bpy.data.objects.new("LeftHandGrip", None)
left_grip.empty_display_type = "ARROWS"
left_grip.empty_display_size = 0.05
scene.collection.objects.link(left_grip)
left_grip.parent = root
left_grip.location = left_grip_location

muzzle_location = transformed_point(RAW_MUZZLE_CENTER)
muzzle = bpy.data.objects.new("Muzzle", None)
muzzle.empty_display_type = "ARROWS"
muzzle.empty_display_size = 0.055
scene.collection.objects.link(muzzle)
muzzle.parent = root
muzzle.location = muzzle_location

for obj in (root, mesh_obj, right_grip, left_grip, muzzle):
    obj.rotation_euler = (0.0, 0.0, 0.0)
    obj.scale = (1.0, 1.0, 1.0)

scene["item_id"] = ITEM_ID
scene["weapon_class"] = "Advanced Shotgun"
scene["muzzle_axis"] = "+Z"
scene["up_axis"] = "+Y"
scene["grip_spacing_m"] = GRIP_SPACING
scene["emission_scope"] = "Amber heat tube and sealed chamber only; copper and paint non-emissive"

bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH), compress=True)
bpy.ops.file.make_paths_relative()
bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH), compress=True)

bpy.ops.object.select_all(action="DESELECT")
for obj in (root, mesh_obj, right_grip, left_grip, muzzle):
    obj.select_set(True)
bpy.context.view_layer.objects.active = root
bpy.ops.export_scene.fbx(
    filepath=str(FBX_PATH),
    use_selection=True,
    object_types={"EMPTY", "MESH"},
    use_mesh_modifiers=True,
    use_triangles=True,
    add_leaf_bones=False,
    bake_anim=False,
    axis_forward="-Z",
    axis_up="Y",
    apply_unit_scale=True,
    use_space_transform=True,
    path_mode="COPY",
    embed_textures=False,
)

mesh_obj.data.calc_loop_triangles()
mesh_bounds = [mesh_obj.matrix_world @ Vector(corner) for corner in mesh_obj.bound_box]
bounds_min = [min(point[index] for point in mesh_bounds) for index in range(3)]
bounds_max = [max(point[index] for point in mesh_bounds) for index in range(3)]

validation = {
    "item_id": ITEM_ID,
    "blender_version": bpy.app.version_string,
    "source_glb": str(RAW_GLB.relative_to(ITEM_DIR)),
    "raw_triangles": raw_triangles,
    "final_triangles": final_triangles,
    "triangle_target": "40000-60000, hard maximum 100000",
    "model_scale_from_raw": round(MODEL_SCALE, 8),
    "axes": {"muzzle": "+Z", "up": "+Y"},
    "root": {"name": ITEM_ID, "location": [0.0, 0.0, 0.0], "scale": [1.0, 1.0, 1.0]},
    "direct_children": sorted(child.name for child in root.children),
    "right_hand_grip": {
        "location": [round(value, 6) for value in right_grip.location],
        "raw_contact_width_m": round((0.214699 - 0.160004) * MODEL_SCALE, 6),
        "nearest_mesh_vertex_m": round(nearest_vertex_distance(mesh_obj, right_grip.location), 6),
    },
    "left_hand_grip": {
        "location": [round(value, 6) for value in left_grip.location],
        "foregrip_continuous_length_m": round((-0.073323 - -0.217709) * MODEL_SCALE, 6),
        "nearest_mesh_vertex_m": round(nearest_vertex_distance(mesh_obj, left_grip.location), 6),
    },
    "grip_spacing_z_m": round(left_grip.location.z - right_grip.location.z, 6),
    "muzzle": {
        "location": [round(value, 6) for value in muzzle.location],
        "forward_axis": "+Z",
    },
    "bounds_min_m": [round(value, 6) for value in bounds_min],
    "bounds_max_m": [round(value, 6) for value in bounds_max],
    "pbr": {
        "base_color": BASE_COLOR_PATH.name,
        "normal_gl": NORMAL_PATH.name,
        "orm": ORM_PATH.name,
        "emission": EMISSION_PATH.name,
        "emission_scope": "hard mask from saturated amber heat surfaces only; copper/paint excluded",
        "emission_mask_coverage": round(float(amber_mask.mean()), 8),
        "emission_allowed_face_count": emission_allowed_faces,
        "emission_core_hole_repaired_pixels": repaired_core_pixels,
        "emission_rgb_target_srgb": [204, 119, 27],
        "emission_rgb_tolerance_srgb": [5, 5, 7],
    },
    "forbidden_objects": [obj.name for obj in scene.objects if obj.type in {"CAMERA", "LIGHT", "ARMATURE"}],
    "outputs": {
        "blend": {"path": BLEND_PATH.name, "sha256": sha256(BLEND_PATH)},
        "fbx": {"path": FBX_PATH.name, "sha256": sha256(FBX_PATH)},
    },
}
VALIDATION_PATH.write_text(json.dumps(validation, indent=2), encoding="utf-8")
print(json.dumps(validation, indent=2))

# QA rendering uses temporary camera/lights after the clean production file and FBX are saved.
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1024
scene.render.resolution_y = 512
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

key_data = bpy.data.lights.new("QA_Key", type="AREA")
key_data.energy = 110.0
key_data.shape = "DISK"
key_data.size = 2.0
key = bpy.data.objects.new("QA_Key", key_data)
scene.collection.objects.link(key)
key.location = (-1.2, 1.4, 0.25)
point_at(key, (0.0, 0.03, 0.18))

fill_data = bpy.data.lights.new("QA_Fill", type="AREA")
fill_data.energy = 55.0
fill_data.size = 1.8
fill = bpy.data.objects.new("QA_Fill", fill_data)
scene.collection.objects.link(fill)
fill.location = (1.1, 0.7, 0.35)
point_at(fill, (0.0, 0.03, 0.18))

rim_data = bpy.data.lights.new("QA_Rim", type="AREA")
rim_data.energy = 75.0
rim_data.size = 1.5
rim = bpy.data.objects.new("QA_Rim", rim_data)
scene.collection.objects.link(rim)
rim.location = (0.6, 1.2, -0.4)
point_at(rim, (0.0, 0.03, 0.18))

views = {
    "left": ((-1.8, 0.03, 0.18), 1.08),
    "right": ((1.8, 0.03, 0.18), 1.08),
    "front_muzzle": ((0.0, 0.04, 1.8), 0.36),
    "back": ((0.0, 0.04, -1.8), 0.36),
}
neutral_dir = QA_DIR / "neutral"
emission_dir = QA_DIR / "emission"
neutral_dir.mkdir(parents=True, exist_ok=True)
emission_dir.mkdir(parents=True, exist_ok=True)

for name, (location, ortho_scale) in views.items():
    camera.location = location
    camera.data.ortho_scale = ortho_scale
    point_at(camera, (0.0, 0.03, 0.18))
    scene.render.filepath = str(neutral_dir / f"{name}.png")
    bpy.ops.render.render(write_still=True)

for light in (key, fill, rim):
    light.data.energy = 0.0
if scene.world is None:
    scene.world = bpy.data.worlds.new("QA_World")
scene.world.color = (0.0, 0.0, 0.0)
for name in ("left", "right"):
    location, ortho_scale = views[name]
    camera.location = location
    camera.data.ortho_scale = ortho_scale
    point_at(camera, (0.0, 0.03, 0.18))
    scene.render.filepath = str(emission_dir / f"{name}.png")
    bpy.ops.render.render(write_still=True)
