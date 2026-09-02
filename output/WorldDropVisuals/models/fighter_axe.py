"""Deterministic industrial single-bit axe world-drop marker.

The broad curved cutting edge, compact rear poll, visible socket, and long
wrapped shaft are deliberately exaggerated for the fixed Act1 game camera.
This remains a one-mesh, one-material marker rather than an equippable weapon.
"""

from __future__ import annotations

import math

from mathutils import Matrix

from world_drop_common import box, cone, cylinder, custom_mesh, join_and_finalize


ASSET_NAME = "WorldDrop_FighterAxe"


def _extruded_outline(
    name: str,
    outline: list[tuple[float, float]],
    depth: float,
    y: float,
    material,
    bevel: float,
):
    """Create a shallow X/Z silhouette prism centered on ``y``."""

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


def build(material):
    """Build a finished, unmistakable axe silhouette."""

    parts = []

    # The single cutting bit owns most of the visual weight. Its convex outer
    # edge, lower beard, and narrow neck keep it distinct from a hammer head.
    blade_outline = [
        (-0.055, 0.455),
        (-0.170, 0.525),
        (-0.305, 0.505),
        (-0.390, 0.430),
        (-0.420, 0.330),
        (-0.400, 0.220),
        (-0.340, 0.125),
        (-0.270, 0.100),
        (-0.205, 0.155),
        (-0.135, 0.235),
        (-0.055, 0.280),
    ]
    parts.append(
        _extruded_outline(
            "Axe_BroadCurvedBlade",
            blade_outline,
            depth=0.105,
            y=0.0,
            material=material,
            bevel=0.012,
        )
    )

    edge_outline = [
        (-0.292, 0.470),
        (-0.365, 0.410),
        (-0.392, 0.326),
        (-0.376, 0.238),
        (-0.326, 0.158),
        (-0.292, 0.145),
        (-0.323, 0.235),
        (-0.340, 0.326),
        (-0.320, 0.404),
        (-0.255, 0.448),
    ]
    parts.append(
        _extruded_outline(
            "Axe_CuttingEdgeLip",
            edge_outline,
            depth=0.020,
            y=-0.062,
            material=material,
            bevel=0.0,
        )
    )

    rib_outline = [
        (-0.070, 0.395),
        (-0.165, 0.455),
        (-0.265, 0.430),
        (-0.310, 0.360),
        (-0.285, 0.285),
        (-0.205, 0.205),
        (-0.110, 0.275),
    ]
    parts.append(
        _extruded_outline(
            "Axe_BladeReinforcement",
            rib_outline,
            depth=0.018,
            y=-0.061,
            material=material,
            bevel=0.0,
        )
    )

    # The rear poll stays compact so the blade remains visually dominant.
    poll_outline = [
        (0.050, 0.445),
        (0.185, 0.455),
        (0.270, 0.410),
        (0.292, 0.345),
        (0.260, 0.292),
        (0.175, 0.272),
        (0.050, 0.292),
    ]
    parts.append(
        _extruded_outline(
            "Axe_RearPoll",
            poll_outline,
            depth=0.095,
            y=0.0,
            material=material,
            bevel=0.012,
        )
    )

    # A deep socket visibly captures both the head and upper shaft.
    parts.append(
        box(
            "Axe_HeadSocket",
            size=(0.145, 0.155, 0.220),
            location=(0.0, 0.0, 0.350),
            material=material,
            bevel=0.018,
        )
    )

    parts.append(
        cylinder(
            "Axe_MainShaft",
            radius=0.038,
            depth=0.610,
            location=(0.0, 0.0, -0.015),
            material=material,
            vertices=14,
            bevel=0.0,
        )
    )
    parts.append(
        cylinder(
            "Axe_WrappedGrip",
            radius=0.053,
            depth=0.285,
            location=(0.0, 0.0, -0.160),
            material=material,
            vertices=16,
            bevel=0.004,
        )
    )
    for index, band_z in enumerate((-0.297, -0.160, -0.023), start=1):
        parts.append(
            cylinder(
                f"Axe_GripBand_{index}",
                radius=0.060,
                depth=0.020,
                location=(0.0, 0.0, band_z),
                material=material,
                vertices=12,
                bevel=0.0,
            )
        )
    parts.append(
        cone(
            "Axe_FlaredPommel",
            radius1=0.067,
            radius2=0.047,
            depth=0.075,
            location=(0.0, 0.0, -0.357),
            material=material,
            vertices=12,
            bevel=0.0,
        )
    )

    for face_name, bolt_y in (("Front", -0.091), ("Rear", 0.091)):
        parts.append(
            cylinder(
                f"Axe_{face_name}SocketBolt",
                radius=0.019,
                depth=0.028,
                location=(0.0, bolt_y, 0.355),
                material=material,
                rotation=(math.radians(90.0), 0.0, 0.0),
                vertices=8,
                bevel=0.0,
            )
        )

    # Normalize the authored X/Z profile to the runtime X/Y display plane.
    display_rotation = Matrix.Rotation(-math.pi * 0.5, 4, "X")
    contract_scale = Matrix.Scale(0.85, 4)
    for part in parts:
        part.matrix_world = display_rotation @ contract_scale @ part.matrix_world

    return join_and_finalize(ASSET_NAME, parts, material)
