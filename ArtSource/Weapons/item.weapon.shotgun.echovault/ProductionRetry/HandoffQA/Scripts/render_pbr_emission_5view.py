from __future__ import annotations

from pathlib import Path

import bpy
from mathutils import Matrix, Vector


HANDOFF = Path(__file__).resolve().parents[1]
PRODUCTION = HANDOFF.parent
BLEND = PRODUCTION / "item.weapon.shotgun.echovault.blend"
OUTPUT = HANDOFF / "Renders"


def aim(camera: bpy.types.Object, center: Vector, direction: Vector, world_up: Vector) -> None:
    camera.location = center + direction.normalized() * 3.0
    camera_back = direction.normalized()
    camera_right = world_up.cross(camera_back).normalized()
    camera_up = camera_back.cross(camera_right).normalized()
    camera.rotation_euler = Matrix(
        (camera_right, camera_up, camera_back)
    ).transposed().to_euler()


def fit(camera: bpy.types.Object, corners: list[Vector], aspect: float) -> None:
    bpy.context.view_layer.update()
    inverse = camera.matrix_world.inverted()
    projected = [inverse @ corner for corner in corners]
    width = max(point.x for point in projected) - min(point.x for point in projected)
    height = max(point.y for point in projected) - min(point.y for point in projected)
    camera.data.ortho_scale = max(height * 1.50, width * 1.50 / aspect)


def render_views(
    scene: bpy.types.Scene,
    camera: bpy.types.Object,
    center: Vector,
    corners: list[Vector],
    prefix: str,
) -> None:
    views = {
        "muzzle": (Vector((0.0, 0.0, 1.0)), Vector((0.0, 1.0, 0.0))),
        "stock": (Vector((0.0, 0.0, -1.0)), Vector((0.0, 1.0, 0.0))),
        "left": (Vector((-1.0, 0.0, 0.0)), Vector((0.0, 1.0, 0.0))),
        "right": (Vector((1.0, 0.0, 0.0)), Vector((0.0, 1.0, 0.0))),
        "top": (Vector((0.0, 1.0, 0.0)), Vector((0.0, 0.0, 1.0))),
    }
    aspect = scene.render.resolution_x / scene.render.resolution_y
    for name, (direction, world_up) in views.items():
        aim(camera, center, direction, world_up)
        fit(camera, corners, aspect)
        scene.render.filepath = str(OUTPUT / f"{prefix}_{name}.png")
        bpy.ops.render.render(write_still=True)
        print(f"HANDOFF_RENDER={scene.render.filepath}")


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(BLEND))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError(f"Expected one mesh, found {len(meshes)}")
    weapon = meshes[0]
    materials = list(weapon.data.materials)
    if len(materials) != 1 or materials[0] is None:
        raise RuntimeError("Expected one non-null material")
    material = materials[0]
    if hasattr(material, "use_backface_culling"):
        material.use_backface_culling = True

    corners = [weapon.matrix_world @ Vector(corner) for corner in weapon.bound_box]
    minimum = Vector((min(point[i] for point in corners) for i in range(3)))
    maximum = Vector((max(point[i] for point in corners) for i in range(3)))
    center = (minimum + maximum) * 0.5

    bpy.ops.object.camera_add()
    camera = bpy.context.object
    camera.data.type = "ORTHO"
    camera.data.lens = 70
    bpy.context.scene.camera = camera

    radius = max(maximum - minimum) * 3.0
    for offset, energy, size in (
        ((1.2, 1.4, 0.8), 850.0, 4.0),
        ((-0.8, 0.6, -1.0), 450.0, 3.0),
    ):
        location = center + Vector(offset) * radius
        bpy.ops.object.light_add(type="AREA", location=location)
        light = bpy.context.object
        light.data.energy = energy
        light.data.shape = "DISK"
        light.data.size = size
        light.rotation_euler = (center - location).to_track_quat("-Z", "Y").to_euler()

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 900
    scene.render.resolution_y = 600
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.render.film_transparent = False
    scene.view_settings.look = "AgX - Medium High Contrast"
    world = scene.world or bpy.data.worlds.new("HandoffWorld")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (
        0.004,
        0.006,
        0.008,
        1.0,
    )
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.08

    render_views(scene, camera, center, corners, "pbr")

    nodes = material.node_tree.nodes
    links = material.node_tree.links
    output = next(node for node in nodes if node.type == "OUTPUT_MATERIAL")
    emission_color = nodes.get("EmissionColor_x_BinaryMask")
    if emission_color is None:
        raise RuntimeError("EmissionColor_x_BinaryMask node is missing")
    for link in list(output.inputs["Surface"].links):
        links.remove(link)
    emission = nodes.new("ShaderNodeEmission")
    emission.name = "HandoffQA_EmissionOnly"
    emission.inputs["Strength"].default_value = 1.0
    links.new(emission_color.outputs["Color"], emission.inputs["Color"])
    links.new(emission.outputs["Emission"], output.inputs["Surface"])
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.0, 0.0, 0.0, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.0
    for light in [obj for obj in scene.objects if obj.type == "LIGHT"]:
        light.hide_render = True

    render_views(scene, camera, center, corners, "emission")
    print(f"BACKFACE_CULLING_QA={getattr(material, 'use_backface_culling', 'unsupported')}")
    print("SOURCE_BLEND_SAVED=False")


if __name__ == "__main__":
    main()
