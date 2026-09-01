"""Deterministic industrial miniature rifle marker for world drops.

The visual is deliberately a single joined mesh: long barrel and handguard,
compact receiver, tapered stock, and a small under-slung magazine remain
readable from the top-down game camera without carrying any equip-only anchors.
"""

from __future__ import annotations

import math

from world_drop_common import box, cone, custom_mesh, cylinder, join_and_finalize, torus


ASSET_NAME = "WorldDrop_GunnerRifle"


def _stock(material):
    """Make a tapered stock with a slightly thicker butt and a clean bevel."""
    # x is the rifle length axis; the stock overlaps the rear of the receiver.
    vertices = [
        (-0.22, -0.085, 0.105),
        (-0.22, 0.085, 0.105),
        (-0.22, -0.085, 0.235),
        (-0.22, 0.085, 0.235),
        (-0.400, -0.115, 0.075),
        (-0.400, 0.115, 0.075),
        (-0.400, -0.115, 0.245),
        (-0.400, 0.115, 0.245),
    ]
    faces = [
        (0, 4, 5, 1),
        (2, 3, 7, 6),
        (0, 2, 6, 4),
        (1, 5, 7, 3),
        (0, 1, 3, 2),
        (4, 6, 7, 5),
    ]
    return custom_mesh("Rifle_Stock", vertices, faces, material, bevel=0.016)


def build(material):
    """Build and return the one-mesh world-drop rifle marker."""
    parts = []

    # Stage 2/3: the chunky receiver anchors every secondary form.
    parts.append(box(
        "Rifle_Receiver",
        (0.285, 0.19, 0.165),
        (-0.075, 0.0, 0.18),
        material,
        bevel=0.018,
    ))
    parts.append(box(
        "Rifle_LowerHousing",
        (0.17, 0.17, 0.065),
        (-0.095, 0.0, 0.095),
        material,
        bevel=0.012,
    ))
    parts.append(box(
        "Rifle_TopCover",
        (0.21, 0.155, 0.042),
        (-0.045, 0.0, 0.283),
        material,
        bevel=0.0,
    ))
    parts.append(box(
        "Rifle_TopRail",
        (0.17, 0.075, 0.026),
        (-0.005, 0.0, 0.318),
        material,
        bevel=0.006,
    ))

    # Stage 3: a supported handguard and a long, thin barrel give the marker
    # its immediately recognizable rifle silhouette.
    parts.append(box(
        "Rifle_Handguard",
        (0.205, 0.16, 0.105),
        (0.135, 0.0, 0.185),
        material,
        bevel=0.016,
    ))
    parts.append(cylinder(
        "Rifle_Barrel",
        0.031,
        0.335,
        (0.245, 0.0, 0.215),
        material,
        rotation=(0.0, math.pi / 2.0, 0.0),
        vertices=12,
        bevel=0.0,
    ))
    parts.append(cylinder(
        "Rifle_MuzzleCap",
        0.052,
        0.034,
        (0.430, 0.0, 0.215),
        material,
        rotation=(0.0, math.pi / 2.0, 0.0),
        vertices=12,
        bevel=0.0,
    ))
    parts.append(cone(
        "Rifle_MuzzleTaper",
        0.043,
        0.052,
        0.045,
        (0.392, 0.0, 0.215),
        material,
        rotation=(0.0, math.pi / 2.0, 0.0),
        vertices=12,
        bevel=0.0,
    ))

    # Stage 3/4: stock, butt and cheek support are all visibly connected to
    # the receiver so the rear mass does not read as a floating decoration.
    parts.append(_stock(material))
    parts.append(box(
        "Rifle_ButtPlate",
        (0.026, 0.215, 0.165),
        (-0.416, 0.0, 0.16),
        material,
        bevel=0.0,
    ))
    parts.append(box(
        "Rifle_CheekRest",
        (0.12, 0.145, 0.042),
        (-0.295, 0.0, 0.258),
        material,
        bevel=0.0,
    ))

    # Stage 3: the grip and compact magazine sit under the receiver.  Their
    # overlap is intentional, avoiding a visibly unsupported gap after join.
    parts.append(box(
        "Rifle_PistolGrip",
        (0.085, 0.125, 0.17),
        (-0.115, 0.0, 0.045),
        material,
        rotation=(0.0, -0.22, 0.0),
        bevel=0.0,
    ))
    parts.append(box(
        "Rifle_Magazine",
        (0.105, 0.075, 0.125),
        (-0.015, 0.0, 0.055),
        material,
        rotation=(0.0, -0.16, 0.0),
        bevel=0.0,
    ))
    parts.append(torus(
        "Rifle_TriggerGuard",
        0.053,
        0.009,
        (-0.16, 0.0, 0.085),
        material,
        rotation=(math.pi / 2.0, 0.0, 0.0),
        major_segments=12,
        minor_segments=4,
    ))

    # Stage 4/5: small sights and a side ejection panel reinforce orientation
    # while staying below the world-drop detail budget.
    parts.append(box(
        "Rifle_FrontSight",
        (0.028, 0.035, 0.072),
        (0.378, 0.0, 0.265),
        material,
        bevel=0.0,
    ))
    parts.append(box(
        "Rifle_RearSight",
        (0.032, 0.045, 0.052),
        (-0.105, 0.0, 0.326),
        material,
        bevel=0.0,
    ))
    parts.append(box(
        "Rifle_EjectionPanel",
        (0.12, 0.014, 0.062),
        (-0.08, -0.098, 0.205),
        material,
        bevel=0.0,
    ))

    return join_and_finalize(ASSET_NAME, parts, material)
