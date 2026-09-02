"""Deterministic industrial miniature greatsword world-drop marker.

The marker is intentionally a single joined mesh with one neutral material.  It
uses the shared primitive helpers so that the source can be regenerated from a
clean Blender process without any authored scene state.
"""

from __future__ import annotations

import math

from world_drop_common import (
    box,
    custom_mesh,
    cylinder,
    join_and_finalize,
    sphere,
    torus,
)


def _blade_prism(material):
    """Build a broad, double-edged blade with a manufactured bevel."""

    # Counter-clockwise when viewed from +Z.  The wide lower shoulders and
    # symmetric taper make the greatsword read cleanly from the game camera.
    outline = [
        (-0.090, 0.020),
        (0.090, 0.020),
        (0.130, 0.080),
        (0.100, 0.320),
        (0.045, 0.425),
        (0.000, 0.490),
        (-0.045, 0.425),
        (-0.100, 0.320),
        (-0.130, 0.080),
    ]
    half_thickness = 0.032
    vertices = [(x, y, -half_thickness) for x, y in outline]
    vertices += [(x, y, half_thickness) for x, y in outline]
    count = len(outline)
    faces = [tuple(reversed(range(count))), tuple(range(count, count * 2))]
    faces += [
        (index, (index + 1) % count, (index + 1) % count + count, index + count)
        for index in range(count)
    ]
    return custom_mesh(
        "BladeCore",
        vertices,
        faces,
        material,
        bevel=0.008,
    )


def _blade_spine(material):
    """Add a low, central ridge for the characteristic double-edge read."""

    outline = [
        (-0.020, 0.065),
        (0.020, 0.065),
        (0.021, 0.345),
        (0.010, 0.430),
        (0.000, 0.465),
        (-0.010, 0.430),
        (-0.021, 0.345),
    ]
    base_z = 0.030
    top_z = 0.051
    vertices = [(x, y, base_z) for x, y in outline]
    vertices += [(x, y, top_z) for x, y in outline]
    count = len(outline)
    faces = [tuple(reversed(range(count))), tuple(range(count, count * 2))]
    faces += [
        (index, (index + 1) % count, (index + 1) % count + count, index + count)
        for index in range(count)
    ]
    return custom_mesh(
        "BladeSpine",
        vertices,
        faces,
        material,
        bevel=0.004,
    )


def build(material):
    """Return the finished ``WorldDrop_FighterGreatsword`` mesh object."""

    parts = [
        _blade_prism(material),
        _blade_spine(material),
        # A compact squared guard gives a clear transition from blade to grip.
        box(
            "GuardCore",
            (0.270, 0.055, 0.074),
            (0.000, 0.000, 0.000),
            material,
            bevel=0.010,
        ),
        # Round end caps soften the silhouette without turning the marker into
        # an ornate equippable weapon.
        cylinder(
            "GuardEndL",
            0.037,
            0.062,
            (-0.132, 0.000, 0.000),
            material,
            rotation=(math.pi / 2.0, 0.0, 0.0),
            vertices=16,
            bevel=0.006,
        ),
        cylinder(
            "GuardEndR",
            0.037,
            0.062,
            (0.132, 0.000, 0.000),
            material,
            rotation=(math.pi / 2.0, 0.0, 0.0),
            vertices=16,
            bevel=0.006,
        ),
        # The short octagonal grip overlaps the guard and pommel, so no part
        # reads as a floating decoration from the side view.
        cylinder(
            "HandleCore",
            0.043,
            0.150,
            (0.000, -0.084, 0.000),
            material,
            rotation=(math.pi / 2.0, 0.0, 0.0),
            vertices=16,
            bevel=0.006,
        ),
        torus(
            "HandleBandUpper",
            0.044,
            0.006,
            (0.000, -0.045, 0.000),
            material,
            rotation=(math.pi / 2.0, 0.0, 0.0),
            major_segments=12,
            minor_segments=4,
        ),
        torus(
            "HandleBandLower",
            0.044,
            0.006,
            (0.000, -0.128, 0.000),
            material,
            rotation=(math.pi / 2.0, 0.0, 0.0),
            major_segments=12,
            minor_segments=4,
        ),
        sphere(
            "Pommel",
            0.052,
            (1.0, 1.05, 0.90),
            (0.000, -0.188, 0.000),
            material,
            segments=16,
            rings=8,
        ),
    ]
    return join_and_finalize("WorldDrop_FighterGreatsword", parts, material)
