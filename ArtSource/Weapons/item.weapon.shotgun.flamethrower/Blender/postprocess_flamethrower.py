from __future__ import annotations

import json
import math
import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree


ITEM_ID = "item.weapon.shotgun.flamethrower"
TARGET_TRIANGLES = 55000
LEFT_HAND_Z = 0.32625


def cli_arg(name: str) -> str:
    args = sys.argv[sys.argv.index("--") + 1 :]
    return args[args.index(name) + 1]


def external_image(path: Path, colorspace: str) -> bpy.types.Image:
    image = bpy.data.images.load(str(path.resolve()), check_existing=False)
    image.colorspace_settings.name = colorspace
    image.filepath = str(path.resolve())
    image.filepath_raw = str(path.resolve())
    return image


def emission_material(name: str, color: tuple[float, float, float, float]) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    nodes.clear()
    shader = nodes.new("ShaderNodeEmission")
    shader.inputs["Color"].default_value = color
    shader.inputs["Strength"].default_value = 1.0
    output = nodes.new("ShaderNodeOutputMaterial")
    material.node_tree.links.new(shader.outputs["Emission"], output.inputs["Surface"])
    return material


def add_bake_target(material: bpy.types.Material, image: bpy.types.Image) -> None:
    node = material.node_tree.nodes.new("ShaderNodeTexImage")
    node.name = "EmissionBakeTarget"
    node.image = image
    node.select = True
    material.node_tree.nodes.active = node


source = Path(cli_arg("--source")).resolve()
root_dir = Path(cli_arg("--root-dir")).resolve()
texture_dir = root_dir / "Textures"
blend_dir = root_dir / "Blender"
export_dir = root_dir / "Export"
qa_dir = root_dir / "QA"
for directory in (texture_dir, blend_dir, export_dir, qa_dir):
    directory.mkdir(parents=True, exist_ok=True)

base_color_path = texture_dir / f"{ITEM_ID}_BaseColor.png"
normal_path = texture_dir / f"{ITEM_ID}_NormalGL.png"
orm_path = texture_dir / f"{ITEM_ID}_ORM.png"
emission_path = texture_dir / f"{ITEM_ID}_Emission.png"
blend_path = blend_dir / f"{ITEM_ID}.blend"
fbx_path = export_dir / f"{ITEM_ID}.fbx"
report_path = qa_dir / "postprocess_report.json"

for required in (source, base_color_path, normal_path, orm_path):
    if not required.is_file():
        raise FileNotFoundError(required)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(source))
mesh_object = next(obj for obj in bpy.context.scene.objects if obj.type == "MESH")
mesh_object.name = f"{ITEM_ID}_Mesh"
mesh_object.data.name = f"{ITEM_ID}_MeshData"
mesh_object.data.calc_loop_triangles()
source_triangles = len(mesh_object.data.loop_triangles)

modifier = mesh_object.modifiers.new("Final55k", "DECIMATE")
modifier.decimate_type = "COLLAPSE"
modifier.ratio = TARGET_TRIANGLES / source_triangles
modifier.use_collapse_triangulate = True
bpy.context.view_layer.objects.active = mesh_object
mesh_object.select_set(True)
bpy.ops.object.modifier_apply(modifier=modifier.name)
mesh_object.data.calc_loop_triangles()
final_triangles = len(mesh_object.data.loop_triangles)

# Capture the source atlas before replacing the imported material.
source_color = next(image for image in bpy.data.images if image.name.startswith("Color_"))
width, height = source_color.size
source_pixels = np.empty(width * height * 4, dtype=np.float32)
source_color.pixels.foreach_get(source_pixels)
source_pixels = source_pixels.reshape((height, width, 4))

# Core selection is the intersection of the physical central capsule volume and red UV samples.
mesh = mesh_object.data
uv_data = mesh.uv_layers.active.data
core_faces = []
for polygon in mesh.polygons:
    loop_indices = polygon.loop_indices
    uv_center = sum(
        (uv_data[index].uv for index in loop_indices),
        start=uv_data[loop_indices[0]].uv.copy() * 0.0,
    ) / len(loop_indices)
    px = min(width - 1, max(0, int(uv_center.x * width)))
    py = min(height - 1, max(0, int(uv_center.y * height)))
    color = source_pixels[py, px, :3]
    center = polygon.center
    is_red = color[0] > 0.22 and color[0] > color[1] * 1.35 and color[0] > color[2] * 1.18
    is_core_space = -0.19 <= center.x <= 0.19 and -0.065 <= center.z <= 0.065
    core_faces.append(bool(is_red and is_core_space))

# Bake a UV-aligned binary map. No helper geometry is retained.
bake_black = emission_material("_BakeBlack", (0.0, 0.0, 0.0, 1.0))
bake_white = emission_material("_BakeWhite", (1.0, 0.015, 0.002, 1.0))
emission_image = bpy.data.images.new(
    f"{ITEM_ID}_Emission",
    width=2048,
    height=2048,
    alpha=False,
    float_buffer=False,
)
emission_image.generated_color = (0.0, 0.0, 0.0, 1.0)
add_bake_target(bake_black, emission_image)
add_bake_target(bake_white, emission_image)
mesh.materials.clear()
mesh.materials.append(bake_black)
mesh.materials.append(bake_white)
for polygon, selected in zip(mesh.polygons, core_faces):
    polygon.material_index = int(selected)

scene = bpy.context.scene
scene.render.engine = "CYCLES"
scene.cycles.samples = 1
scene.render.bake.use_clear = True
scene.render.bake.margin = 12
bpy.ops.object.select_all(action="DESELECT")
mesh_object.select_set(True)
bpy.context.view_layer.objects.active = mesh_object
bpy.ops.object.bake(type="EMIT", margin=12)
emission_image.filepath_raw = str(emission_path)
emission_image.file_format = "PNG"
emission_image.save()

# Build one final PBR material with emission restricted by the baked map.
base_image = external_image(base_color_path, "sRGB")
normal_image = external_image(normal_path, "Non-Color")
orm_image = external_image(orm_path, "Non-Color")
emission_external = external_image(emission_path, "sRGB")
bpy.data.images.remove(emission_image)

material = bpy.data.materials.new(f"{ITEM_ID}_PBR")
material.use_nodes = True
nodes = material.node_tree.nodes
links = material.node_tree.links
nodes.clear()

output = nodes.new("ShaderNodeOutputMaterial")
principled = nodes.new("ShaderNodeBsdfPrincipled")
principled.inputs["Base Color"].default_value = (0.8, 0.8, 0.8, 1.0)
principled.inputs["Metallic"].default_value = 0.2
principled.inputs["Roughness"].default_value = 0.55
principled.inputs["Emission Strength"].default_value = 4.0
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
normal_map.inputs["Strength"].default_value = 1.0
links.new(normal_node.outputs["Color"], normal_map.inputs["Color"])
links.new(normal_map.outputs["Normal"], principled.inputs["Normal"])

emission_node = nodes.new("ShaderNodeTexImage")
emission_node.name = "EmissionMap"
emission_node.image = emission_external
links.new(emission_node.outputs["Color"], principled.inputs["Emission Color"])

mesh.materials.clear()
mesh.materials.append(material)
for polygon in mesh.polygons:
    polygon.material_index = 0

# Source X runs rearward; transform to Blender +Z muzzle, +Y up, then place RH at origin.
rh_source = Vector((0.405, 0.0, -0.125))
lh_source = Vector((-0.061, 0.0, -0.103))
muzzle_source = Vector((-0.5, 0.0, 0.0))
uniform_scale = LEFT_HAND_Z / (rh_source.x - lh_source.x)
source_to_target = Matrix(
    (
        (0.0, -1.0, 0.0, 0.0),
        (0.0, 0.0, 1.0, 0.0),
        (-1.0, 0.0, 0.0, 0.0),
        (0.0, 0.0, 0.0, 1.0),
    )
)
oriented_rh = (Matrix.Scale(uniform_scale, 4) @ source_to_target) @ rh_source
transform = Matrix.Translation(-oriented_rh) @ Matrix.Scale(uniform_scale, 4) @ source_to_target
mesh.transform(transform)
mesh.update()
mesh_object.location = (0.0, 0.0, 0.0)
mesh_object.rotation_euler = (0.0, 0.0, 0.0)
mesh_object.scale = (1.0, 1.0, 1.0)

lh_target = transform @ lh_source
muzzle_target = transform @ muzzle_source

root = bpy.data.objects.new(ITEM_ID, None)
bpy.context.scene.collection.objects.link(root)
root.empty_display_type = "PLAIN_AXES"
root.empty_display_size = 0.06
mesh_object.parent = root

right_grip = bpy.data.objects.new("RightHandGrip", None)
bpy.context.scene.collection.objects.link(right_grip)
right_grip.parent = root
right_grip.location = (0.0, 0.0, 0.0)
right_grip.rotation_euler = (math.radians(42.0), 0.0, 0.0)
right_grip.empty_display_type = "PLAIN_AXES"
right_grip.empty_display_size = 0.045

left_grip = bpy.data.objects.new("LeftHandGrip", None)
bpy.context.scene.collection.objects.link(left_grip)
left_grip.parent = root
left_grip.location = lh_target
left_grip.rotation_euler = (0.0, 0.0, 0.0)
left_grip.empty_display_type = "PLAIN_AXES"
left_grip.empty_display_size = 0.045

muzzle = bpy.data.objects.new("Muzzle", None)
bpy.context.scene.collection.objects.link(muzzle)
muzzle.parent = root
muzzle.location = muzzle_target
muzzle.rotation_euler = (0.0, 0.0, 0.0)
muzzle.empty_display_type = "ARROWS"
muzzle.empty_display_size = 0.05

# Surface proximity is measured against the final decimated mesh in root-local coordinates.
vertices = [vertex.co.copy() for vertex in mesh.vertices]
polygons = [list(polygon.vertices) for polygon in mesh.polygons]
bvh = BVHTree.FromPolygons(vertices, polygons, all_triangles=True)
rh_nearest = bvh.find_nearest(Vector((0.0, 0.0, 0.0)))
lh_nearest = bvh.find_nearest(lh_target)
muzzle_nearest = bvh.find_nearest(muzzle_target)

bpy.context.view_layer.update()
bounds = [vertex.co.copy() for vertex in mesh.vertices]
bounds_min = [min(point[index] for point in bounds) for index in range(3)]
bounds_max = [max(point[index] for point in bounds) for index in range(3)]

# Save the clean scene before FBX export; no cameras, lights, armatures or colliders exist.
scene.render.engine = "BLENDER_EEVEE"
for image, path in (
    (base_image, base_color_path),
    (normal_image, normal_path),
    (orm_image, orm_path),
    (emission_external, emission_path),
):
    portable_path = f"//../Textures/{path.name}"
    image.filepath = portable_path
    image.filepath_raw = portable_path
bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), compress=True)

# Keep portable relative paths in the authored .blend, but give the FBX exporter
# concrete source paths so COPY can place every supported texture beside the FBX.
for image, path in (
    (base_image, base_color_path),
    (normal_image, normal_path),
    (orm_image, orm_path),
    (emission_external, emission_path),
):
    absolute_path = str(path.resolve())
    image.filepath = absolute_path
    image.filepath_raw = absolute_path

bpy.ops.object.select_all(action="DESELECT")
for obj in (root, mesh_object, right_grip, left_grip, muzzle):
    obj.select_set(True)
bpy.context.view_layer.objects.active = root
bpy.ops.export_scene.fbx(
    filepath=str(fbx_path),
    use_selection=True,
    object_types={"EMPTY", "MESH"},
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z",
    axis_up="Y",
    add_leaf_bones=False,
    bake_anim=False,
    path_mode="COPY",
    use_mesh_modifiers=True,
)

report = {
    "item_id": ITEM_ID,
    "blender_version": bpy.app.version_string,
    "source_triangles": source_triangles,
    "candidate_triangles": {"100k": 99999, "final_55k": final_triangles},
    "selected_core_faces": int(sum(core_faces)),
    "uniform_scale": uniform_scale,
    "root": ITEM_ID,
    "mesh": mesh_object.name,
    "right_hand_grip": list(right_grip.location),
    "left_hand_grip": list(left_grip.location),
    "muzzle": list(muzzle.location),
    "surface_distance": {
        "RightHandGrip": float(rh_nearest[3]),
        "LeftHandGrip": float(lh_nearest[3]),
        "Muzzle": float(muzzle_nearest[3]),
    },
    "bounds_min": bounds_min,
    "bounds_max": bounds_max,
    "dimensions": [bounds_max[i] - bounds_min[i] for i in range(3)],
    "axis_contract": {"muzzle": "+Z", "up": "+Y"},
    "transforms": {"location": [0.0, 0.0, 0.0], "rotation": [0.0, 0.0, 0.0], "scale": [1.0, 1.0, 1.0]},
    "materials": [material.name],
    "textures": [str(path.relative_to(root_dir)) for path in (base_color_path, normal_path, orm_path, emission_path)],
    "outputs": {"blend": str(blend_path.relative_to(root_dir)), "fbx": str(fbx_path.relative_to(root_dir))},
}
report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(report, ensure_ascii=False, indent=2))
