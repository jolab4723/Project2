from __future__ import annotations

from pathlib import Path

import bpy
from mathutils import Vector


PRODUCTION = Path(__file__).resolve().parent
OUTPUT = PRODUCTION / "QA" / "Unity" / "railcarbine_muzzle_target.png"


def aim(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def flat_material(name: str, color: tuple[float, float, float, float]) -> bpy.types.Material:
    result = bpy.data.materials.new(name)
    result.use_nodes = True
    nodes = result.node_tree.nodes
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    emission = nodes.new("ShaderNodeEmission")
    emission.inputs["Color"].default_value = color
    emission.inputs["Strength"].default_value = 0.7
    result.node_tree.links.new(emission.outputs["Emission"], output.inputs["Surface"])
    return result


def is_lower_muzzle_face(polygon: bpy.types.MeshPolygon) -> bool:
    center = polygon.center
    return (
        abs(center.x) <= 0.014
        and 0.027 <= center.y <= 0.064
        and center.z >= 0.505
        and polygon.normal.z >= 0.72
    )


def main() -> None:
    body = bpy.data.objects["RailCarbine_Body"]
    mesh = body.data
    mesh.materials.clear()
    mesh.materials.append(flat_material("Untouched", (0.035, 0.045, 0.055, 1.0)))
    mesh.materials.append(flat_material("Lower_Muzzle_Target", (0.08, 0.9, 0.95, 1.0)))
    selected = []
    for polygon in mesh.polygons:
        if is_lower_muzzle_face(polygon):
            polygon.material_index = 1
            selected.append(polygon.index)
        else:
            polygon.material_index = 0

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 768
    scene.render.resolution_y = 768
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    world = scene.world or bpy.data.worlds.new("Muzzle_Target_World")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.015, 0.015, 0.015, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.5

    target = Vector((0.0, 0.078, 0.507))
    camera_data = bpy.data.cameras.new("Muzzle_Target_Camera")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 0.19
    camera = bpy.data.objects.new(camera_data.name, camera_data)
    camera.location = (0.0, 0.078, 0.82)
    aim(camera, target)
    scene.collection.objects.link(camera)
    scene.camera = camera

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    scene.render.filepath = str(OUTPUT)
    bpy.ops.render.render(write_still=True)
    print({"selected_polygon_count": len(selected), "selected_polygons": selected})


if __name__ == "__main__":
    main()
