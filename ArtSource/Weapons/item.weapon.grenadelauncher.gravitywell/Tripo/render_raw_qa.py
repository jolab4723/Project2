from pathlib import Path

import bpy
from mathutils import Matrix, Vector


ROOT = Path(__file__).resolve().parents[1]
GLB = (
    ROOT
    / "Tripo"
    / "Downloaded"
    / "H3"
    / "tripo-out"
    / "gravitywell-h3-2ef5edaf"
    / "model.glb"
)
OUT = ROOT / "Tripo" / "RawQA"


def orient(camera, target):
    direction = (target - camera.location).normalized()
    screen_up = Vector((0.0, 0.0, 1.0))
    if abs(direction.dot(screen_up)) > 0.98:
        screen_up = Vector((0.0, 1.0, 0.0))
    screen_right = direction.cross(screen_up).normalized()
    screen_up = screen_right.cross(direction).normalized()
    camera.matrix_world = Matrix((screen_right, screen_up, -direction)).transposed().to_4x4()
    camera.location = target - direction * (target - camera.location).length


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(GLB))
meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
points = [obj.matrix_world @ Vector(corner) for obj in meshes for corner in obj.bound_box]
minimum = Vector((min(point[i] for point in points) for i in range(3)))
maximum = Vector((max(point[i] for point in points) for i in range(3)))
center = (minimum + maximum) * 0.5
longest = max(maximum - minimum)

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1280
scene.render.resolution_y = 720
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.film_transparent = True
scene.view_settings.look = "AgX - Medium High Contrast"

camera_data = bpy.data.cameras.new("RawQA_Camera")
camera_data.type = "ORTHO"
camera_data.ortho_scale = longest * 1.28
camera = bpy.data.objects.new("RawQA_Camera", camera_data)
scene.collection.objects.link(camera)
scene.camera = camera

for name, location, energy, size in (
    ("Key", (1.6, -1.6, 2.0), 600, 2.0),
    ("Fill", (-1.5, -0.8, 0.8), 260, 1.8),
    ("Rim", (0.0, 1.8, 1.5), 380, 1.5),
):
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.size = size
    light = bpy.data.objects.new(name, data)
    scene.collection.objects.link(light)
    light.location = center + Vector(location) * longest
    light.rotation_euler = (center - light.location).to_track_quat("-Z", "Y").to_euler()

world = bpy.data.worlds.new("RawQA_World")
world.color = (0.008, 0.010, 0.016)
scene.world = world
OUT.mkdir(parents=True, exist_ok=True)

distance = longest * 3.0
views = {
    "side_minus_y": Vector((0.0, -distance, 0.0)),
    "side_plus_y": Vector((0.0, distance, 0.0)),
    "raw_plus_x": Vector((distance, 0.0, 0.0)),
    "raw_minus_x": Vector((-distance, 0.0, 0.0)),
    "top_plus_z": Vector((0.0, 0.0, distance)),
    "isometric": Vector((distance, -distance, distance * 0.75)),
}
for name, offset in views.items():
    camera.location = center + offset
    orient(camera, center)
    camera.data.ortho_scale = longest * (1.30 if "side" in name else 0.78)
    scene.render.filepath = str(OUT / f"{name}.png")
    bpy.ops.render.render(write_still=True)
