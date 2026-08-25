from __future__ import annotations

import json
import math
from collections import deque
from datetime import datetime, timezone
from pathlib import Path

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector


OUT = Path(__file__).resolve().parent
SOURCE = (
    OUT.parents[1]
    / "Tripo"
    / "CostSafeRetry20260825"
    / "Downloaded"
    / "item.weapon.rifle.railcarbine_raw.glb"
)
SCALE = 0.654880706921944
ROOT_RAW = Vector((0.17888563049853373, 0.0, -0.06940988239700376))
TARGETS = (300_000, 500_000)
FRONT_ZONE_Z = 0.39


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def v3(value) -> list[float]:
    return [round(float(value[index]), 7) for index in range(3)]


def triangles(obj: bpy.types.Object) -> int:
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def raw_to_final(point: Vector) -> Vector:
    delta = point - ROOT_RAW
    # Proper right-handed rotation: raw -X muzzle -> final +Z and raw +Z up
    # -> final +Y.  The historical +Y,+Z,-X mapping had determinant -1 and
    # reflected every triangle, which made the otherwise valid H3 caps and
    # shell disappear as soon as backface culling was enabled.
    return Vector((-delta.y, delta.z, -delta.x)) * SCALE


def transform_body(obj: bpy.types.Object) -> None:
    for vertex in obj.data.vertices:
        vertex.co = raw_to_final(Vector(vertex.co))
    obj.data.update()
    obj.location = (0.0, 0.0, 0.0)
    obj.rotation_euler = (0.0, 0.0, 0.0)
    obj.scale = (1.0, 1.0, 1.0)


def bounds(objects: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    points = [obj.matrix_world @ Vector(corner) for obj in objects for corner in obj.bound_box]
    minimum = Vector(tuple(min(point[index] for point in points) for index in range(3)))
    maximum = Vector(tuple(max(point[index] for point in points) for index in range(3)))
    return minimum, maximum


def component_count(bm: bmesh.types.BMesh) -> int:
    unvisited = set(bm.verts)
    count = 0
    while unvisited:
        count += 1
        queue = deque((unvisited.pop(),))
        while queue:
            vertex = queue.popleft()
            for edge in vertex.link_edges:
                other = edge.other_vert(vertex)
                if other in unvisited:
                    unvisited.remove(other)
                    queue.append(other)
    return count


def topology(obj: bpy.types.Object) -> dict:
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.verts.ensure_lookup_table()
    boundary = [edge for edge in bm.edges if edge.is_boundary]
    front_boundary = [
        edge
        for edge in boundary
        if min(float(edge.verts[0].co.z), float(edge.verts[1].co.z)) >= FRONT_ZONE_Z
    ]
    front_vertices = [vertex for edge in front_boundary for vertex in edge.verts]
    record = {
        "vertices": len(bm.verts),
        "triangles": triangles(obj),
        "boundary_edges": len(boundary),
        "non_manifold_edges": sum(1 for edge in bm.edges if not edge.is_manifold),
        "connected_components": component_count(bm),
        "front_zone_threshold_z": FRONT_ZONE_Z,
        "front_boundary_edges": len(front_boundary),
        "front_boundary_bounds": None,
        "uv_layers": [layer.name for layer in obj.data.uv_layers],
    }
    if front_vertices:
        record["front_boundary_bounds"] = {
            "min": v3(tuple(min(float(vertex.co[index]) for vertex in front_vertices) for index in range(3))),
            "max": v3(tuple(max(float(vertex.co[index]) for vertex in front_vertices) for index in range(3))),
        }
    bm.free()
    return record


def decimate(source: bpy.types.Object, target: int) -> bpy.types.Object:
    body = source.copy()
    body.data = source.data.copy()
    body.name = f"RailCarbine_LOD_{target // 1000}k"
    body.data.name = f"{body.name}_Mesh"
    bpy.context.scene.collection.objects.link(body)
    before = triangles(body)
    modifier = body.modifiers.new("Conservative_LOD_Decimate", "DECIMATE")
    modifier.decimate_type = "COLLAPSE"
    modifier.ratio = target / float(before)
    modifier.use_collapse_triangulate = True
    bpy.context.view_layer.objects.active = body
    body.select_set(True)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    body.select_set(False)
    if not obj_has_uv(body):
        raise RuntimeError(f"LOD {target} lost its UV map")
    return body


def obj_has_uv(obj: bpy.types.Object) -> bool:
    return obj.type == "MESH" and len(obj.data.uv_layers) > 0


def point_at(obj: bpy.types.Object, target: Vector, reference_up: Vector = Vector((0, 1, 0))) -> None:
    forward = (target - obj.location).normalized()
    if abs(forward.dot(reference_up)) > 0.98:
        reference_up = Vector((0, 0, 1))
    right = forward.cross(reference_up).normalized()
    corrected_up = right.cross(forward).normalized()
    obj.rotation_euler = Matrix((right, corrected_up, -forward)).transposed().to_euler()


def setup_render(center: Vector, longest: float):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1024
    scene.render.resolution_y = 512
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.render.film_transparent = False
    scene.view_settings.look = "AgX - Medium High Contrast"
    world = bpy.data.worlds.new("LOD_Gate_World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.018, 0.022, 0.030, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.35
    scene.world = world

    camera_data = bpy.data.cameras.new("LOD_Gate_Camera")
    camera_data.type = "ORTHO"
    camera = bpy.data.objects.new("LOD_Gate_Camera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera

    for name, direction, energy, size in (
        ("LOD_Key", Vector((-0.8, -1.0, 1.2)), 260.0, 2.5),
        ("LOD_Fill", Vector((1.0, -0.2, 0.3)), 125.0, 2.0),
        ("LOD_Rim", Vector((0.5, 1.0, 0.8)), 180.0, 1.8),
    ):
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.shape = "DISK"
        data.size = size
        light = bpy.data.objects.new(name, data)
        light.location = center + direction.normalized() * longest * 2.2
        point_at(light, center)
        scene.collection.objects.link(light)
    return scene, camera


def render(scene, camera, center, longest, direction, path, closeup=False) -> None:
    distance = longest * 3.0
    camera.location = center + direction.normalized() * distance
    point_at(camera, center)
    if closeup:
        scene.render.resolution_x = 1024
        scene.render.resolution_y = 1024
        camera.data.ortho_scale = 0.16
    else:
        scene.render.resolution_x = 1024
        scene.render.resolution_y = 512
        inverse = camera.matrix_world.inverted()
        # Bounds fit is intentionally generous and identical across culling passes.
        minimum, maximum = bounds([obj for obj in scene.objects if obj.type == "MESH" and not obj.hide_render])
        corners = [Vector((x, y, z)) for x in (minimum.x, maximum.x) for y in (minimum.y, maximum.y) for z in (minimum.z, maximum.z)]
        projected = [inverse @ point for point in corners]
        width = max(point.x for point in projected) - min(point.x for point in projected)
        height = max(point.y for point in projected) - min(point.y for point in projected)
        camera.data.ortho_scale = max(height * 1.20, width * 0.60)
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)


def load_pixels(path: Path) -> np.ndarray:
    image = bpy.data.images.load(str(path), check_existing=False)
    values = np.empty(image.size[0] * image.size[1] * 4, dtype=np.float32)
    image.pixels.foreach_get(values)
    array = values.reshape((image.size[1], image.size[0], 4)).copy()
    bpy.data.images.remove(image)
    return array


def image_delta(standard: Path, culled: Path) -> dict:
    first = load_pixels(standard)
    second = load_pixels(culled)
    absolute = np.abs(first[:, :, :3] - second[:, :, :3])
    per_pixel = absolute.max(axis=2)
    threshold = 2.0 / 255.0
    return {
        "pixels": int(per_pixel.size),
        "changed_pixels_gt_2_255": int((per_pixel > threshold).sum()),
        "changed_fraction": round(float((per_pixel > threshold).mean()), 9),
        "mean_abs_rgb_delta": round(float(absolute.mean()), 9),
        "max_abs_rgb_delta": round(float(absolute.max()), 9),
    }


def render_candidate(scene, camera, body, all_meshes, output, center, longest) -> dict:
    for mesh in all_meshes:
        mesh.hide_render = mesh is not body
    directions = {
        "left_side_minus_x": Vector((-1, 0, 0)),
        "right_side_plus_x": Vector((1, 0, 0)),
        "top_plus_y": Vector((0, 1, 0)),
        "bottom_minus_y": Vector((0, -1, 0)),
        "muzzle_plus_z": Vector((0, 0, 1)),
        "rear_minus_z": Vector((0, 0, -1)),
        "iso_muzzle_left_top": Vector((-1, 0.72, 1)).normalized(),
        "iso_rear_right_top": Vector((1, 0.72, -1)).normalized(),
    }
    records = {}
    for pass_name, culling in (("standard", False), ("backface_culled", True)):
        destination = output / pass_name
        destination.mkdir(parents=True, exist_ok=True)
        for material in bpy.data.materials:
            material.use_backface_culling = culling
        records[pass_name] = {}
        for name, direction in directions.items():
            path = destination / f"{name}.png"
            render(scene, camera, center, longest, direction, path)
            records[pass_name][name] = str(path)
        close_center = Vector((0.0, 0.045, 0.421))
        close_path = destination / "muzzle_close.png"
        render(scene, camera, close_center, longest, Vector((0, 0, 1)), close_path, closeup=True)
        records[pass_name]["muzzle_close"] = str(close_path)
    records["culling_delta"] = {
        name: image_delta(Path(records["standard"][name]), Path(records["backface_culled"][name]))
        for name in records["standard"]
    }
    return records


def main() -> None:
    qa = OUT / "QA" / "LODGate"
    qa.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(SOURCE))
    sources = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(sources) != 1:
        raise RuntimeError(f"Expected one raw mesh, got {len(sources)}")
    source = sources[0]
    transform_body(source)
    source.name = "RailCarbine_Raw_1411936"
    source_record = topology(source)
    candidates = {target: decimate(source, target) for target in TARGETS}
    all_meshes = [source, *candidates.values()]
    minimum, maximum = bounds([source])
    center = (minimum + maximum) * 0.5
    longest = max(maximum - minimum)
    scene, camera = setup_render(center, longest)

    candidate_records = {}
    # Render raw with the same camera once so image deltas share the same baseline.
    candidate_records["raw"] = {
        "topology": source_record,
        "renders": render_candidate(scene, camera, source, all_meshes, qa / "raw", center, longest),
    }
    for target, body in candidates.items():
        candidate_records[str(target)] = {
            "topology": topology(body),
            "reduction_fraction": round(1.0 - triangles(body) / float(triangles(source)), 9),
            "renders": render_candidate(scene, camera, body, all_meshes, qa / str(target), center, longest),
        }

    report = {
        "generated_at": utc_now(),
        "source": str(SOURCE),
        "method": "right-handed raw-to-final rotation followed by progressive Blender Collapse decimation; no normals recalculation and no repair geometry",
        "targets": list(TARGETS),
        "front_zone_z": FRONT_ZONE_Z,
        "candidates": candidate_records,
        "decision": "pending direct standard/backface visual review",
    }
    (OUT / "lod_candidate_evaluation.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT / "lod_candidate_evaluation.blend"))
    print(json.dumps({"source_triangles": triangles(source), "candidates": {str(target): triangles(obj) for target, obj in candidates.items()}}))


if __name__ == "__main__":
    main()
