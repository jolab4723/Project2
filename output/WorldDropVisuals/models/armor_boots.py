"""Deterministic paired armored boots world-drop marker."""

from __future__ import annotations

import math

import bpy

from world_drop_common import box, custom_mesh, join_and_finalize


ASSET_NAME = "WorldDrop_ArmorBoots"


def _extruded_outline(
    name: str,
    outline: list[tuple[float, float]],
    depth: float,
    y: float,
    material: bpy.types.Material,
    bevel: float,
    rotation_z: float = 0.0,
) -> bpy.types.Object:
    half_depth = depth * 0.5
    vertices = [(x, y - half_depth, z) for x, z in outline]
    vertices.extend((x, y + half_depth, z) for x, z in outline)
    count = len(outline)
    faces: list[tuple[int, ...]] = [
        tuple(reversed(range(count))),
        tuple(range(count, count * 2)),
    ]
    for index in range(count):
        next_index = (index + 1) % count
        faces.append((index, next_index, next_index + count, index + count))
    return custom_mesh(
        name,
        vertices,
        faces,
        material,
        rotation=(0.0, 0.0, rotation_z),
        bevel=bevel,
    )


def _build_boot(side: float, material: bpy.types.Material) -> list[bpy.types.Object]:
    suffix = "L" if side < 0.0 else "R"
    center_x = side * 0.135
    outward = -side * math.radians(4.0)
    parts: list[bpy.types.Object] = []

    ankle_outline = [
        (center_x - 0.078, -0.145),
        (center_x + 0.078, -0.145),
        (center_x + 0.070, 0.155),
        (center_x + 0.050, 0.245),
        (center_x - 0.058, 0.245),
        (center_x - 0.083, 0.115),
    ]
    parts.append(
        _extruded_outline(
            f"Boot_{suffix}_AnkleShell",
            ankle_outline,
            depth=0.185,
            y=0.015,
            material=material,
            bevel=0.016,
            rotation_z=outward,
        )
    )

    parts.append(
        box(
            f"Boot_{suffix}_ToeCap",
            size=(0.195, 0.285, 0.125),
            location=(center_x, -0.075, -0.175),
            material=material,
            rotation=(0.0, 0.0, outward),
            bevel=0.028,
        )
    )
    parts.append(
        box(
            f"Boot_{suffix}_Sole",
            size=(0.215, 0.305, 0.050),
            location=(center_x, -0.065, -0.252),
            material=material,
            rotation=(0.0, 0.0, outward),
            bevel=0.012,
        )
    )
    parts.append(
        box(
            f"Boot_{suffix}_Cuff",
            size=(0.185, 0.200, 0.065),
            location=(center_x, 0.015, 0.220),
            material=material,
            rotation=(0.0, 0.0, outward),
            bevel=0.014,
        )
    )

    for index, strap_z in enumerate((-0.020, 0.085), start=1):
        parts.append(
            box(
                f"Boot_{suffix}_Strap_{index}",
                size=(0.170, 0.025, 0.038),
                location=(center_x, -0.112, strap_z),
                material=material,
                rotation=(0.0, 0.0, outward),
                bevel=0.007,
            )
        )
    parts.append(
        box(
            f"Boot_{suffix}_ToeArmor",
            size=(0.145, 0.035, 0.070),
            location=(center_x, -0.225, -0.145),
            material=material,
            rotation=(math.radians(-8.0), 0.0, outward),
            bevel=0.012,
        )
    )
    return parts


def build(material: bpy.types.Material) -> bpy.types.Object:
    parts: list[bpy.types.Object] = []
    parts.extend(_build_boot(-1.0, material))
    parts.extend(_build_boot(1.0, material))
    return join_and_finalize(ASSET_NAME, parts, material)
