"""Deterministic industrial helmet world-drop marker."""

from __future__ import annotations

import math

import bpy

from world_drop_common import box, custom_mesh, cylinder, join_and_finalize, sphere, torus


ASSET_NAME = "WorldDrop_ArmorHelmet"


def _extruded_outline(
    name: str,
    outline: list[tuple[float, float]],
    depth: float,
    y: float,
    material: bpy.types.Material,
    bevel: float,
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
    return custom_mesh(name, vertices, faces, material, bevel=bevel)


def build(material: bpy.types.Material) -> bpy.types.Object:
    parts: list[bpy.types.Object] = []

    parts.append(
        sphere(
            "Helmet_MainShell",
            radius=1.0,
            scale=(0.285, 0.205, 0.245),
            location=(0.0, 0.0, 0.055),
            material=material,
            segments=20,
            rings=10,
        )
    )

    # Wide angular visor, brow rail, and separated cheek guards make the front
    # unmistakably read as protective headgear.
    parts.append(
        _extruded_outline(
            "Helmet_VisorPlate",
            [
                (-0.215, 0.165),
                (-0.165, 0.215),
                (0.165, 0.215),
                (0.215, 0.165),
                (0.185, 0.065),
                (-0.185, 0.065),
            ],
            depth=0.050,
            y=-0.205,
            material=material,
            bevel=0.012,
        )
    )
    parts.append(
        box(
            "Helmet_BrowRail",
            size=(0.390, 0.055, 0.042),
            location=(0.0, -0.205, 0.215),
            material=material,
            bevel=0.010,
        )
    )

    right_cheek = [
        (0.090, 0.060),
        (0.235, 0.070),
        (0.260, -0.020),
        (0.225, -0.155),
        (0.135, -0.205),
        (0.080, -0.120),
    ]
    left_cheek = [(-x, z) for x, z in reversed(right_cheek)]
    parts.append(_extruded_outline("Helmet_CheekGuard_R", right_cheek, 0.055, -0.198, material, 0.013))
    parts.append(_extruded_outline("Helmet_CheekGuard_L", left_cheek, 0.055, -0.198, material, 0.013))
    parts.append(
        box(
            "Helmet_ChinBridge",
            size=(0.235, 0.070, 0.060),
            location=(0.0, -0.185, -0.185),
            material=material,
            bevel=0.014,
        )
    )

    for side in (-1.0, 1.0):
        parts.append(
            cylinder(
                "Helmet_EarPod_L" if side < 0.0 else "Helmet_EarPod_R",
                radius=0.072,
                depth=0.065,
                location=(side * 0.282, 0.0, 0.015),
                material=material,
                rotation=(0.0, math.pi * 0.5, 0.0),
                vertices=16,
                bevel=0.008,
            )
        )
        parts.append(
            torus(
                "Helmet_EarRing_L" if side < 0.0 else "Helmet_EarRing_R",
                major_radius=0.058,
                minor_radius=0.008,
                location=(side * 0.318, 0.0, 0.015),
                material=material,
                rotation=(0.0, math.pi * 0.5, 0.0),
                major_segments=12,
                minor_segments=4,
            )
        )

    parts.append(
        box(
            "Helmet_CrownRidge",
            size=(0.070, 0.210, 0.155),
            location=(0.0, 0.020, 0.245),
            material=material,
            bevel=0.018,
        )
    )
    parts.append(
        torus(
            "Helmet_NeckSeal",
            major_radius=0.160,
            minor_radius=0.018,
            location=(0.0, 0.0, -0.175),
            material=material,
            major_segments=18,
            minor_segments=5,
        )
    )

    return join_and_finalize(ASSET_NAME, parts, material)
