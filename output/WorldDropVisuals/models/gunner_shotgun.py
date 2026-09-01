"""Industrial miniature shotgun marker for the world-drop visual set.

The source intentionally builds the prop from a small set of manufactured
forms.  It is a drop marker, not an equippable weapon: there are no hand
anchors, muzzle anchors, colliders, or rig objects in the output.
"""

from __future__ import annotations

import math

from world_drop_common import box, cylinder, custom_mesh, join_and_finalize, torus


ASSET_NAME = "WorldDrop_GunnerShotgun"


def _stock(material):
    """Make the short, slightly dropped stock as a beveled wedge."""
    vertices = [
        (-0.13, -0.095, 0.125),
        (-0.13, 0.095, 0.125),
        (-0.13, -0.095, 0.285),
        (-0.13, 0.095, 0.285),
        (-0.345, -0.085, 0.055),
        (-0.345, 0.085, 0.055),
        (-0.345, -0.085, 0.205),
        (-0.345, 0.085, 0.205),
    ]
    faces = [
        (0, 4, 6, 2),  # near side
        (1, 3, 7, 5),  # far side
        (0, 1, 5, 4),  # lower edge
        (2, 6, 7, 3),  # cheek edge
        (0, 2, 3, 1),  # receiver contact
        (4, 5, 7, 6),  # butt
    ]
    return custom_mesh("compact_stock", vertices, faces, material, bevel=0.012)


def build(material):
    """Return one joined mesh representing a compact industrial shotgun."""
    parts = []

    # Thick receiver and its small top rail establish the pump-action body.
    parts.append(box("receiver_body", (0.235, 0.245, 0.215), (-0.02, 0.0, 0.215), material, bevel=0.018))
    parts.append(box("receiver_front_block", (0.045, 0.255, 0.19), (0.105, 0.0, 0.215), material, bevel=0.0))
    parts.append(box("receiver_rear_plate", (0.035, 0.235, 0.19), (-0.145, 0.0, 0.215), material, bevel=0.0))
    parts.append(box("top_sight_rail", (0.235, 0.07, 0.034), (-0.005, 0.0, 0.333), material, bevel=0.0))

    # Two thick, side-by-side barrels are deliberately broader and shorter
    # than a rifle barrel.  Their collars make the large-bore silhouette read
    # from the top-down game camera and from the muzzle end.
    for index, y in enumerate((-0.064, 0.064), start=1):
        parts.append(
            cylinder(
                f"barrel_{index}",
                0.041,
                0.385,
                (0.285, y, 0.252),
                material,
                rotation=(0.0, math.pi / 2.0, 0.0),
                vertices=16,
                bevel=0.003,
            )
        )
        parts.append(
            cylinder(
                f"barrel_collar_{index}",
                0.052,
                0.045,
                (0.45, y, 0.252),
                material,
                rotation=(0.0, math.pi / 2.0, 0.0),
                vertices=16,
                bevel=0.004,
            )
        )

    parts.append(box("muzzle_bridge", (0.052, 0.19, 0.034), (0.45, 0.0, 0.252), material, bevel=0.0))
    parts.append(box("barrel_top_rib", (0.29, 0.042, 0.022), (0.29, 0.0, 0.305), material, bevel=0.0))

    # The broad pump and tube sit below the barrels, providing a second
    # horizontal mass that is characteristic of a shotgun rather than a
    # narrow rifle handguard.
    parts.append(box("pump_housing", (0.17, 0.205, 0.115), (0.145, 0.0, 0.125), material, bevel=0.016))
    parts.append(box("pump_front_band", (0.028, 0.215, 0.13), (0.232, 0.0, 0.145), material, bevel=0.0))
    parts.append(
        cylinder(
            "underbarrel_tube",
            0.028,
            0.30,
            (0.205, 0.0, 0.072),
            material,
            rotation=(0.0, math.pi / 2.0, 0.0),
            vertices=12,
            bevel=0.0,
        )
    )

    # Short dropped stock and a compact rear handle make the rear silhouette
    # visibly different from a straight rifle butt.
    parts.append(_stock(material))
    parts.append(box("butt_cap", (0.035, 0.18, 0.145), (-0.36, 0.0, 0.13), material, rotation=(0.0, -0.16, 0.0), bevel=0.0))
    parts.append(box("rear_handle", (0.092, 0.175, 0.15), (-0.105, 0.0, 0.095), material, rotation=(0.0, -0.27, 0.0), bevel=0.0))

    # Trigger guard and trigger are shallow secondary details, kept inside
    # the single neutral material slot so the marker stays understated.
    parts.append(
        torus(
            "trigger_guard",
            0.045,
            0.009,
            (-0.028, 0.0, 0.095),
            material,
            rotation=(math.pi / 2.0, 0.0, 0.0),
            major_segments=12,
            minor_segments=4,
        )
    )
    parts.append(box("trigger", (0.022, 0.032, 0.058), (-0.035, 0.0, 0.136), material, rotation=(0.0, -0.18, 0.0), bevel=0.0))

    # A shallow side plate and two round fasteners give the receiver a clear
    # side plane without introducing a second material or floating ornament.
    parts.append(box("receiver_side_plate", (0.108, 0.014, 0.086), (-0.025, -0.13, 0.235), material, bevel=0.0))
    for index, x in enumerate((-0.068, 0.018), start=1):
        parts.append(
            cylinder(
                f"receiver_fastener_{index}",
                0.012,
                0.016,
                (x, -0.141, 0.26),
                material,
                rotation=(math.pi / 2.0, 0.0, 0.0),
                vertices=10,
                bevel=0.0,
            )
        )

    return join_and_finalize(ASSET_NAME, parts, material)
