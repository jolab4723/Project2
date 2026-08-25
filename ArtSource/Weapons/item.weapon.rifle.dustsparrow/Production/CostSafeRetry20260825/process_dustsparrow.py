import hashlib
import json
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


ITEM_ID = "item.weapon.rifle.dustsparrow"
ITEM_DIR = Path(__file__).resolve().parents[2]
RAW_GLB = ITEM_DIR / "Tripo" / "CostSafeRetry20260825" / "Downloaded" / f"{ITEM_ID}_raw.glb"
PRODUCTION_DIR = Path(__file__).resolve().parent
TEXTURE_DIR = PRODUCTION_DIR / "Textures"
QA_DIR = PRODUCTION_DIR / "QA"
BLEND_PATH = PRODUCTION_DIR / f"{ITEM_ID}.blend"
FBX_PATH = PRODUCTION_DIR / f"{ITEM_ID}.fbx"
VALIDATION_PATH = PRODUCTION_DIR / "blender_validation.json"

BASE_COLOR_PATH = TEXTURE_DIR / f"{ITEM_ID}_BaseColor.png"
NORMAL_PATH = TEXTURE_DIR / f"{ITEM_ID}_NormalGL.png"
ORM_PATH = TEXTURE_DIR / f"{ITEM_ID}_ORM.png"

# RAW long axis is X, with muzzle at -X and top at +Z.
# These centers are measured from the raw side/axial gates and the dense mesh bounds.
RAW_RIGHT_HAND_CENTER = Vector((0.2015, 0.0005, -0.0710))
RAW_LEFT_HAND_CENTER = Vector((-0.1600, 0.0005, 0.0260))
RAW_MUZZLE_CENTER = Vector((-0.5000, 0.00056, 0.06326))
GRIP_SPACING = 0.32625
MODEL_SCALE = GRIP_SPACING / (RAW_RIGHT_HAND_CENTER.x - RAW_LEFT_HAND_CENTER.x)
TARGET_TRIANGLES = 300000


def transformed_point(point: Vector) -> Vector:
    local = point - RAW_RIGHT_HAND_CENTER
    return Vector((-local.y, local.z, -local.x)) * MODEL_SCALE


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def point_at(obj, target: Vector) -> None:
    forward = (target - obj.location).normalized()
    reference_up = Vector((0.0, 1.0, 0.0))
    if abs(forward.dot(reference_up)) > 0.98:
        reference_up = Vector((0.0, 0.0, 1.0))
    right = forward.cross(reference_up).normalized()
    corrected_up = right.cross(forward).normalized()
    obj.rotation_euler = Matrix((right, corrected_up, -forward)).transposed().to_euler()


def nearest_vertex_distance(mesh_obj, location: Vector) -> float:
    return min((vertex.co - location).length for vertex in mesh_obj.data.vertices)


def save_source_image(image, destination: Path, colorspace: str) -> None:
    image.colorspace_settings.name = colorspace
    image.filepath_raw = str(destination)
    image.file_format = "PNG"
    image.save()


def load_external_image(path: Path, colorspace: str):
    image = bpy.data.images.load(str(path), check_existing=False)
    image.name = path.stem
    image.colorspace_settings.name = colorspace
    image.filepath = str(path)
    return image


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
    raise RuntimeError(f"Expected one raw mesh, found {len(mesh_objects)}")
mesh_obj = mesh_objects[0]
mesh_obj.name = f"{ITEM_ID}_Mesh"
mesh_obj.data.name = f"{ITEM_ID}_MeshData"

source_images = {}
all_source_images = []
for material in mesh_obj.data.materials:
    if not material.use_nodes or not material.node_tree:
        continue
    for node in material.node_tree.nodes:
        if node.type != "TEX_IMAGE" or node.image is None:
            continue
        if node.image not in all_source_images:
            all_source_images.append(node.image)
        lower = node.image.name.lower()
        if "orm" in lower:
            source_images["orm"] = node.image
        elif "normal" in lower:
            source_images["normal"] = node.image
        elif "color" in lower:
            source_images["base"] = node.image
if "normal" not in source_images:
    remaining = [image for image in all_source_images if image not in source_images.values()]
    if len(remaining) == 1:
        source_images["normal"] = remaining[0]
if set(source_images) != {"base", "normal", "orm"}:
    raise RuntimeError(f"Incomplete PBR source images: {sorted(source_images)}")

save_source_image(source_images["base"], BASE_COLOR_PATH, "sRGB")
save_source_image(source_images["normal"], NORMAL_PATH, "Non-Color")
save_source_image(source_images["orm"], ORM_PATH, "Non-Color")

mesh_obj.data.calc_loop_triangles()
raw_triangles = len(mesh_obj.data.loop_triangles)
decimate_ratio = min(1.0, TARGET_TRIANGLES / raw_triangles)
if decimate_ratio < 0.999999:
    decimate = mesh_obj.modifiers.new(name="ProductionDecimate", type="DECIMATE")
    decimate.decimate_type = "COLLAPSE"
    decimate.ratio = decimate_ratio
    decimate.use_collapse_triangulate = True
    try:
        decimate.delimit = {"UV"}
    except (AttributeError, TypeError):
        pass
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

base_image = load_external_image(BASE_COLOR_PATH, "sRGB")
normal_image = load_external_image(NORMAL_PATH, "Non-Color")
orm_image = load_external_image(ORM_PATH, "Non-Color")

material = mesh_obj.data.materials[0] if mesh_obj.data.materials else None
if material is None:
    material = bpy.data.materials.new(name=f"{ITEM_ID}_PBR")
    mesh_obj.data.materials.append(material)
material.name = f"{ITEM_ID}_PBR"
material.use_nodes = True
material.diffuse_color = (0.31, 0.29, 0.24, 1.0)
material.metallic = 0.55
material.roughness = 0.42

# Preserve Tripo's working glTF node graph exactly; only replace packed images with
# the external Unity-delivery files. Rebuilding this graph changes implicit glTF UV
# behavior in Blender and is therefore intentionally avoided.
for node in material.node_tree.nodes:
    if node.type == "TEX_IMAGE" and node.image is not None:
        lower = node.image.name.lower()
        if "orm" in lower:
            node.image = orm_image
        elif "normal" in lower:
            node.image = normal_image
        elif "color" in lower:
            node.image = base_image
    if node.type == "BSDF_PRINCIPLED":
        node.inputs["Emission Strength"].default_value = 0.0

root = bpy.data.objects.new(ITEM_ID, None)
root.empty_display_type = "PLAIN_AXES"
root.empty_display_size = 0.06
scene.collection.objects.link(root)
mesh_obj.parent = root

right_grip = bpy.data.objects.new("RightHandGrip", None)
right_grip.empty_display_type = "ARROWS"
right_grip.empty_display_size = 0.045
scene.collection.objects.link(right_grip)
right_grip.parent = root
right_grip.location = (0.0, 0.0, 0.0)

left_grip = bpy.data.objects.new("LeftHandGrip", None)
left_grip.empty_display_type = "ARROWS"
left_grip.empty_display_size = 0.045
scene.collection.objects.link(left_grip)
left_grip.parent = root
left_grip.location = transformed_point(RAW_LEFT_HAND_CENTER)
left_grip.location.z = GRIP_SPACING

muzzle = bpy.data.objects.new("Muzzle", None)
muzzle.empty_display_type = "ARROWS"
muzzle.empty_display_size = 0.04
scene.collection.objects.link(muzzle)
muzzle.parent = root
muzzle.location = transformed_point(RAW_MUZZLE_CENTER)

for obj in (root, mesh_obj, right_grip, left_grip, muzzle):
    obj.rotation_euler = (0.0, 0.0, 0.0)
    obj.scale = (1.0, 1.0, 1.0)

scene["item_id"] = ITEM_ID
scene["weapon_class"] = "Rifle"
scene["muzzle_axis"] = "+Z"
scene["up_axis"] = "+Y"
scene["grip_spacing_m"] = GRIP_SPACING
scene["emission_scope"] = "None: Common weapon has no intended emission"

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
    "source_glb": str(RAW_GLB.relative_to(ITEM_DIR)).replace("\\", "/"),
    "raw_triangles": raw_triangles,
    "final_triangles": final_triangles,
    "triangle_target": TARGET_TRIANGLES,
    "model_scale_from_raw": round(MODEL_SCALE, 8),
    "axes": {"muzzle": "+Z", "up": "+Y"},
    "root": {"name": ITEM_ID, "location": [0.0, 0.0, 0.0], "scale": [1.0, 1.0, 1.0]},
    "direct_children": sorted(child.name for child in root.children),
    "right_hand_grip": {
        "location": [round(value, 6) for value in right_grip.location],
        "raw_center": [round(value, 6) for value in RAW_RIGHT_HAND_CENTER],
        "nearest_mesh_vertex_m": round(nearest_vertex_distance(mesh_obj, right_grip.location), 6),
    },
    "left_hand_grip": {
        "location": [round(value, 6) for value in left_grip.location],
        "raw_center": [round(value, 6) for value in RAW_LEFT_HAND_CENTER],
        "nearest_mesh_vertex_m": round(nearest_vertex_distance(mesh_obj, left_grip.location), 6),
    },
    "grip_spacing_z_m": round(left_grip.location.z - right_grip.location.z, 6),
    "muzzle": {
        "location": [round(value, 6) for value in muzzle.location],
        "raw_center": [round(value, 6) for value in RAW_MUZZLE_CENTER],
        "forward_axis": "+Z",
    },
    "bounds_min_m": [round(value, 6) for value in bounds_min],
    "bounds_max_m": [round(value, 6) for value in bounds_max],
    "pbr": {
        "base_color": BASE_COLOR_PATH.name,
        "normal_gl": NORMAL_PATH.name,
        "orm": ORM_PATH.name,
        "emission": None,
        "emission_reason": "Common Dust Sparrow has no intended emission.",
    },
    "forbidden_objects": [obj.name for obj in scene.objects if obj.type in {"CAMERA", "LIGHT", "ARMATURE"}],
    "outputs": {
        "blend": {"path": BLEND_PATH.name, "sha256": sha256(BLEND_PATH)},
        "fbx": {"path": FBX_PATH.name, "sha256": sha256(FBX_PATH)},
    },
}
VALIDATION_PATH.write_text(json.dumps(validation, indent=2), encoding="utf-8")
print(json.dumps(validation, indent=2))

# Temporary QA-only camera and lights are added after the clean Blend/FBX are saved.
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

for name, location, energy, size in (
    ("QA_Key", (-1.2, 1.4, 0.25), 750.0, 2.0),
    ("QA_Fill", (1.1, 0.8, 0.45), 380.0, 1.8),
    ("QA_Rim", (0.6, 1.2, -0.45), 500.0, 1.5),
):
    light_data = bpy.data.lights.new(name, type="AREA")
    light_data.energy = energy
    light_data.size = size
    light = bpy.data.objects.new(name, light_data)
    scene.collection.objects.link(light)
    light.location = location
    point_at(light, Vector((0.0, 0.03, 0.18)))

view_directions = {
    "left": Vector((-1.0, 0.0, 0.0)),
    "right": Vector((1.0, 0.0, 0.0)),
    "front_muzzle": Vector((0.0, 0.0, 1.0)),
    "rear_stock": Vector((0.0, 0.0, -1.0)),
    "top": Vector((0.0, 1.0, 0.0)),
    "bottom": Vector((0.0, -1.0, 0.0)),
    "iso_front_left": Vector((-0.65, 0.45, 0.8)).normalized(),
    "iso_rear_right": Vector((0.65, 0.45, -0.8)).normalized(),
}
center = Vector(tuple((bounds_min[index] + bounds_max[index]) * 0.5 for index in range(3)))
corners = [mesh_obj.matrix_world @ Vector(corner) for corner in mesh_obj.bound_box]
for name, direction in view_directions.items():
    camera.location = center + direction * 2.2
    point_at(camera, center)
    bpy.context.view_layer.update()
    inverse = camera.matrix_world.inverted()
    projected = [inverse @ point for point in corners]
    width = max(point.x for point in projected) - min(point.x for point in projected)
    height = max(point.y for point in projected) - min(point.y for point in projected)
    camera.data.ortho_scale = max(height * 1.22, width / 2.0 * 1.22)
    scene.render.filepath = str(QA_DIR / f"{name}.png")
    bpy.ops.render.render(write_still=True)
