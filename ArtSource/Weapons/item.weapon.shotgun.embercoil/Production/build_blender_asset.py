from __future__ import annotations

import hashlib
import json
import math
import os
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


ITEM_ID = "item.weapon.shotgun.embercoil"
ROOT = Path(__file__).resolve().parent
SOURCE_GLB = ROOT.parent / "Tripo" / "Downloaded" / "model.glb"
TEXTURE_DIR = ROOT / "Textures"
BASE_COLOR = TEXTURE_DIR / f"{ITEM_ID}_BaseColor.png"
NORMAL = TEXTURE_DIR / f"{ITEM_ID}_Normal.png"
ORM = TEXTURE_DIR / f"{ITEM_ID}_MetallicRoughness.png"
EMISSION = TEXTURE_DIR / f"{ITEM_ID}_Emission.png"
BLEND_PATH = ROOT / f"{ITEM_ID}.blend"
FBX_PATH = ROOT / f"{ITEM_ID}.fbx"
REPORT_PATH = ROOT / "QA" / "production_build.json"

TARGET_TRIANGLES = 120_000
RIGHT_GRIP_RAW = Vector((0.205, 0.0, -0.075))
LEFT_GRIP_RAW = Vector((RIGHT_GRIP_RAW.x - 0.326, 0.0, -0.015))
MUZZLE_RAW = Vector((-0.5, -0.0051376353790613715, 0.0789124548736462))
RIGHT_GRIP_ROTATION_X_DEGREES = 30.0
DESIGNATED_EMISSION_SRGB = (255, 74, 26)
EMISSION_STRENGTH = 6.0


def srgb_channel_to_linear(value: int) -> float:
    channel = value / 255.0
    if channel <= 0.04045:
        return channel / 12.92
    return ((channel + 0.055) / 1.055) ** 2.4


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def triangle_count(obj: bpy.types.Object) -> int:
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def load_image(path: Path, colorspace: str) -> bpy.types.Image:
    image = bpy.data.images.load(os.fspath(path), check_existing=True)
    image.name = path.stem
    image.colorspace_settings.name = colorspace
    image.filepath = f"//Textures/{path.name}"
    image.filepath_raw = image.filepath
    image.source = "FILE"
    image.use_fake_user = True
    return image


def build_material() -> bpy.types.Material:
    material = bpy.data.materials.new("M_EmberCoil_PBR")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()

    output = nodes.new("ShaderNodeOutputMaterial")
    output.name = "Material Output"
    output.location = (860, 80)

    principled = nodes.new("ShaderNodeBsdfPrincipled")
    principled.name = "Ember Coil Principled PBR"
    principled.location = (560, 80)
    principled.inputs["Alpha"].default_value = 1.0
    principled.inputs["Emission Color"].default_value = tuple(
        srgb_channel_to_linear(value) for value in DESIGNATED_EMISSION_SRGB
    ) + (1.0,)
    links.new(principled.outputs["BSDF"], output.inputs["Surface"])

    base_node = nodes.new("ShaderNodeTexImage")
    base_node.name = "BaseColor_sRGB"
    base_node.label = "Base Color (sRGB)"
    base_node.location = (-760, 420)
    base_node.image = load_image(BASE_COLOR, "sRGB")
    base_node.interpolation = "Linear"
    links.new(base_node.outputs["Color"], principled.inputs["Base Color"])

    orm_node = nodes.new("ShaderNodeTexImage")
    orm_node.name = "ORM_NonColor"
    orm_node.label = "R=Occlusion G=Roughness B=Metallic"
    orm_node.location = (-760, 40)
    orm_node.image = load_image(ORM, "Non-Color")
    orm_node.interpolation = "Linear"

    orm_separate = nodes.new("ShaderNodeSeparateColor")
    orm_separate.name = "Separate_ORM"
    orm_separate.mode = "RGB"
    orm_separate.location = (-430, 40)
    links.new(orm_node.outputs["Color"], orm_separate.inputs["Color"])
    links.new(orm_separate.outputs["Green"], principled.inputs["Roughness"])
    links.new(orm_separate.outputs["Blue"], principled.inputs["Metallic"])

    normal_node = nodes.new("ShaderNodeTexImage")
    normal_node.name = "NormalGL_NonColor"
    normal_node.label = "Tangent-space NormalGL (Non-Color)"
    normal_node.location = (-760, -280)
    normal_node.image = load_image(NORMAL, "Non-Color")
    normal_node.interpolation = "Linear"

    normal_map = nodes.new("ShaderNodeNormalMap")
    normal_map.name = "Normal Map"
    normal_map.location = (-410, -250)
    normal_map.space = "TANGENT"
    normal_map.inputs["Strength"].default_value = 1.0
    links.new(normal_node.outputs["Color"], normal_map.inputs["Color"])
    links.new(normal_map.outputs["Normal"], principled.inputs["Normal"])

    emission_node = nodes.new("ShaderNodeTexImage")
    emission_node.name = "EmissionMask_GlobalBinary"
    emission_node.label = "Global RGB threshold only; binary mask"
    emission_node.location = (-760, -580)
    emission_node.image = load_image(EMISSION, "Non-Color")
    emission_node.interpolation = "Closest"

    emission_strength = nodes.new("ShaderNodeMath")
    emission_strength.name = "Emission Strength"
    emission_strength.operation = "MULTIPLY"
    emission_strength.location = (-360, -500)
    emission_strength.inputs[1].default_value = EMISSION_STRENGTH
    links.new(emission_node.outputs["Color"], emission_strength.inputs[0])
    links.new(emission_strength.outputs[0], principled.inputs["Emission Strength"])

    return material


def transformed_point(raw: Vector) -> Vector:
    relative = raw - RIGHT_GRIP_RAW
    return Vector((-relative.y, relative.z, -relative.x))


for required_path in (SOURCE_GLB, BASE_COLOR, NORMAL, ORM, EMISSION):
    if not required_path.is_file():
        raise FileNotFoundError(required_path)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.cameras, bpy.data.lights):
    for datablock in list(datablocks):
        datablocks.remove(datablock)

bpy.ops.import_scene.gltf(filepath=os.fspath(SOURCE_GLB))
mesh_objects = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
if len(mesh_objects) != 1:
    raise RuntimeError(f"Expected exactly one source mesh, found {len(mesh_objects)}")
model = mesh_objects[0]

for obj in list(bpy.context.scene.objects):
    if obj != model:
        bpy.data.objects.remove(obj, do_unlink=True)

bpy.context.view_layer.objects.active = model
model.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
raw_triangles = triangle_count(model)

if raw_triangles > TARGET_TRIANGLES:
    decimate = model.modifiers.new(name="Realtime Decimate", type="DECIMATE")
    decimate.decimate_type = "COLLAPSE"
    decimate.ratio = TARGET_TRIANGLES / raw_triangles
    decimate.use_collapse_triangulate = True
    bpy.context.view_layer.objects.active = model
    bpy.ops.object.modifier_apply(modifier=decimate.name)

model.data.calc_loop_triangles()
final_triangles = triangle_count(model)
if not model.data.uv_layers:
    raise RuntimeError("UV layer was lost during optimization")

# Raw muzzle points toward -X and raw top points toward +Z. Apply a proper
# rotation and translate the real trigger-grip center to the asset origin:
# final X=-raw Y, final Y=raw Z, final Z=-raw X.
raw_to_final = Matrix(
    (
        (0.0, -1.0, 0.0, 0.0),
        (0.0, 0.0, 1.0, 0.0),
        (-1.0, 0.0, 0.0, 0.0),
        (0.0, 0.0, 0.0, 1.0),
    )
)
model.data.transform(raw_to_final @ Matrix.Translation(-RIGHT_GRIP_RAW))
model.data.update()
model.name = "Model"
model.data.name = f"{ITEM_ID}_Mesh"
model.location = (0.0, 0.0, 0.0)
model.rotation_euler = (0.0, 0.0, 0.0)
model.scale = (1.0, 1.0, 1.0)

model.data.materials.clear()
for material in list(bpy.data.materials):
    bpy.data.materials.remove(material)
for image in list(bpy.data.images):
    bpy.data.images.remove(image)
model.data.materials.append(build_material())

asset_root = bpy.data.objects.new(ITEM_ID, None)
asset_root.empty_display_type = "PLAIN_AXES"
asset_root.empty_display_size = 0.08
bpy.context.scene.collection.objects.link(asset_root)
model.parent = asset_root

right_hand = bpy.data.objects.new("RightHandGrip", None)
right_hand.empty_display_type = "ARROWS"
right_hand.empty_display_size = 0.055
right_hand.rotation_euler = (math.radians(RIGHT_GRIP_ROTATION_X_DEGREES), 0.0, 0.0)
bpy.context.scene.collection.objects.link(right_hand)
right_hand.parent = asset_root

left_hand = bpy.data.objects.new("LeftHandGrip", None)
left_hand.empty_display_type = "ARROWS"
left_hand.empty_display_size = 0.055
left_hand.location = transformed_point(LEFT_GRIP_RAW)
bpy.context.scene.collection.objects.link(left_hand)
left_hand.parent = asset_root

muzzle = bpy.data.objects.new("Muzzle", None)
muzzle.empty_display_type = "ARROWS"
muzzle.empty_display_size = 0.055
muzzle.location = transformed_point(MUZZLE_RAW)
bpy.context.scene.collection.objects.link(muzzle)
muzzle.parent = asset_root

for obj in (asset_root, model, right_hand, left_hand, muzzle):
    if any(abs(value - 1.0) > 1e-8 for value in obj.scale):
        raise RuntimeError(f"Non-unit scale on {obj.name}: {tuple(obj.scale)}")

scene = bpy.context.scene
scene.unit_settings.system = "METRIC"
scene.unit_settings.scale_length = 1.0
scene.render.film_transparent = True

for obj in bpy.context.scene.objects:
    obj.select_set(False)
for obj in (asset_root, model, right_hand, left_hand, muzzle):
    obj.select_set(True)
bpy.context.view_layer.objects.active = asset_root

BLEND_PATH.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=os.fspath(BLEND_PATH), check_existing=False)

bpy.ops.export_scene.fbx(
    filepath=os.fspath(FBX_PATH),
    use_selection=True,
    object_types={"EMPTY", "MESH"},
    global_scale=1.0,
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_ALL",
    use_space_transform=True,
    bake_space_transform=False,
    axis_forward="-Z",
    axis_up="Y",
    use_mesh_modifiers=True,
    mesh_smooth_type="OFF",
    use_mesh_edges=False,
    use_tspace=True,
    add_leaf_bones=False,
    primary_bone_axis="Y",
    secondary_bone_axis="X",
    path_mode="RELATIVE",
    embed_textures=False,
    bake_anim=False,
)

world_corners = [model.matrix_world @ Vector(corner) for corner in model.bound_box]
minimum = Vector((min(p.x for p in world_corners), min(p.y for p in world_corners), min(p.z for p in world_corners)))
maximum = Vector((max(p.x for p in world_corners), max(p.y for p in world_corners), max(p.z for p in world_corners)))

report = {
    "item_id": ITEM_ID,
    "blender_version": bpy.app.version_string,
    "source_glb": str(SOURCE_GLB.resolve()),
    "source_glb_sha256": sha256(SOURCE_GLB),
    "blend_path": str(BLEND_PATH.resolve()),
    "fbx_path": str(FBX_PATH.resolve()),
    "raw_triangles": raw_triangles,
    "target_triangles": TARGET_TRIANGLES,
    "final_triangles": final_triangles,
    "optimization_ratio": final_triangles / raw_triangles,
    "mesh_vertices": len(model.data.vertices),
    "uv_layers": [layer.name for layer in model.data.uv_layers],
    "bounds_min_m": list(minimum),
    "bounds_max_m": list(maximum),
    "dimensions_m": list(maximum - minimum),
    "axis_convention": {"muzzle": "+Z", "top": "+Y"},
    "root_origin": "actual trigger-grip center",
    "objects": {
        obj.name: {
            "type": obj.type,
            "parent": obj.parent.name if obj.parent else None,
            "location": list(obj.location),
            "rotation_euler_radians": list(obj.rotation_euler),
            "scale": list(obj.scale),
        }
        for obj in (asset_root, model, right_hand, left_hand, muzzle)
    },
    "anchors": {
        "RightHandGrip": {
            "location_m": list(right_hand.location),
            "grip_axis_rotation_x_degrees": RIGHT_GRIP_ROTATION_X_DEGREES,
        },
        "LeftHandGrip": {
            "location_m": list(left_hand.location),
            "forward_distance_from_trigger_m": float(left_hand.location.z),
        },
        "Muzzle": {
            "location_m": list(muzzle.location),
            "forward_axis": "+Z",
            "source_front_plane_center_raw": list(MUZZLE_RAW),
        },
    },
    "textures": {
        "BaseColor_sRGB": {"path": str(BASE_COLOR.resolve()), "sha256": sha256(BASE_COLOR)},
        "NormalGL_NonColor": {"path": str(NORMAL.resolve()), "sha256": sha256(NORMAL)},
        "ORM_NonColor": {"path": str(ORM.resolve()), "sha256": sha256(ORM)},
        "Emission_GlobalBinary_NonColor": {"path": str(EMISSION.resolve()), "sha256": sha256(EMISSION)},
    },
    "emission": {
        "designated_srgb": list(DESIGNATED_EMISSION_SRGB),
        "designated_srgb_hex": "#FF4A1A",
        "designated_linear": [
            srgb_channel_to_linear(value) for value in DESIGNATED_EMISSION_SRGB
        ],
        "strength": EMISSION_STRENGTH,
        "global_binary_mask_only": True,
        "uv_exception": False,
    },
    "excluded_object_types": ["CAMERA", "LIGHT", "ARMATURE"],
    "colliders_added": False,
}
REPORT_PATH.parent.mkdir(parents=True, exist_ok=True)
REPORT_PATH.write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps(report, indent=2))
