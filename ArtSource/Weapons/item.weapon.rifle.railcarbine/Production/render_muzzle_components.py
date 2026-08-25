from __future__ import annotations

import colorsys
from pathlib import Path

import bpy
from mathutils import Vector


PRODUCTION = Path(__file__).resolve().parent
OUTPUT = PRODUCTION / "QA" / "Unity" / "railcarbine_actual_muzzle_components.png"


def aim(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def material(name: str, color: tuple[float, float, float, float]) -> bpy.types.Material:
    result = bpy.data.materials.new(name)
    result.diffuse_color = color
    result.use_nodes = True
    nodes = result.node_tree.nodes
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    emission = nodes.new("ShaderNodeEmission")
    emission.inputs["Color"].default_value = color
    emission.inputs["Strength"].default_value = 0.6
    result.node_tree.links.new(emission.outputs["Emission"], output.inputs["Surface"])
    return result


def main() -> None:
    body = bpy.data.objects["RailCarbine_Body"]
    mesh = body.data
    parent = list(range(len(mesh.vertices)))

    def find(index: int) -> int:
        while parent[index] != index:
            parent[index] = parent[parent[index]]
            index = parent[index]
        return index

    def union(left: int, right: int) -> None:
        left_root, right_root = find(left), find(right)
        if left_root != right_root:
            parent[right_root] = left_root

    for edge in mesh.edges:
        union(edge.vertices[0], edge.vertices[1])

    component_bounds: dict[int, list[float]] = {}
    for vertex in mesh.vertices:
        root = find(vertex.index)
        if root not in component_bounds:
            component_bounds[root] = [vertex.co.y, vertex.co.y, vertex.co.z, vertex.co.z]
        else:
            component_bounds[root][0] = min(component_bounds[root][0], vertex.co.y)
            component_bounds[root][1] = max(component_bounds[root][1], vertex.co.y)
            component_bounds[root][2] = min(component_bounds[root][2], vertex.co.z)
            component_bounds[root][3] = max(component_bounds[root][3], vertex.co.z)

    roots = sorted(
        (root for root, bounds in component_bounds.items() if bounds[2] < -0.22),
        key=lambda root: (component_bounds[root][0] + component_bounds[root][1]) * 0.5,
    )
    colors = [(*colorsys.hsv_to_rgb(index / max(1, len(roots)), 0.82, 0.95), 1.0) for index in range(len(roots))]
    body.data.materials.clear()
    body.data.materials.append(material("Other", (0.06, 0.07, 0.08, 1.0)))
    for index, root in enumerate(roots):
        body.data.materials.append(material(f"Component_{root}", colors[index]))

    root_to_slot = {root: index + 1 for index, root in enumerate(roots)}
    for polygon in mesh.polygons:
        polygon.material_index = root_to_slot.get(find(polygon.vertices[0]), 0)

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 768
    scene.render.resolution_y = 768
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    world = scene.world or bpy.data.worlds.new("Muzzle_Component_World")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.025, 0.025, 0.025, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.8

    target = Vector((0.0, 0.078, -0.255))
    camera_data = bpy.data.cameras.new("Muzzle_Component_Camera")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 0.19
    camera = bpy.data.objects.new(camera_data.name, camera_data)
    camera.location = (0.0, 0.078, -0.56)
    aim(camera, target)
    scene.collection.objects.link(camera)
    scene.camera = camera

    light_data = bpy.data.lights.new("Muzzle_Component_Key", "AREA")
    light_data.energy = 550.0
    light_data.size = 0.4
    light = bpy.data.objects.new(light_data.name, light_data)
    light.location = (-0.1, 0.2, -0.5)
    aim(light, target)
    scene.collection.objects.link(light)

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    scene.render.filepath = str(OUTPUT)
    bpy.ops.render.render(write_still=True)
    print({root: colors[index] for index, root in enumerate(roots)})


if __name__ == "__main__":
    main()
