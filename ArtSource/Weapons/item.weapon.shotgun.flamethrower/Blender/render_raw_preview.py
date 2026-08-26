import sys
from pathlib import Path

import bpy
from mathutils import Vector


def cli_arg(name: str) -> str:
    args = sys.argv[sys.argv.index("--") + 1 :]
    return args[args.index(name) + 1]


def cli_optional(name: str, default=None):
    args = sys.argv[sys.argv.index("--") + 1 :]
    if name not in args:
        return default
    return args[args.index(name) + 1]


def point_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


source = Path(cli_arg("--source")).resolve()
output = Path(cli_arg("--output")).resolve()

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(source))

target_triangles = cli_optional("--target-triangles")
mesh_object = next(obj for obj in bpy.context.scene.objects if obj.type == "MESH")
mesh_object.data.calc_loop_triangles()
source_triangles = len(mesh_object.data.loop_triangles)
if target_triangles:
    target_triangles = int(target_triangles)
    modifier = mesh_object.modifiers.new("QA_Decimate", "DECIMATE")
    modifier.decimate_type = "COLLAPSE"
    modifier.ratio = min(1.0, target_triangles / source_triangles)
    modifier.use_collapse_triangulate = True
    bpy.context.view_layer.objects.active = mesh_object
    mesh_object.select_set(True)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
mesh_object.data.calc_loop_triangles()
print(f"triangles {source_triangles} -> {len(mesh_object.data.loop_triangles)}")

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1200
scene.render.resolution_y = 700
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.film_transparent = True
scene.render.filepath = str(output)
scene.view_settings.look = "AgX - Medium High Contrast"

camera_data = bpy.data.cameras.new("AuditCamera")
camera = bpy.data.objects.new("AuditCamera", camera_data)
scene.collection.objects.link(camera)
scene.camera = camera
camera.location = (0.0, -2.15, 0.08)
camera.data.type = "ORTHO"
camera.data.ortho_scale = 1.25
point_at(camera, Vector((0.0, 0.0, 0.0)))

for name, location, energy, size in (
    ("Key", (-0.5, -1.5, 1.4), 8.0, 3.0),
    ("Fill", (0.8, -0.8, 0.3), 4.0, 2.2),
    ("Rim", (0.0, 1.0, 1.0), 6.0, 2.0),
):
    light_data = bpy.data.lights.new(name, "AREA")
    light_data.energy = energy
    light_data.shape = "DISK"
    light_data.size = size
    light = bpy.data.objects.new(name, light_data)
    scene.collection.objects.link(light)
    light.location = location
    point_at(light, Vector((0.0, 0.0, 0.0)))

output.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.render.render(write_still=True)
print(output)
