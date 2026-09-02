"""Deterministic simple block-hammer world-drop marker.

The silhouette intentionally follows the common T-shaped hammer convention:
a broad rectangular head on top and a single long handle below.  It avoids the
curved cutting shapes used by the axe marker.
"""

from __future__ import annotations

import math

from world_drop_common import box, cone, cylinder, join_and_finalize


ASSET_NAME = "WorldDrop_FighterBlunt"


def build(material):
    parts = []

    # Large rectangular striking block with softly manufactured edges.
    parts.append(
        box(
            "Hammer_MainHead",
            size=(0.455, 0.170, 0.205),
            location=(0.0, 0.0, 0.300),
            material=material,
            bevel=0.026,
        )
    )
    parts.append(
        box(
            "Hammer_LeftFacePlate",
            size=(0.075, 0.205, 0.235),
            location=(-0.245, 0.0, 0.300),
            material=material,
            bevel=0.0,
        )
    )
    parts.append(
        box(
            "Hammer_RightFacePlate",
            size=(0.075, 0.205, 0.235),
            location=(0.245, 0.0, 0.300),
            material=material,
            bevel=0.0,
        )
    )

    # The centered socket visually locks the head to the handle and reinforces
    # the plain T silhouette requested for the blunt category.
    parts.append(
        box(
            "Hammer_CentralSocket",
            size=(0.135, 0.190, 0.255),
            location=(0.0, 0.0, 0.235),
            material=material,
            bevel=0.020,
        )
    )
    parts.append(
        box(
            "Hammer_TopReinforcement",
            size=(0.170, 0.120, 0.040),
            location=(0.0, 0.0, 0.420),
            material=material,
            bevel=0.0,
        )
    )

    # Long lower handle with one grip sleeve and restrained bands.
    parts.append(
        cylinder(
            "Hammer_MainHandle",
            radius=0.040,
            depth=0.570,
            location=(0.0, 0.0, -0.045),
            material=material,
            vertices=14,
            bevel=0.0,
        )
    )
    parts.append(
        cylinder(
            "Hammer_GripSleeve",
            radius=0.056,
            depth=0.285,
            location=(0.0, 0.0, -0.175),
            material=material,
            vertices=16,
            bevel=0.004,
        )
    )
    for index, band_z in enumerate((-0.300, -0.175, -0.050), start=1):
        parts.append(
            cylinder(
                f"Hammer_GripBand_{index}",
                radius=0.063,
                depth=0.020,
                location=(0.0, 0.0, band_z),
                material=material,
                vertices=12,
                bevel=0.0,
            )
        )
    parts.append(
        cone(
            "Hammer_Pommel",
            radius1=0.070,
            radius2=0.050,
            depth=0.075,
            location=(0.0, 0.0, -0.355),
            material=material,
            vertices=12,
            bevel=0.0,
        )
    )

    # Two large side bolts are enough to sell the industrial assembly without
    # compromising the simple block-head read.
    for face_name, y in (("Front", -0.105), ("Rear", 0.105)):
        parts.append(
            cylinder(
                f"Hammer_{face_name}SocketBolt",
                radius=0.026,
                depth=0.030,
                location=(0.0, y, 0.300),
                material=material,
                rotation=(math.radians(90.0), 0.0, 0.0),
                vertices=8,
                bevel=0.0,
            )
        )

    return join_and_finalize(ASSET_NAME, parts, material)
