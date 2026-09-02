"""Deterministic potion marker based on the current Project2 potion icons.

The icon language is preserved through a round flask silhouette, short cork,
metal neck ring, descending chain links, and a front medal.  The runtime
material remains neutral white so rarity tint can color the full model.
"""

from __future__ import annotations

import math

import bpy

from world_drop_common import box, cylinder, custom_mesh, join_and_finalize, torus


ASSET_NAME = "WorldDrop_Potion"
BODY_SEGMENTS = 24


def _lathe(
    name: str,
    profile: list[tuple[float, float]],
    segments: int,
    material: bpy.types.Material,
) -> bpy.types.Object:
    """Create a capped rotational profile around the Blender Z axis."""

    vertices: list[tuple[float, float, float]] = []
    for z, radius in profile:
        for index in range(segments):
            angle = (2.0 * math.pi * index) / segments
            vertices.append((radius * math.cos(angle), radius * math.sin(angle), z))

    faces: list[tuple[int, ...]] = []
    for ring in range(len(profile) - 1):
        first = ring * segments
        second = (ring + 1) * segments
        for index in range(segments):
            next_index = (index + 1) % segments
            faces.append((first + index, first + next_index, second + next_index, second + index))

    faces.append(tuple(reversed(range(segments))))
    top_start = (len(profile) - 1) * segments
    faces.append(tuple(top_start + index for index in range(segments)))
    return custom_mesh(name, vertices, faces, material, bevel=0.0)


def build(material: bpy.types.Material) -> bpy.types.Object:
    parts: list[bpy.types.Object] = []

    # Smooth, nearly spherical flask with a small flattened contact patch and
    # a pronounced shoulder taper matching the red/blue inventory icons.
    parts.append(
        _lathe(
            "Potion_RoundFlask",
            [
                (-0.235, 0.070),
                (-0.225, 0.145),
                (-0.195, 0.195),
                (-0.145, 0.225),
                (-0.075, 0.242),
                (0.010, 0.247),
                (0.095, 0.238),
                (0.165, 0.215),
                (0.215, 0.175),
                (0.245, 0.112),
                (0.252, 0.073),
            ],
            BODY_SEGMENTS,
            material,
        )
    )

    # A restrained rounded foot replaces the old broad slab base.
    parts.append(
        _lathe(
            "Potion_GlassFoot",
            [
                (-0.258, 0.078),
                (-0.257, 0.125),
                (-0.248, 0.155),
                (-0.232, 0.162),
                (-0.220, 0.145),
                (-0.218, 0.085),
            ],
            18,
            material,
        )
    )

    parts.append(
        _lathe(
            "Potion_ShortNeck",
            [
                (0.242, 0.074),
                (0.255, 0.070),
                (0.270, 0.062),
                (0.325, 0.062),
                (0.336, 0.069),
            ],
            16,
            material,
        )
    )
    parts.append(
        torus(
            "Potion_MetalNeckRing",
            major_radius=0.071,
            minor_radius=0.011,
            location=(0.0, 0.0, 0.292),
            material=material,
            major_segments=16,
            minor_segments=5,
        )
    )

    # Short, broad cork with a softly tapered crown.
    parts.append(
        _lathe(
            "Potion_Cork",
            [
                (0.325, 0.067),
                (0.335, 0.074),
                (0.375, 0.076),
                (0.407, 0.068),
                (0.417, 0.056),
            ],
            16,
            material,
        )
    )

    # Three visible links descend from the neck to the front-right medal.
    for index, (x, z, rotation_z) in enumerate(
        ((0.060, 0.235, -18.0), (0.095, 0.178, -28.0), (0.122, 0.120, -18.0)),
        start=1,
    ):
        parts.append(
            torus(
                f"Potion_ChainLink_{index}",
                major_radius=0.025,
                minor_radius=0.005,
                location=(x, -0.236, z),
                material=material,
                rotation=(math.pi * 0.5, 0.0, math.radians(rotation_z)),
                major_segments=10,
                minor_segments=4,
            )
        )

    medal_center = (0.145, -0.251, 0.045)
    parts.append(
        cylinder(
            "Potion_MedalDisc",
            radius=0.058,
            depth=0.018,
            location=medal_center,
            material=material,
            rotation=(math.pi * 0.5, 0.0, 0.0),
            vertices=16,
            bevel=0.004,
        )
    )
    parts.append(
        torus(
            "Potion_MedalRim",
            major_radius=0.050,
            minor_radius=0.006,
            location=(medal_center[0], medal_center[1] - 0.012, medal_center[2]),
            material=material,
            rotation=(math.pi * 0.5, 0.0, 0.0),
            major_segments=16,
            minor_segments=4,
        )
    )
    parts.append(
        box(
            "Potion_MedalCrossVertical",
            size=(0.022, 0.012, 0.063),
            location=(medal_center[0], medal_center[1] - 0.023, medal_center[2]),
            material=material,
            bevel=0.004,
        )
    )
    parts.append(
        box(
            "Potion_MedalCrossHorizontal",
            size=(0.063, 0.012, 0.022),
            location=(medal_center[0], medal_center[1] - 0.023, medal_center[2]),
            material=material,
            bevel=0.004,
        )
    )

    return join_and_finalize(ASSET_NAME, parts, material)
