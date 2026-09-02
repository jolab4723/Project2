"""Build the compact industrial world-drop grenade launcher marker.

The world-drop set intentionally uses a single neutral material and a single
joined mesh.  The side drum, six chamber bosses, and the short reinforced
barrel are the silhouette cues that separate this marker from the rifle and
shotgun markers; none of the equippable weapon anchors are included.
"""

from __future__ import annotations

import math

import bpy

from world_drop_common import box, cylinder, join_and_finalize, torus


ASSET_NAME = "WorldDrop_GunnerGrenadeLauncher"


def build(material: bpy.types.Material) -> bpy.types.Object:
    """Create and return the one-mesh world-drop grenade launcher marker."""

    parts: list[bpy.types.Object] = []

    # Compact lower receiver and the angled rear block establish a stable,
    # readable silhouette at the game's top-down drop distance.
    parts.append(
        box(
            "GrenadeLauncher_Frame",
            (0.27, 0.40, 0.20),
            (0.0, -0.04, 0.19),
            material,
            bevel=0.018,
        )
    )
    parts.append(
        box(
            "GrenadeLauncher_RearStock",
            (0.24, 0.18, 0.15),
            (0.0, -0.29, 0.19),
            material,
            rotation=(math.radians(-12.0), 0.0, 0.0),
            bevel=0.016,
        )
    )

    # A shallow lower grip and trigger housing keep the frame visually
    # grounded without introducing a weapon-rig grip anchor.
    parts.append(
        box(
            "GrenadeLauncher_LowerHousing",
            (0.19, 0.14, 0.18),
            (0.0, -0.19, 0.085),
            material,
            rotation=(math.radians(-8.0), 0.0, 0.0),
            bevel=0.0,
        )
    )
    parts.append(
        box(
            "GrenadeLauncher_UnderbarrelBrace",
            (0.22, 0.24, 0.075),
            (0.0, 0.19, 0.175),
            material,
            bevel=0.0,
        )
    )

    # Raised carry rail: two short feet and a broad top make the compact frame
    # legible from the perspective hero view and from the orthographic top view.
    parts.append(
        box(
            "GrenadeLauncher_RailRear",
            (0.18, 0.045, 0.12),
            (0.0, -0.17, 0.34),
            material,
            bevel=0.0,
        )
    )
    parts.append(
        box(
            "GrenadeLauncher_RailFront",
            (0.18, 0.045, 0.12),
            (0.0, 0.025, 0.34),
            material,
            bevel=0.0,
        )
    )
    parts.append(
        box(
            "GrenadeLauncher_RailTop",
            (0.18, 0.24, 0.045),
            (0.0, -0.072, 0.405),
            material,
            bevel=0.0,
        )
    )

    # The short, thick barrel is deliberately offset above the receiver.  A
    # rear collar and a heavy muzzle ring reinforce the industrial miniature
    # language while keeping the +Y barrel silhouette unmistakable.
    barrel_axis = (math.radians(90.0), 0.0, 0.0)
    parts.append(
        cylinder(
            "GrenadeLauncher_Barrel",
            0.094,
            0.38,
            (0.0, 0.27, 0.30),
            material,
            rotation=barrel_axis,
            vertices=16,
            bevel=0.010,
        )
    )
    parts.append(
        cylinder(
            "GrenadeLauncher_BarrelCollar",
            0.116,
            0.085,
            (0.0, 0.085, 0.30),
            material,
            rotation=barrel_axis,
            vertices=16,
            bevel=0.0,
        )
    )
    parts.append(
        cylinder(
            "GrenadeLauncher_MuzzleCap",
            0.102,
            0.032,
            (0.0, 0.472, 0.30),
            material,
            rotation=barrel_axis,
            vertices=16,
            bevel=0.0,
        )
    )
    parts.append(
        torus(
            "GrenadeLauncher_MuzzleReinforcement",
            0.108,
            0.016,
            (0.0, 0.463, 0.30),
            material,
            rotation=barrel_axis,
            major_segments=16,
            minor_segments=4,
        )
    )

    # Side-mounted rotating drum.  The X-axis cylinder and two outer rings
    # intentionally expose the magazine identity in side and hero views.
    drum_axis = (0.0, math.radians(90.0), 0.0)
    drum_center = (0.0, -0.075, 0.17)
    parts.append(
        cylinder(
            "GrenadeLauncher_Drum",
            0.142,
            0.29,
            drum_center,
            material,
            rotation=drum_axis,
            vertices=16,
            bevel=0.012,
        )
    )
    parts.append(
        torus(
            "GrenadeLauncher_DrumRingNear",
            0.143,
            0.014,
            (0.152, drum_center[1], drum_center[2]),
            material,
            rotation=drum_axis,
            major_segments=16,
            minor_segments=4,
        )
    )
    parts.append(
        torus(
            "GrenadeLauncher_DrumRingFar",
            0.143,
            0.014,
            (-0.152, drum_center[1], drum_center[2]),
            material,
            rotation=drum_axis,
            major_segments=16,
            minor_segments=4,
        )
    )

    # Axle boss and six shallow chamber bosses are restrained secondary detail
    # rather than deep cut-outs, so the final single mesh remains robust in a
    # fresh GLB import while still reading as a rotary launcher.
    parts.append(
        cylinder(
            "GrenadeLauncher_DrumAxle",
            0.060,
            0.035,
            (0.177, drum_center[1], drum_center[2]),
            material,
            rotation=drum_axis,
            vertices=16,
            bevel=0.0,
        )
    )
    for chamber_index in range(6):
        angle = math.radians(chamber_index * 60.0 + 30.0)
        chamber_y = drum_center[1] + math.cos(angle) * 0.094
        chamber_z = drum_center[2] + math.sin(angle) * 0.094
        parts.append(
            cylinder(
                f"GrenadeLauncher_Chamber{chamber_index + 1}",
                0.023,
                0.020,
                (0.178, chamber_y, chamber_z),
                material,
                rotation=drum_axis,
                vertices=10,
                bevel=0.0,
            )
        )

    return join_and_finalize(ASSET_NAME, parts, material)
