import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector


def cli_arg(name: str) -> str:
    args = sys.argv[sys.argv.index("--") + 1 :]
    return args[args.index(name) + 1]


def point_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


source = Path(cli_arg("--source")).resolve()
output = Path(cli_arg("--output")).resolve()

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(source))
obj = next(item for item in bpy.context.scene.objects if item.type == "MESH")
obj.data.calc_loop_triangles()
modifier = obj.modifiers.new("Preview55k", "DECIMATE")
modifier.ratio = 55000 / len(obj.data.loop_triangles)
modifier.use_collapse_triangulate = True
bpy.context.view_layer.objects.active = obj
obj.select_set(True)
bpy.ops.object.modifier_apply(modifier=modifier.name)

mesh = obj.data
uv = mesh.uv_layers.active.data
image = next(image for image in bpy.data.images if image.name.startswith("Color_"))
width, height = image.size
pixels = np.empty(width * height * 4, dtype=np.float32)
image.pixels.foreach_get(pixels)
pixels = pixels.reshape((height, width, 4))

black = bpy.data.materials.new("MaskBlack")
black.diffuse_color = (0.0, 0.0, 0.0, 1.0)
black.use_nodes = True
black.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.0, 0.0, 0.0, 1.0)

white = bpy.data.materials.new("MaskWhite")
white.use_nodes = True
principled = white.node_tree.nodes["Principled BSDF"]
principled.inputs["Base Color"].default_value = (1.0, 0.02, 0.0, 1.0)
principled.inputs["Emission Color"].default_value = (1.0, 0.0, 0.0, 1.0)
principled.inputs["Emission Strength"].default_value = 8.0

obj.data.materials.clear()
obj.data.materials.append(black)
obj.data.materials.append(white)
selected = 0
for polygon in mesh.polygons:
    loop_indices = polygon.loop_indices
    uv_center = sum((uv[index].uv for index in loop_indices), start=uv[loop_indices[0]].uv.copy() * 0.0) / len(loop_indices)
    px = min(width - 1, max(0, int(uv_center.x * width)))
    py = min(height - 1, max(0, int(uv_center.y * height)))
    color = pixels[py, px, :3]
    center = polygon.center
    is_red = color[0] > 0.22 and color[0] > color[1] * 1.35 and color[0] > color[2] * 1.18
    is_core_space = -0.19 <= center.x <= 0.19 and -0.065 <= center.z <= 0.065
    polygon.material_index = 1 if is_red and is_core_space else 0
    selected += int(polygon.material_index == 1)

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1200
scene.render.resolution_y = 700
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.film_transparent = False
scene.render.filepath = str(output)
scene.world = bpy.data.worlds.new("MaskWorld")
scene.world.color = (0.0, 0.0, 0.0)

camera_data = bpy.data.cameras.new("MaskCamera")
camera = bpy.data.objects.new("MaskCamera", camera_data)
scene.collection.objects.link(camera)
scene.camera = camera
camera.location = (0.0, -2.15, 0.08)
camera.data.type = "ORTHO"
camera.data.ortho_scale = 1.25
point_at(camera, Vector((0.0, 0.0, 0.0)))

output.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.render.render(write_still=True)
print(f"selected_core_faces={selected}")
