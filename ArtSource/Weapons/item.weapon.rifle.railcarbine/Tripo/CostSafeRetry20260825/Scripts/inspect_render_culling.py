import json
import sys
from collections import deque
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector


def argument(flag: str) -> Path:
    values = sys.argv[sys.argv.index("--") + 1 :]
    return Path(values[values.index(flag) + 1]).resolve()


def point_at(obj, target: Vector) -> None:
    forward = (target - obj.location).normalized()
    reference_up = Vector((0.0, 0.0, 1.0))
    if abs(forward.dot(reference_up)) > 0.98:
        reference_up = Vector((0.0, 1.0, 0.0))
    right = forward.cross(reference_up).normalized()
    corrected_up = right.cross(forward).normalized()
    obj.rotation_euler = Matrix((right, corrected_up, -forward)).transposed().to_euler()


def component_count(bm: bmesh.types.BMesh) -> int:
    unvisited = set(bm.verts)
    count = 0
    while unvisited:
        count += 1
        seed = unvisited.pop()
        queue = deque([seed])
        while queue:
            vertex = queue.popleft()
            for edge in vertex.link_edges:
                other = edge.other_vert(vertex)
                if other in unvisited:
                    unvisited.remove(other)
                    queue.append(other)
    return count


source = argument("--source")
output = argument("--output")
script_arguments = sys.argv[sys.argv.index("--") + 1 :]
final_axis = "--final-axis" in script_arguments
standard_dir = output / "standard"
culled_dir = output / "backface_culled"
standard_dir.mkdir(parents=True, exist_ok=True)
culled_dir.mkdir(parents=True, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
if source.suffix.lower() == ".fbx":
    bpy.ops.import_scene.fbx(filepath=str(source))
else:
    bpy.ops.import_scene.gltf(filepath=str(source))
bpy.context.view_layer.update()
meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
if not meshes:
    raise RuntimeError("No mesh imported")

corners = [obj.matrix_world @ Vector(corner) for obj in meshes for corner in obj.bound_box]
minimum = Vector(tuple(min(point[index] for point in corners) for index in range(3)))
maximum = Vector(tuple(max(point[index] for point in corners) for index in range(3)))
center = (minimum + maximum) * 0.5
diagonal = (maximum - minimum).length

mesh_records = []
for obj in meshes:
    mesh = obj.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    boundary_edges = sum(1 for edge in bm.edges if edge.is_boundary)
    non_manifold_edges = sum(1 for edge in bm.edges if not edge.is_manifold)
    components = component_count(bm)
    volume = None
    try:
        volume = float(bm.calc_volume(signed=True))
    except ValueError:
        pass
    bm.free()
    mesh_records.append(
        {
            "name": obj.name,
            "vertices": len(mesh.vertices),
            "triangles": sum(max(0, len(poly.vertices) - 2) for poly in mesh.polygons),
            "boundaryEdges": boundary_edges,
            "nonManifoldEdges": non_manifold_edges,
            "connectedComponents": components,
            "signedVolume": volume,
            "uvLayers": [layer.name for layer in mesh.uv_layers],
        }
    )

materials = []
for material in bpy.data.materials:
    images = []
    if material.use_nodes and material.node_tree:
        for node in material.node_tree.nodes:
            if node.type == "TEX_IMAGE" and node.image:
                images.append(
                    {
                        "node": node.name,
                        "image": node.image.name,
                        "size": list(node.image.size),
                        "colorspace": node.image.colorspace_settings.name,
                    }
                )
    materials.append({"name": material.name, "images": images})

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1024
scene.render.resolution_y = 512
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.image_settings.color_depth = "8"
scene.render.film_transparent = False
if scene.world is None:
    scene.world = bpy.data.worlds.new("RawGateWorld")
scene.world.color = (0.018, 0.022, 0.030)
scene.view_settings.look = "AgX - Medium High Contrast"

camera_data = bpy.data.cameras.new("RawGateCamera")
camera_data.type = "ORTHO"
camera = bpy.data.objects.new("RawGateCamera", camera_data)
scene.collection.objects.link(camera)
scene.camera = camera

for name, direction, energy, size in (
    ("Key", Vector((0.2, -1.0, 1.2)), 250.0, 2.5),
    ("Fill", Vector((-0.8, 0.8, 0.5)), 125.0, 2.0),
    ("Rim", Vector((1.0, 0.4, 1.0)), 180.0, 1.8),
):
    light_data = bpy.data.lights.new(name, type="AREA")
    light_data.energy = energy
    light_data.shape = "DISK"
    light_data.size = size
    light = bpy.data.objects.new(name, light_data)
    scene.collection.objects.link(light)
    light.location = center + direction.normalized() * diagonal * 2.2
    point_at(light, center)

if final_axis:
    directions = {
        "left_side_minus_x": Vector((-1.0, 0.0, 0.0)),
        "right_side_plus_x": Vector((1.0, 0.0, 0.0)),
        "top_plus_y": Vector((0.0, 1.0, 0.0)),
        "bottom_minus_y": Vector((0.0, -1.0, 0.0)),
        "muzzle_plus_z": Vector((0.0, 0.0, 1.0)),
        "rear_minus_z": Vector((0.0, 0.0, -1.0)),
        "iso_muzzle_left_top": Vector((-1.0, 0.72, 1.0)).normalized(),
        "iso_rear_right_top": Vector((1.0, 0.72, -1.0)).normalized(),
    }
else:
    directions = {
        "left_side_minus_y": Vector((0.0, -1.0, 0.0)),
        "right_side_plus_y": Vector((0.0, 1.0, 0.0)),
        "top_plus_z": Vector((0.0, 0.0, 1.0)),
        "bottom_minus_z": Vector((0.0, 0.0, -1.0)),
        "rear_plus_x": Vector((1.0, 0.0, 0.0)),
        "muzzle_minus_x": Vector((-1.0, 0.0, 0.0)),
        "iso_muzzle_left_top": Vector((-1.0, -1.0, 0.72)).normalized(),
        "iso_rear_right_top": Vector((1.0, 1.0, 0.72)).normalized(),
    }

view_records = {}
for pass_name, destination_dir, culling in (
    ("standard", standard_dir, False),
    ("backface_culled", culled_dir, True),
):
    for material in bpy.data.materials:
        material.use_backface_culling = culling
    pass_records = {}
    for name, direction in directions.items():
        camera.location = center + direction * diagonal * 3.0
        point_at(camera, center)
        if final_axis:
            forward = (center - camera.location).normalized()
            reference_up = Vector((0.0, 1.0, 0.0))
            if abs(forward.dot(reference_up)) > 0.98:
                reference_up = Vector((0.0, 0.0, 1.0))
            right = forward.cross(reference_up).normalized()
            corrected_up = right.cross(forward).normalized()
            camera.rotation_euler = Matrix((right, corrected_up, -forward)).transposed().to_euler()
        bpy.context.view_layer.update()
        inverse = camera.matrix_world.inverted()
        projected = [inverse @ point for point in corners]
        width = max(point.x for point in projected) - min(point.x for point in projected)
        height = max(point.y for point in projected) - min(point.y for point in projected)
        # Blender's orthographic scale is keyed to sensor height. Keeping the
        # full projected width here gives an intentionally generous QA margin
        # and prevents a long weapon from being clipped by camera-fit nuances.
        camera.data.ortho_scale = max(height * 1.18, width * 1.18)
        destination = destination_dir / f"{name}.png"
        scene.render.filepath = str(destination)
        bpy.ops.render.render(write_still=True)
        pass_records[name] = {
            "direction": [round(value, 6) for value in direction],
            "path": str(destination.relative_to(output)).replace("\\", "/"),
        }
    view_records[pass_name] = pass_records

(output / "raw_inspection.json").write_text(
    json.dumps(
        {
            "source": str(source),
            "coordinateMode": "final +Z muzzle/+Y up" if final_axis else "raw long-axis X/up Z",
            "meshCount": len(meshes),
            "meshes": mesh_records,
            "totalVertices": sum(record["vertices"] for record in mesh_records),
            "totalTriangles": sum(record["triangles"] for record in mesh_records),
            "boundsMin": [round(value, 7) for value in minimum],
            "boundsMax": [round(value, 7) for value in maximum],
            "dimensions": [round(value, 7) for value in maximum - minimum],
            "materials": materials,
            "views": view_records,
        },
        indent=2,
    ),
    encoding="utf-8",
)
print(json.dumps({"source": str(source), "meshes": len(meshes), "triangles": sum(r["triangles"] for r in mesh_records)}))
