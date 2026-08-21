from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parent
MODEL_PATH = ROOT / "Tripo" / "Downloaded" / "ITM_WPN_GNR_0013_P1_raw.glb"
OUTPUT_DIR = ROOT / "QA" / "P1Raw" / "Renders"


def look_at(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def scene_bounds(mesh_objects):
    points = [obj.matrix_world @ Vector(corner) for obj in mesh_objects for corner in obj.bound_box]
    minimum = Vector((min(point.x for point in points), min(point.y for point in points), min(point.z for point in points)))
    maximum = Vector((max(point.x for point in points), max(point.y for point in points), max(point.z for point in points)))
    return minimum, maximum, (minimum + maximum) * 0.5


def add_area_light(name, location, energy, size, center):
    data = bpy.data.lights.new(name=name, type="AREA")
    data.energy = energy
    data.shape = "DISK"
    data.size = size
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = location
    look_at(obj, center)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(MODEL_PATH))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    minimum, maximum, center = scene_bounds(meshes)
    extent = maximum - minimum
    longest = max(extent)

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1024
    scene.render.resolution_y = 1024
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = True
    scene.render.image_settings.color_depth = "8"
    scene.view_settings.look = "AgX - Medium High Contrast"

    camera_data = bpy.data.cameras.new("QA_Camera")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = longest * 1.28
    camera = bpy.data.objects.new("QA_Camera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera

    add_area_light("Key", center + Vector((1.6, -1.8, 2.2)) * longest, 850, longest * 2.0, center)
    add_area_light("Fill", center + Vector((-1.2, -0.9, 0.8)) * longest, 500, longest * 1.8, center)
    add_area_light("Rim", center + Vector((0.2, 1.8, 1.5)) * longest, 700, longest * 1.5, center)
    scene.world = bpy.data.worlds.new("QA_World")
    scene.world.color = (0.025, 0.025, 0.025)

    distance = longest * 3.0
    views = {
        "muzzle_front": Vector((distance, 0.0, 0.0)),
        "rear": Vector((-distance, 0.0, 0.0)),
        "left_side": Vector((0.0, -distance, 0.0)),
        "right_side": Vector((0.0, distance, 0.0)),
        "top": Vector((0.0, 0.0, distance)),
        "isometric": Vector((distance, -distance, distance * 0.72)),
    }

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    for name, offset in views.items():
        camera.location = center + offset
        look_at(camera, center)
        if name in {"muzzle_front", "rear"}:
            camera.data.ortho_scale = max(extent.y, extent.z) * 1.35
        elif name == "top":
            camera.data.ortho_scale = max(extent.x, extent.y) * 1.22
        else:
            camera.data.ortho_scale = max(extent.x, extent.z) * 1.22
        scene.render.filepath = str(OUTPUT_DIR / f"{name}.png")
        bpy.ops.render.render(write_still=True)
        print(scene.render.filepath)


if __name__ == "__main__":
    main()
