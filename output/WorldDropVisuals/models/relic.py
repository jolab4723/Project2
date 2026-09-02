"""Deterministic industrial relic marker for the Project2 world-drop set.

The marker is intentionally a single material and a single joined mesh.  A
small faceted core sits inside three manufactured rings, with four clamp shoes
to make the circular silhouette read from the top-down game camera.  It is a
world visual only: there are no grips, muzzle anchors, colliders, or armature
objects.
"""

from __future__ import annotations

from world_drop_common import box, cylinder, sphere, torus, join_and_finalize


ASSET_NAME = "WorldDrop_Relic"


def build(material):
    """Build and return the one joined relic marker mesh."""
    parts = []

    # The core is a compact, slightly elongated bronze capsule.  The shallow
    # bevel on the 12-sided body keeps its manufactured edges readable while
    # the rounded caps stop the marker from looking like an unfinished prism.
    parts.append(
        cylinder(
            "RelicCoreBody",
            radius=0.125,
            depth=0.270,
            location=(0.0, 0.0, 0.0),
            material=material,
            vertices=12,
            bevel=0.016,
        )
    )
    parts.append(
        sphere(
            "RelicCoreCrown",
            radius=0.105,
            scale=(1.0, 1.0, 0.45),
            location=(0.0, 0.0, 0.152),
            material=material,
            segments=12,
            rings=6,
        )
    )
    parts.append(
        sphere(
            "RelicCoreBase",
            radius=0.105,
            scale=(1.0, 1.0, 0.45),
            location=(0.0, 0.0, -0.152),
            material=material,
            segments=12,
            rings=6,
        )
    )

    # Three offset ring planes communicate a restrained mechanical cradle:
    # the broad equatorial band reads as a circular marker from above, while
    # the two crossing bands remain legible in front and side views.
    parts.append(
        torus(
            "RelicEquatorRing",
            major_radius=0.204,
            minor_radius=0.024,
            location=(0.0, 0.0, 0.0),
            material=material,
            major_segments=16,
            minor_segments=5,
        )
    )
    parts.append(
        torus(
            "RelicMeridianRingX",
            major_radius=0.200,
            minor_radius=0.021,
            location=(0.0, 0.0, 0.0),
            material=material,
            rotation=(0.0, 1.5707963267948966, 0.0),
            major_segments=16,
            minor_segments=5,
        )
    )
    parts.append(
        torus(
            "RelicMeridianRingY",
            major_radius=0.200,
            minor_radius=0.021,
            location=(0.0, 0.0, 0.0),
            material=material,
            rotation=(1.5707963267948966, 0.0, 0.0),
            major_segments=16,
            minor_segments=5,
        )
    )

    # Four short shoes sit over the equatorial band at the cardinal points.
    # They are deliberately shallow and joined to the ring so they read as
    # clamps rather than arbitrary floating decoration.
    parts.extend(
        [
            box(
                "RelicClampEast",
                size=(0.062, 0.052, 0.052),
                location=(0.215, 0.0, 0.0),
                material=material,
                bevel=0.009,
            ),
            box(
                "RelicClampWest",
                size=(0.062, 0.052, 0.052),
                location=(-0.215, 0.0, 0.0),
                material=material,
                bevel=0.009,
            ),
            box(
                "RelicClampNorth",
                size=(0.052, 0.062, 0.052),
                location=(0.0, 0.215, 0.0),
                material=material,
                bevel=0.009,
            ),
            box(
                "RelicClampSouth",
                size=(0.052, 0.062, 0.052),
                location=(0.0, -0.215, 0.0),
                material=material,
                bevel=0.009,
            ),
        ]
    )

    return join_and_finalize(ASSET_NAME, parts, material)
