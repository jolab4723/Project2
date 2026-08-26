from __future__ import annotations

import json
import os
from collections import deque
from math import cos, pi, sin
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector


ROOT = Path(__file__).resolve().parent
ATTEMPT = os.environ.get("WORLDENDER_H3_ATTEMPT", "1").strip()
TRIPO_FOLDER = "Tripo" if ATTEMPT == "1" else "TripoAttempt2"
QA_FOLDER = "Raw" if ATTEMPT == "1" else "RawAttempt2"
GLB = ROOT / TRIPO_FOLDER / "Downloaded" / "item.weapon.grenadelauncher.worldender_raw.glb"
OUT = ROOT / "QA" / QA_FOLDER


def rounded(vector, digits=6):
    return [round(float(value), digits) for value in vector]


def orient(camera, target):
    direction = (target - camera.location).normalized()
    screen_up = Vector((0.0, 0.0, 1.0))
    if abs(direction.dot(screen_up)) > 0.98:
        screen_up = Vector((0.0, 1.0, 0.0))
    screen_right = direction.cross(screen_up).normalized()
    screen_up = screen_right.cross(direction).normalized()
    camera.matrix_world = Matrix((screen_right, screen_up, -direction)).transposed().to_4x4()
    camera.location = target - direction * (target - camera.location).length


def component_report(mesh):
    adjacency = [[] for _ in mesh.vertices]
    for edge in mesh.edges:
        a, b = edge.vertices
        adjacency[a].append(b)
        adjacency[b].append(a)
    unseen = set(range(len(mesh.vertices)))
    sizes = []
    while unseen:
        seed = unseen.pop()
        queue = deque([seed])
        size = 0
        while queue:
            index = queue.popleft()
            size += 1
            for neighbor in adjacency[index]:
                if neighbor in unseen:
                    unseen.remove(neighbor)
                    queue.append(neighbor)
        sizes.append(size)
    sizes.sort(reverse=True)
    return {
        "count": len(sizes),
        "vertex_sizes_top_20": sizes[:20],
        "largest_vertex_ratio": round(sizes[0] / len(mesh.vertices), 6) if sizes else 0.0,
    }


def topology_report(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    result = {
        "vertices": len(bm.verts),
        "edges": len(bm.edges),
        "faces": len(bm.faces),
        "boundary_edges": sum(1 for edge in bm.edges if edge.is_boundary),
        "non_manifold_edges_including_boundaries": sum(1 for edge in bm.edges if not edge.is_manifold),
        "loose_edges": sum(1 for edge in bm.edges if not edge.link_faces),
        "degenerate_faces": sum(1 for face in bm.faces if face.calc_area() < 1.0e-10),
    }
    bm.free()
    return result


def radial_probe(obj, x_levels, radius=0.35, samples=24):
    levels = []
    for x_value in x_levels:
        distances = []
        misses = []
        for index in range(samples):
            angle = 2.0 * pi * index / samples
            direction = Vector((0.0, cos(angle), sin(angle)))
            hit, location, _normal, _face = obj.ray_cast(
                Vector((x_value, 0.0, 0.0)), direction, distance=radius
            )
            if hit:
                distances.append(round(float((location - Vector((x_value, 0.0, 0.0))).length), 6))
            else:
                misses.append(index)
        levels.append(
            {
                "raw_x": round(x_value, 6),
                "hits": samples - len(misses),
                "samples": samples,
                "miss_indices": misses,
                "min_hit_distance": min(distances) if distances else None,
                "max_hit_distance": max(distances) if distances else None,
            }
        )
    return levels


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(GLB))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError(f"Expected one H3 mesh object, got {len(meshes)}")
    obj = meshes[0]
    obj.data.calc_loop_triangles()
    points = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    minimum = Vector((min(point[i] for point in points) for i in range(3)))
    maximum = Vector((max(point[i] for point in points) for i in range(3)))
    center = (minimum + maximum) * 0.5
    longest = max(maximum - minimum)

    muzzle_origin = Vector((minimum.x - 0.02, 0.0, 0.0))
    hit, location, normal, face = obj.ray_cast(muzzle_origin, Vector((1.0, 0.0, 0.0)), distance=2.0)
    clear_depth = float(location.x - minimum.x) if hit else 2.0
    x_levels = [minimum.x + offset for offset in (0.005, 0.035, 0.075)]
    report = {
        "source": str(GLB.relative_to(ROOT)),
        "raw_coordinate_assumption": {"muzzle": "-X", "stock": "+X", "up": "+Z"},
        "bounds": {"min": rounded(minimum), "max": rounded(maximum), "dimensions": rounded(maximum - minimum)},
        "triangles": len(obj.data.loop_triangles),
        "topology": topology_report(obj),
        "connected_components": component_report(obj.data),
        "muzzle_axis_probe": {
            "origin": rounded(muzzle_origin),
            "direction": [1.0, 0.0, 0.0],
            "hit": bool(hit),
            "first_hit": rounded(location) if hit else None,
            "first_hit_normal": rounded(normal) if hit else None,
            "first_hit_face": int(face),
            "clear_depth_from_muzzle_plane_m": round(clear_depth, 6),
            "minimum_required_m": 0.08,
            "passes": clear_depth >= 0.08,
        },
        "muzzle_radial_probe": radial_probe(obj, x_levels),
    }
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / "raw_geometry_probe.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8"
    )

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 900
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = True
    scene.view_settings.look = "AgX - Medium High Contrast"
    camera_data = bpy.data.cameras.new("RawQA_Camera")
    camera_data.type = "ORTHO"
    camera = bpy.data.objects.new("RawQA_Camera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera

    for name, location, energy, size in (
        ("Key", (1.6, -1.6, 2.0), 700, 2.0),
        ("Fill", (-1.5, -0.8, 0.8), 300, 1.8),
        ("Rim", (0.0, 1.8, 1.5), 420, 1.5),
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

    distance = longest * 3.0
    views = {
        "muzzle_minus_x": Vector((-distance, 0.0, 0.0)),
        "stock_plus_x": Vector((distance, 0.0, 0.0)),
        "left_side_minus_y": Vector((0.0, -distance, 0.0)),
        "right_side_plus_y": Vector((0.0, distance, 0.0)),
        "top_plus_z": Vector((0.0, 0.0, distance)),
        "bottom_minus_z": Vector((0.0, 0.0, -distance)),
        "iso_muzzle_left_high": Vector((-distance, -distance, distance * 0.7)),
        "iso_muzzle_right_high": Vector((-distance, distance, distance * 0.7)),
        "iso_stock_left_high": Vector((distance, -distance, distance * 0.7)),
        "iso_stock_right_high": Vector((distance, distance, distance * 0.7)),
    }
    for cull in (False, True):
        for material in bpy.data.materials:
            material.use_backface_culling = cull
        folder = OUT / ("renders_culled" if cull else "renders")
        folder.mkdir(parents=True, exist_ok=True)
        for name, offset in views.items():
            camera.location = center + offset
            orient(camera, center)
            camera.data.ortho_scale = longest * (0.78 if name in {"muzzle_minus_x", "stock_plus_x"} else 1.25)
            scene.render.filepath = str(folder / f"{name}.png")
            bpy.ops.render.render(write_still=True)

    solid = bpy.data.materials.new("RawQA_OpaqueSolid")
    solid.diffuse_color = (0.32, 0.36, 0.42, 1.0)
    solid.use_nodes = True
    principled = solid.node_tree.nodes.get("Principled BSDF")
    principled.inputs["Base Color"].default_value = (0.32, 0.36, 0.42, 1.0)
    principled.inputs["Metallic"].default_value = 0.25
    principled.inputs["Roughness"].default_value = 0.38
    obj.data.materials.clear()
    obj.data.materials.append(solid)
    folder = OUT / "renders_solid"
    folder.mkdir(parents=True, exist_ok=True)
    for name, offset in views.items():
        camera.location = center + offset
        orient(camera, center)
        camera.data.ortho_scale = longest * (0.78 if name in {"muzzle_minus_x", "stock_plus_x"} else 1.25)
        scene.render.filepath = str(folder / f"{name}.png")
        bpy.ops.render.render(write_still=True)

    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
