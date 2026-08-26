from __future__ import annotations

from pathlib import Path

import bpy
from mathutils import Vector


PRODUCTION = Path(__file__).resolve().parent
QA = PRODUCTION / "QA" / "Unity"


def aim(camera: bpy.types.Object, target: Vector) -> None:
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()


def main() -> None:
    QA.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 768
    scene.render.resolution_y = 768
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = False
    scene.view_settings.look = "AgX - Medium High Contrast"

    world = scene.world or bpy.data.worlds.new("RailCarbine_Muzzle_QA_World")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.055, 0.085, 0.13, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.35

    target = Vector((0.0, 0.102, 0.205))
    camera_data = bpy.data.cameras.new("RailCarbine_Muzzle_QA_Camera")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 0.19
    camera = bpy.data.objects.new(camera_data.name, camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera

    for name, location, energy, size in (
        ("RailCarbine_Muzzle_Key", (0.16, 0.22, 0.5), 300.0, 0.3),
        ("RailCarbine_Muzzle_Fill", (-0.18, -0.02, 0.48), 180.0, 0.25),
    ):
        light_data = bpy.data.lights.new(name, "AREA")
        light_data.energy = energy
        light_data.shape = "DISK"
        light_data.size = size
        light = bpy.data.objects.new(name, light_data)
        light.location = location
        scene.collection.objects.link(light)
        aim(light, target)

    for filename, location in (
        ("railcarbine_actual_muzzle_front_close.png", Vector((0.0, 0.102, 0.56))),
        ("railcarbine_actual_muzzle_front_iso_close.png", Vector((0.13, 0.16, 0.53))),
    ):
        camera.location = location
        aim(camera, target)
        scene.render.filepath = str(QA / filename)
        bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    main()
