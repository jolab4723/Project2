from __future__ import annotations

import argparse
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


VIEWS = {
    "front": Vector((0.0, 0.0, 1.0)),
    "back": Vector((0.0, 0.0, -1.0)),
    "left": Vector((-1.0, 0.0, 0.0)),
    "right": Vector((1.0, 0.0, 0.0)),
    "iso": Vector((-0.7, 0.62, 0.8)),
}


def v3(value) -> list[float]:
    return [round(float(value[index]), 6) for index in range(3)]


def triangle_count(obj: bpy.types.Object) -> int:
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def object_bounds(obj: bpy.types.Object) -> tuple[Vector, Vector]:
    points = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    return (
        Vector(tuple(min(point[index] for point in points) for index in range(3))),
        Vector(tuple(max(point[index] for point in points) for index in range(3))),
    )


def aim_camera(camera: bpy.types.Object, target: Vector) -> None:
    direction = (target - camera.location).normalized()
    up_hint = Vector((0.0, 1.0, 0.0))
    if abs(direction.dot(up_hint)) > 0.98:
        up_hint = Vector((0.0, 0.0, 1.0))
    right = direction.cross(up_hint).normalized()
    up = right.cross(direction).normalized()
    camera.rotation_mode = "QUATERNION"
    camera.rotation_quaternion = Matrix((right, up, -direction)).transposed().to_quaternion()


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--blend", type=Path, required=True)
    parser.add_argument("--qa", type=Path, required=True)
    parser.add_argument("--report", type=Path, required=True)
    script_args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    args = parser.parse_args(script_args)
    args.blend = args.blend.resolve()
    args.qa = args.qa.resolve()
    args.report = args.report.resolve()

    bpy.ops.wm.open_mainfile(filepath=str(args.blend))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError(f"Expected one mesh, got {len(meshes)}")
    body = meshes[0]
    root_objects = [obj for obj in bpy.context.scene.objects if obj.parent is None]
    body.data.calc_loop_triangles()
    center = sum((Vector(corner) for corner in body.bound_box), Vector()) / 8.0
    outward = inward = ambiguous = 0
    signed_volume = 0.0
    degenerate = 0
    for triangle in body.data.loop_triangles:
        p0, p1, p2 = (body.data.vertices[index].co for index in triangle.vertices)
        signed_volume += p0.dot(p1.cross(p2)) / 6.0
        area = (p1 - p0).cross(p2 - p0).length * 0.5
        if area <= 1e-14:
            degenerate += 1
        triangle_center = (p0 + p1 + p2) / 3.0
        direction = triangle_center - center
        score = triangle.normal.dot(direction)
        if score > 1e-7:
            outward += 1
        elif score < -1e-7:
            inward += 1
        else:
            ambiguous += 1

    minimum, maximum = object_bounds(body)
    world_center = (minimum + maximum) * 0.5
    longest = max(maximum - minimum)
    args.qa.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1200
    scene.render.resolution_y = 700
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = True
    camera_data = bpy.data.cameras.new("HandoffCullCamera")
    camera_data.type = "ORTHO"
    camera = bpy.data.objects.new("HandoffCullCamera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    temporary = [camera]
    for name, offset, energy in (("HandoffKey", (1.4, -1.6, 1.5), 900.0), ("HandoffFill", (-1.1, 1.2, 0.7), 450.0)):
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.shape = "DISK"
        data.size = 2.5
        light = bpy.data.objects.new(name, data)
        light.location = world_center + Vector(offset) * longest
        light.rotation_euler = (world_center - light.location).to_track_quat("-Z", "Y").to_euler()
        scene.collection.objects.link(light)
        temporary.append(light)
    world = bpy.data.worlds.new("HandoffCullWorld")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.025, 0.03, 0.04, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.35
    scene.world = world
    culling_properties = {}
    for material in body.data.materials:
        if material is None:
            continue
        if hasattr(material, "use_backface_culling"):
            culling_properties[material.name] = bool(material.use_backface_culling)
            material.use_backface_culling = True
    render_paths = {}
    distance = longest * 3.2
    for name, unit in VIEWS.items():
        camera.location = world_center + unit.normalized() * distance
        aim_camera(camera, world_center)
        camera.data.ortho_scale = longest * (1.34 if name == "iso" else 1.20)
        path = args.qa / f"culling_{name}.png"
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        render_paths[name] = str(path)
    for material in body.data.materials:
        if material and material.name in culling_properties:
            material.use_backface_culling = culling_properties[material.name]

    markers = {}
    for name in ("RightHandGrip", "LeftHandGrip", "Muzzle"):
        marker = bpy.data.objects.get(name)
        markers[name] = None if marker is None else {
            "parent": marker.parent.name if marker.parent else None,
            "local_location": v3(marker.location),
            "local_plus_z": v3(marker.matrix_local.to_3x3() @ Vector((0.0, 0.0, 1.0)))
        }
    total_oriented = outward + inward
    report = {
        "blend": str(args.blend.resolve()),
        "root_count": len(root_objects),
        "root_names": [obj.name for obj in root_objects],
        "mesh": body.name,
        "triangle_count": triangle_count(body),
        "signed_volume": signed_volume,
        "signed_volume_note": "Diagnostic only: the Tripo mesh contains multiple open/internal shells, so global sign does not determine visible exterior winding; five-view backface-culling renders are authoritative.",
        "degenerate_triangles": degenerate,
        "centroid_normal_heuristic": {
            "outward": outward,
            "inward": inward,
            "ambiguous": ambiguous,
            "outward_fraction": 0.0 if total_oriented == 0 else outward / total_oriented,
            "note": "Concave textured weapon; centroid heuristic is supplemental to five-view backface-culling renders"
        },
        "bounds": {"min": v3(minimum), "max": v3(maximum), "dimensions": v3(maximum - minimum)},
        "markers": markers,
        "culling_renders": render_paths,
        "checks": {
            "single_root": len(root_objects) == 1,
            "single_mesh": len(meshes) == 1,
            "nonzero_signed_volume_diagnostic": abs(signed_volume) > 1e-12,
            "no_degenerate_triangles": degenerate == 0,
            "required_markers": all(markers[name] is not None for name in markers),
            "muzzle_plus_z": markers["Muzzle"] is not None and markers["Muzzle"]["local_plus_z"] == [0.0, 0.0, 1.0],
            "culling_views_rendered": len(render_paths) == 5
        }
    }
    report["pass"] = all(report["checks"].values())
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
