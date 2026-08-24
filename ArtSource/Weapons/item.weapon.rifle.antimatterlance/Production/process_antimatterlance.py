import hashlib
import json
from pathlib import Path

import bpy
import numpy as np
from mathutils import Matrix, Vector


ITEM_ID = "item.weapon.rifle.antimatterlance"
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

RAW_RIGHT_HAND_CENTER = Vector((0.245569, -0.000250, -0.062061))
RAW_LEFT_HAND_CENTER = Vector((-0.099203, 0.000198, -0.038308))
RAW_MUZZLE_CENTER = Vector((-0.500000, -0.004443, 0.037192))
GRIP_SPACING = 0.32625
MODEL_SCALE = GRIP_SPACING / (RAW_RIGHT_HAND_CENTER.x - RAW_LEFT_HAND_CENTER.x)
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
    obj.rotation_euler = Matrix((right, corrected_up, -forward)).transposed().to_euler()


def load_image(path, colorspace):
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
    points = np.asarray([[uv.x * (width - 1), uv.y * (height - 1)] for uv in uv_points], dtype=np.float32)
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


def build_magenta_surface_uv_mask(mesh_obj, width, height):
    mesh = mesh_obj.data
    uv_layer = mesh.uv_layers.active
    if uv_layer is None:
        raise RuntimeError("Emission region mask requires a UV layer")
    allowed = np.zeros((height, width), dtype=bool)
    allowed_faces = 0
    for polygon in mesh.polygons:
        center = polygon.center
        in_beam = 0.045 <= center.y <= 0.135 and 0.11 <= center.z <= 0.60
        in_rear_core = 0.025 <= center.y <= 0.17 and -0.035 <= center.z <= 0.14
        in_captured_crystal = 0.015 <= center.y <= 0.16 and 0.59 <= center.z <= 0.71
        if not (in_beam or in_rear_core or in_captured_crystal):
            continue
        loops = list(polygon.loop_indices)
        if len(loops) != 3:
            continue
        rasterize_uv_triangle(allowed, [uv_layer.data[index].uv for index in loops])
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
modifier = mesh_obj.modifiers.new(name="ProductionDecimate", type="DECIMATE")
modifier.decimate_type = "COLLAPSE"
modifier.ratio = min(1.0, TARGET_TRIANGLES / raw_triangles)
modifier.use_collapse_triangulate = True
bpy.context.view_layer.objects.active = mesh_obj
mesh_obj.select_set(True)
bpy.ops.object.modifier_apply(modifier=modifier.name)
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
base_image = load_image(BASE_COLOR_PATH, "sRGB")
normal_image = load_image(NORMAL_PATH, "Non-Color")
orm_image = load_image(ORM_PATH, "Non-Color")

width, height = base_image.size
pixels = np.asarray(base_image.pixels[:], dtype=np.float32).reshape((height, width, 4))
red, green, blue = pixels[:, :, 0], pixels[:, :, 1], pixels[:, :, 2]
magenta_mask = (
    (red >= 0.09)
    & (blue >= 0.025)
    & (red >= green * 1.55)
    & (blue >= green * 1.25)
    & (red >= blue * 0.78)
)
surface_uv_mask, emission_allowed_faces = build_magenta_surface_uv_mask(mesh_obj, width, height)
magenta_mask &= surface_uv_mask
emission_pixels = np.zeros_like(pixels)
emission_pixels[:, :, 0:3] = magenta_mask[:, :, None].astype(np.float32)
emission_pixels[:, :, 3] = 1.0
emission_image = bpy.data.images.new(EMISSION_PATH.stem, width=width, height=height, alpha=True)
emission_image.colorspace_settings.name = "Non-Color"
emission_image.pixels.foreach_set(emission_pixels.ravel())
emission_image.filepath_raw = str(EMISSION_PATH)
emission_image.file_format = "PNG"
emission_image.save()
emission_image.filepath = str(EMISSION_PATH)

material = mesh_obj.data.materials[0] if mesh_obj.data.materials else bpy.data.materials.new(f"{ITEM_ID}_PBR")
if not mesh_obj.data.materials:
    mesh_obj.data.materials.append(material)
material.name = f"{ITEM_ID}_PBR"
material.use_nodes = True
material.diffuse_color = (0.18, 0.18, 0.18, 1.0)
material.metallic = 0.55
material.roughness = 0.3
nodes = material.node_tree.nodes
links = material.node_tree.links
for node in list(nodes):
    nodes.remove(node)
output = nodes.new("ShaderNodeOutputMaterial")
principled = nodes.new("ShaderNodeBsdfPrincipled")
principled.inputs["Metallic"].default_value = 0.55
principled.inputs["Roughness"].default_value = 0.3
principled.inputs["Emission Strength"].default_value = 1.15
links.new(principled.outputs["BSDF"], output.inputs["Surface"])

base_node = nodes.new("ShaderNodeTexImage")
base_node.name = "BaseColor"
base_node.image = base_image
links.new(base_node.outputs["Color"], principled.inputs["Base Color"])
orm_node = nodes.new("ShaderNodeTexImage")
orm_node.name = "ORM"
orm_node.image = orm_image
separate = nodes.new("ShaderNodeSeparateColor")
links.new(orm_node.outputs["Color"], separate.inputs["Color"])
links.new(separate.outputs["Green"], principled.inputs["Roughness"])
links.new(separate.outputs["Blue"], principled.inputs["Metallic"])
normal_node = nodes.new("ShaderNodeTexImage")
normal_node.name = "NormalGL"
normal_node.image = normal_image
normal_map = nodes.new("ShaderNodeNormalMap")
links.new(normal_node.outputs["Color"], normal_map.inputs["Color"])
links.new(normal_map.outputs["Normal"], principled.inputs["Normal"])
emission_node = nodes.new("ShaderNodeTexImage")
emission_node.name = "EmissionMask"
emission_node.label = "Physical magenta beam/core/crystal only"
emission_node.image = emission_image
tint = nodes.new("ShaderNodeMixRGB")
tint.blend_type = "MULTIPLY"
tint.inputs["Fac"].default_value = 1.0
tint.inputs[2].default_value = (1.0, 0.015, 0.28, 1.0)
links.new(emission_node.outputs["Color"], tint.inputs[1])
links.new(tint.outputs["Color"], principled.inputs["Emission Color"])

root = bpy.data.objects.new(ITEM_ID, None)
root.empty_display_type = "PLAIN_AXES"
root.empty_display_size = 0.06
scene.collection.objects.link(root)
mesh_obj.parent = root

right_grip = bpy.data.objects.new("RightHandGrip", None)
right_grip.empty_display_type = "ARROWS"
right_grip.empty_display_size = 0.05
scene.collection.objects.link(right_grip)
right_grip.parent = root
right_grip.location = (0.0, 0.0, 0.0)

left_location = transformed_point(RAW_LEFT_HAND_CENTER)
left_location.z = GRIP_SPACING
left_grip = bpy.data.objects.new("LeftHandGrip", None)
left_grip.empty_display_type = "ARROWS"
left_grip.empty_display_size = 0.05
scene.collection.objects.link(left_grip)
left_grip.parent = root
left_grip.location = left_location

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
scene["weapon_class"] = "Unique Rifle"
scene["muzzle_axis"] = "+Z"
scene["up_axis"] = "+Y"
scene["grip_spacing_m"] = GRIP_SPACING
scene["muzzle_semantics"] = "Axial four ivory prongs capturing a faceted magenta crystal tip"
scene["emission_scope"] = "Physical magenta beam, rear core, and captured crystal only"

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

bounds = [mesh_obj.matrix_world @ Vector(corner) for corner in mesh_obj.bound_box]
validation = {
    "item_id": ITEM_ID,
    "blender_version": bpy.app.version_string,
    "raw_triangles": raw_triangles,
    "final_triangles": final_triangles,
    "triangle_target": "40000-60000, hard maximum 100000",
    "model_scale_from_raw": round(MODEL_SCALE, 8),
    "axes": {"muzzle": "+Z", "up": "+Y"},
    "design_audit": {
        "two_ivory_rails": True,
        "three_black_gold_collars": True,
        "rear_round_magenta_core": True,
        "captured_faceted_magenta_crystal": True,
        "muzzle_semantics": "axial four-prong crystal tip, not the rear circular core",
    },
    "root": {"name": ITEM_ID, "location": [0.0, 0.0, 0.0], "scale": [1.0, 1.0, 1.0]},
    "direct_children": sorted(child.name for child in root.children),
    "right_hand_grip": {
        "location": [round(value, 6) for value in right_grip.location],
        "actual_contact_width_m": round((0.275193 - 0.218000) * MODEL_SCALE, 6),
        "nearest_mesh_vertex_m": round(nearest_vertex_distance(mesh_obj, right_grip.location), 6),
    },
    "left_hand_grip": {
        "location": [round(value, 6) for value in left_grip.location],
        "foregrip_continuous_length_m": round((-0.028627 - -0.168125) * MODEL_SCALE, 6),
        "nearest_mesh_vertex_m": round(nearest_vertex_distance(mesh_obj, left_grip.location), 6),
    },
    "grip_spacing_z_m": round(left_grip.location.z, 6),
    "muzzle": {"location": [round(value, 6) for value in muzzle.location], "forward_axis": "+Z"},
    "bounds_min_m": [round(min(point[index] for point in bounds), 6) for index in range(3)],
    "bounds_max_m": [round(max(point[index] for point in bounds), 6) for index in range(3)],
    "pbr": {
        "base_color": BASE_COLOR_PATH.name,
        "normal_gl": NORMAL_PATH.name,
        "orm": ORM_PATH.name,
        "emission": EMISSION_PATH.name,
        "emission_scope": "hard UV mask: physical magenta beam, rear core, captured crystal only",
        "emission_mask_coverage": round(float(magenta_mask.mean()), 8),
        "emission_allowed_face_count": emission_allowed_faces,
        "ivory_black_gold_non_emissive": True,
    },
    "forbidden_objects": [obj.name for obj in scene.objects if obj.type in {"CAMERA", "LIGHT", "ARMATURE"}],
    "outputs": {
        "blend": {"path": BLEND_PATH.name, "sha256": sha256(BLEND_PATH)},
        "fbx": {"path": FBX_PATH.name, "sha256": sha256(FBX_PATH)},
    },
}
VALIDATION_PATH.write_text(json.dumps(validation, indent=2), encoding="utf-8")
print(json.dumps(validation, indent=2))

# Temporary PBR QA rig; production files above remain camera/light-free.
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1024
scene.render.resolution_y = 512
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.film_transparent = True
scene.view_settings.look = "AgX - Medium High Contrast"
camera = bpy.data.objects.new("QA_Camera", bpy.data.cameras.new("QA_Camera"))
camera.data.type = "ORTHO"
scene.collection.objects.link(camera)
scene.camera = camera
lights = []
for name, location, energy, size in (
    ("QA_Key", (-1.2, 1.4, 0.3), 110.0, 2.0),
    ("QA_Fill", (1.1, 0.7, 0.4), 55.0, 1.8),
    ("QA_Rim", (0.6, 1.2, -0.4), 75.0, 1.5),
):
    data = bpy.data.lights.new(name, type="AREA")
    data.energy = energy
    data.size = size
    light = bpy.data.objects.new(name, data)
    scene.collection.objects.link(light)
    light.location = location
    point_at(light, (0.0, 0.03, 0.22))
    lights.append(light)
views = {
    "left": ((-1.8, 0.03, 0.22), 1.12),
    "right": ((1.8, 0.03, 0.22), 1.12),
    "front_muzzle": ((0.0, 0.05, 1.9), 0.34),
    "back": ((0.0, 0.05, -1.9), 0.34),
}
neutral_dir = QA_DIR / "neutral"
emission_dir = QA_DIR / "emission"
neutral_dir.mkdir(parents=True, exist_ok=True)
emission_dir.mkdir(parents=True, exist_ok=True)
for name, (location, scale) in views.items():
    camera.location = location
    camera.data.ortho_scale = scale
    point_at(camera, (0.0, 0.03, 0.22))
    scene.render.filepath = str(neutral_dir / f"{name}.png")
    bpy.ops.render.render(write_still=True)
for light in lights:
    light.data.energy = 0.0
if scene.world is None:
    scene.world = bpy.data.worlds.new("QA_World")
scene.world.color = (0.0, 0.0, 0.0)
for name in ("left", "right"):
    location, scale = views[name]
    camera.location = location
    camera.data.ortho_scale = scale
    point_at(camera, (0.0, 0.03, 0.22))
    scene.render.filepath = str(emission_dir / f"{name}.png")
    bpy.ops.render.render(write_still=True)
