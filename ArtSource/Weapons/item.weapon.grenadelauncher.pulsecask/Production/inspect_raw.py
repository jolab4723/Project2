from __future__ import annotations

import argparse
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def args_after_dash() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--item-root", type=Path, required=True)
    parser.add_argument("--raw-glb", type=Path)
    parser.add_argument("--qa-dir", type=Path)
    arguments = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    return parser.parse_args(arguments)


def bounds(meshes: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    points = [obj.matrix_world @ Vector(corner) for obj in meshes for corner in obj.bound_box]
    return (
        Vector(tuple(min(point[i] for point in points) for i in range(3))),
        Vector(tuple(max(point[i] for point in points) for i in range(3))),
    )


def look_at(camera: bpy.types.Object, target: Vector) -> None:
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()


def main() -> None:
    arguments = args_after_dash()
    item_root = arguments.item_root.resolve()
    item_id = item_root.name
    raw_glb = (
        arguments.raw_glb.resolve()
        if arguments.raw_glb
        else item_root / "Tripo" / "Downloaded" / f"{item_id}_raw.glb"
    )
    qa_dir = (
        arguments.qa_dir.resolve()
        if arguments.qa_dir
        else item_root / "Production" / "QA"
    )
    texture_dir = item_root / "Production" / "Textures"
    qa_dir.mkdir(parents=True, exist_ok=True)
    texture_dir.mkdir(parents=True, exist_ok=True)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(raw_glb))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if not meshes:
        raise RuntimeError("Raw GLB has no mesh")
    for image in bpy.data.images:
        if image.name.startswith("Color_"):
            destination = texture_dir / f"{item_id}_BaseColor.png"
            image.colorspace_settings.name = "sRGB"
        elif image.name.startswith("NormalGL_"):
            destination = texture_dir / f"{item_id}_Normal.png"
            image.colorspace_settings.name = "Non-Color"
        elif image.name.startswith("ORM_"):
            destination = texture_dir / f"{item_id}_ORM.png"
            image.colorspace_settings.name = "Non-Color"
        else:
            continue
        image.filepath_raw = str(destination)
        image.file_format = "PNG"
        image.save()
    minimum, maximum = bounds(meshes)
    center = (minimum + maximum) * 0.5
    dimensions = maximum - minimum
    muzzle_band = [
        obj.matrix_world @ vertex.co
        for obj in meshes
        for vertex in obj.data.vertices
        if (obj.matrix_world @ vertex.co).x <= minimum.x + 0.015
    ]
    muzzle_band_min = Vector(tuple(min(point[i] for point in muzzle_band) for i in range(3)))
    muzzle_band_max = Vector(tuple(max(point[i] for point in muzzle_band) for i in range(3)))

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1200
    scene.render.resolution_y = 700
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = True
    scene.view_settings.look = "AgX - Medium High Contrast"
    world = bpy.data.worlds.new("RawQAWorld")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.035, 0.04, 0.05, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.7
    scene.world = world

    camera_data = bpy.data.cameras.new("RawQACamera")
    camera = bpy.data.objects.new("RawQACamera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = max(dimensions) * 1.22

    for name, energy, size, direction in (
        ("Key", 1100.0, 5.0, Vector((1.5, -1.5, 2.2))),
        ("Fill", 700.0, 4.0, Vector((-1.5, 1.0, 1.1))),
    ):
        light_data = bpy.data.lights.new(name, "AREA")
        light_data.energy = energy
        light_data.shape = "DISK"
        light_data.size = size
        light = bpy.data.objects.new(name, light_data)
        light.location = center + direction * max(dimensions)
        light.rotation_euler = (center - light.location).to_track_quat("-Z", "Y").to_euler()
        scene.collection.objects.link(light)

    distance = max(dimensions) * 3.0
    views = {
        "pos_x": Vector((distance, 0, 0)),
        "neg_x": Vector((-distance, 0, 0)),
        "pos_y": Vector((0, distance, 0)),
        "neg_y": Vector((0, -distance, 0)),
        "pos_z": Vector((0, 0, distance)),
        "neg_z": Vector((0, 0, -distance)),
        "iso_pos": Vector((distance * 0.78, distance * 0.68, distance * 0.82)),
        "iso_neg": Vector((-distance * 0.78, -distance * 0.68, distance * 0.82)),
    }
    for name, offset in views.items():
        camera.location = center + offset
        look_at(camera, center)
        scene.render.filepath = str(qa_dir / f"raw_{name}.png")
        bpy.ops.render.render(write_still=True)

    material_records = []
    for material in bpy.data.materials:
        images = []
        if material.use_nodes and material.node_tree:
            for node in material.node_tree.nodes:
                if node.type == "TEX_IMAGE" and node.image:
                    images.append({
                        "name": node.image.name,
                        "size": list(node.image.size),
                        "colorspace": node.image.colorspace_settings.name,
                    })
        material_records.append({"name": material.name, "images": images})

    report = {
        "item_id": item_id,
        "raw_glb": str(raw_glb),
        "objects": [
            {
                "name": obj.name,
                "type": obj.type,
                "parent": obj.parent.name if obj.parent else None,
                "scale": [round(float(value), 6) for value in obj.scale],
            }
            for obj in bpy.context.scene.objects
            if obj.name not in {"RawQACamera", "Key", "Fill"}
        ],
        "mesh_count": len(meshes),
        "vertex_count": sum(len(obj.data.vertices) for obj in meshes),
        "triangle_count": sum(len(obj.data.loop_triangles) for obj in meshes),
        "bounds": {
            "min": [round(float(value), 6) for value in minimum],
            "max": [round(float(value), 6) for value in maximum],
            "dimensions": [round(float(value), 6) for value in dimensions],
            "longest_axis": "XYZ"[max(range(3), key=lambda index: dimensions[index])],
        },
        "raw_minus_x_muzzle_band": {
            "vertex_count": len(muzzle_band),
            "min": [round(float(value), 6) for value in muzzle_band_min],
            "max": [round(float(value), 6) for value in muzzle_band_max],
            "yz_center": [
                round(float((muzzle_band_min.y + muzzle_band_max.y) * 0.5), 6),
                round(float((muzzle_band_min.z + muzzle_band_max.z) * 0.5), 6),
            ],
        },
        "materials": material_records,
        "qa_views": [f"raw_{name}.png" for name in views],
    }
    (qa_dir / "raw_inspection.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
