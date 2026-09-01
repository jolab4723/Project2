"""Deterministic industrial armor marker for the world-drop visual set.

This asset is intentionally a self-contained drop marker rather than an
equippable chest piece: it has no grip points, rig, collider, or attachment
metadata.  The shape is a compact upright chest silhouette with layered
shoulder plates and a raised centre reinforcement panel so it remains legible
from the Act1 camera.
"""

from __future__ import annotations

import math

import bpy

from world_drop_common import box, custom_mesh, join_and_finalize


ASSET_NAME = "WorldDrop_Armor"


def _tapered_plate(
    name: str,
    width_bottom: float,
    width_top: float,
    depth: float,
    height: float,
    location: tuple[float, float, float],
    material: bpy.types.Material,
    bevel: float,
    neck_drop: float = 0.0,
) -> bpy.types.Object:
    """Build a shallow tapered chest plate with a manufactured bevel.

    ``neck_drop`` cuts a small, flat neckline into the upper edge.  The
    optional cut keeps this helper useful for a plain plate while making the
    armor marker read as an upper garment rather than a shield.
    """

    half_bottom = width_bottom * 0.5
    half_top = width_top * 0.5
    half_depth = depth * 0.5
    z_bottom = -height * 0.5
    z_top = height * 0.5
    if neck_drop > 0.0:
        neck_half_width = min(0.09, half_top * 0.55)
        z_neck = z_top - neck_drop
        vertices = [
            (-half_bottom, -half_depth, z_bottom),
            (half_bottom, -half_depth, z_bottom),
            (half_top, -half_depth, z_top),
            (neck_half_width, -half_depth, z_neck),
            (-neck_half_width, -half_depth, z_neck),
            (-half_top, -half_depth, z_top),
            (-half_bottom, half_depth, z_bottom),
            (half_bottom, half_depth, z_bottom),
            (half_top, half_depth, z_top),
            (neck_half_width, half_depth, z_neck),
            (-neck_half_width, half_depth, z_neck),
            (-half_top, half_depth, z_top),
        ]
        faces = [
            (0, 1, 2, 3, 4, 5),
            (6, 11, 10, 9, 8, 7),
            (0, 6, 7, 1),
            (2, 8, 9, 3),
            (3, 9, 10, 4),
            (4, 10, 11, 5),
            (0, 5, 11, 6),
            (1, 7, 8, 2),
        ]
    else:
        vertices = [
            (-half_bottom, -half_depth, z_bottom),
            (half_bottom, -half_depth, z_bottom),
            (half_top, -half_depth, z_top),
            (-half_top, -half_depth, z_top),
            (-half_bottom, half_depth, z_bottom),
            (half_bottom, half_depth, z_bottom),
            (half_top, half_depth, z_top),
            (-half_top, half_depth, z_top),
        ]
        faces = [
            (0, 1, 2, 3),
            (4, 7, 6, 5),
            (0, 4, 5, 1),
            (3, 2, 6, 7),
            (0, 3, 7, 4),
            (1, 5, 6, 2),
        ]
    return custom_mesh(
        name,
        vertices,
        faces,
        material,
        location=location,
        bevel=bevel,
    )


def _reinforcement_panel(material: bpy.types.Material) -> bpy.types.Object:
    """Make a raised, chamfered centre rib that reads at top-down scale."""

    # The sloped shoulders keep the panel from looking like an unedited box.
    half_width = 0.073
    half_depth = 0.025
    z_bottom = -0.17
    z_shoulder = 0.16
    z_top = 0.225
    top_half_width = 0.047
    vertices = [
        (-half_width, -half_depth, z_bottom),
        (half_width, -half_depth, z_bottom),
        (half_width, -half_depth, z_shoulder),
        (top_half_width, -half_depth, z_top),
        (-top_half_width, -half_depth, z_top),
        (-half_width, half_depth, z_bottom),
        (half_width, half_depth, z_bottom),
        (half_width, half_depth, z_shoulder),
        (top_half_width, half_depth, z_top),
        (-top_half_width, half_depth, z_top),
    ]
    faces = [
        (0, 1, 2, 3, 4),
        (5, 9, 8, 7, 6),
        (0, 5, 6, 1),
        (1, 6, 7, 2),
        (2, 7, 8, 3),
        (3, 8, 9, 4),
        (4, 9, 5, 0),
    ]
    return custom_mesh(
        "Armor_CentreReinforcement",
        vertices,
        faces,
        material,
        location=(0.0, -0.135, 0.0),
        bevel=0.011,
    )


def _shoulder_plate(
    side: float,
    material: bpy.types.Material,
) -> list[bpy.types.Object]:
    """Return one sloped shoulder shell plus its raised cap."""

    x = side * 0.255
    # A shallow rotation gives the outer shoulder a deliberate downward fall.
    tilt = -side * math.radians(12.0)
    shell = box(
        "Armor_ShoulderShell_L" if side < 0.0 else "Armor_ShoulderShell_R",
        (0.205, 0.19, 0.135),
        (x, -0.005, 0.175),
        material,
        rotation=(0.0, tilt, 0.0),
        bevel=0.023,
    )
    cap = box(
        "Armor_ShoulderCap_L" if side < 0.0 else "Armor_ShoulderCap_R",
        (0.12, 0.045, 0.092),
        (side * 0.262, -0.112, 0.195),
        material,
        rotation=(0.0, tilt, 0.0),
        bevel=0.012,
    )
    return [shell, cap]


def build(material: bpy.types.Material) -> bpy.types.Object:
    """Build and finalize the single-mesh armor drop visual."""

    parts: list[bpy.types.Object] = []

    # Primary chest shell: broad upper chest, narrowing toward the waist.
    parts.append(
        _tapered_plate(
            "Armor_ChestShell",
            width_bottom=0.34,
            width_top=0.46,
            depth=0.205,
            height=0.44,
            location=(0.0, 0.0, -0.015),
            material=material,
            bevel=0.024,
            neck_drop=0.065,
        )
    )

    # Two layered shoulder pieces establish the armor category in silhouette.
    parts.extend(_shoulder_plate(-1.0, material))
    parts.extend(_shoulder_plate(1.0, material))

    # Raised central panel overlaps the shell at the front face.
    parts.append(_reinforcement_panel(material))

    # A low waist guard and collar lip close the main shell with believable
    # contact planes while remaining a simple miniature marker.
    parts.append(
        box(
            "Armor_WaistGuard",
            (0.33, 0.17, 0.082),
            (0.0, -0.002, -0.217),
            material,
            bevel=0.018,
        )
    )
    for side in (-1.0, 1.0):
        parts.append(
            box(
                "Armor_NeckCollar_L" if side < 0.0 else "Armor_NeckCollar_R",
                (0.12, 0.17, 0.068),
                (side * 0.105, -0.004, 0.198),
                material,
                rotation=(0.0, -side * math.radians(6.0), 0.0),
                bevel=0.014,
            )
        )

    # Small horizontal bars sit on the raised panel as restrained manufactured
    # seams; their overlap keeps them readable without introducing a second
    # material slot or a floating ornament.
    for index, z in enumerate((-0.075, 0.018, 0.108), start=1):
        parts.append(
            box(
                f"Armor_CentreSeam_{index}",
                (0.102 if index != 3 else 0.078, 0.018, 0.018),
                (0.0, -0.168, z),
                material,
                bevel=0.005,
            )
        )

    return join_and_finalize(ASSET_NAME, parts, material)
