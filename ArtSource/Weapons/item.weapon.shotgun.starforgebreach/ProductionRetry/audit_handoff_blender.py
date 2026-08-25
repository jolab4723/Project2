from __future__ import annotations

import json
from pathlib import Path

import bpy
import numpy as np
from mathutils import Matrix, Vector


VIEWS = {
    "front": Vector((0.0, 0.0, 1.0)),
    "back": Vector((0.0, 0.0, -1.0)),
    "left": Vector((-1.0, 0.0, 0.0)),
    "right": Vector((1.0, 0.0, 0.0)),
    "iso": Vector((-0.7, 0.62, 0.8)),
}


def v3(value) -> list[float]:
    return [round(float(value[index]), 6) for index in range(3)]


def aim_camera(camera: bpy.types.Object, target: Vector) -> None:
    direction = (target - camera.location).normalized()
    up_hint = Vector((0.0, 1.0, 0.0))
    if abs(direction.dot(up_hint)) > 0.98:
        up_hint = Vector((0.0, 0.0, 1.0))
    right = direction.cross(up_hint).normalized()
    up = right.cross(direction).normalized()
    camera.rotation_mode = "QUATERNION"
    camera.rotation_quaternion = Matrix((right, up, -direction)).transposed().to_quaternion()


def linear_to_srgb(channel: np.ndarray) -> np.ndarray:
    channel = np.clip(channel, 0.0, 1.0)
    return np.where(channel <= 0.0031308, channel * 12.92, 1.055 * np.power(channel, 1.0 / 2.4) - 0.055)


def main() -> None:
    run = Path(__file__).resolve().parent
    item_id = run.parent.name
    blend_path = run / f"{item_id}.blend"
    handoff = run / "QA" / "Handoff"
    handoff.mkdir(parents=True, exist_ok=True)
    report_path = handoff / "normals_culling_audit.json"

    bpy.ops.wm.open_mainfile(filepath=str(blend_path))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError(f"Expected one mesh, got {len(meshes)}")
    body = meshes[0]
    roots = [obj for obj in bpy.context.scene.objects if obj.parent is None]
    body.data.calc_loop_triangles()

    signed_volume = 0.0
    degenerate = 0
    center = sum((Vector(corner) for corner in body.bound_box), Vector()) / 8.0
    outward = inward = ambiguous = 0
    for triangle in body.data.loop_triangles:
        p0, p1, p2 = (body.data.vertices[index].co for index in triangle.vertices)
        signed_volume += p0.dot(p1.cross(p2)) / 6.0
        area = (p1 - p0).cross(p2 - p0).length * 0.5
        if area <= 1e-14:
            degenerate += 1
        score = triangle.normal.dot(((p0 + p1 + p2) / 3.0) - center)
        if score > 1e-7:
            outward += 1
        elif score < -1e-7:
            inward += 1
        else:
            ambiguous += 1

    points = [body.matrix_world @ Vector(corner) for corner in body.bound_box]
    minimum = Vector(tuple(min(point[index] for point in points) for index in range(3)))
    maximum = Vector(tuple(max(point[index] for point in points) for index in range(3)))
    world_center = (minimum + maximum) * 0.5
    longest = max(maximum - minimum)

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1200
    scene.render.resolution_y = 700
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = True
    camera_data = bpy.data.cameras.new("HandoffCullCamera")
    camera_data.type = "ORTHO"
    camera = bpy.data.objects.new("HandoffCullCamera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    for name, offset, energy in (("HandoffKey", (1.4, -1.6, 1.5), 900.0), ("HandoffFill", (-1.1, 1.2, 0.7), 450.0)):
        light_data = bpy.data.lights.new(name, "AREA")
        light_data.energy = energy
        light_data.shape = "DISK"
        light_data.size = 2.5
        light = bpy.data.objects.new(name, light_data)
        light.location = world_center + Vector(offset) * longest
        light.rotation_euler = (world_center - light.location).to_track_quat("-Z", "Y").to_euler()
        scene.collection.objects.link(light)
    world = bpy.data.worlds.new("HandoffCullWorld")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.025, 0.03, 0.04, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.35
    scene.world = world

    culling_before = {}
    for material in body.data.materials:
        if material is not None and hasattr(material, "use_backface_culling"):
            culling_before[material.name] = bool(material.use_backface_culling)
            material.use_backface_culling = True
    render_paths = {}
    distance = longest * 3.2
    for name, unit in VIEWS.items():
        camera.location = world_center + unit.normalized() * distance
        aim_camera(camera, world_center)
        camera.data.ortho_scale = longest * (1.34 if name == "iso" else 1.20)
        output = handoff / f"culling_{name}.png"
        scene.render.filepath = str(output)
        bpy.ops.render.render(write_still=True)
        render_paths[name] = str(output)
    for material in body.data.materials:
        if material is not None and material.name in culling_before:
            material.use_backface_culling = culling_before[material.name]

    markers = {}
    for name in ("RightHandGrip", "LeftHandGrip", "Muzzle"):
        marker = bpy.data.objects.get(name)
        markers[name] = None if marker is None else {
            "parent": marker.parent.name if marker.parent else None,
            "local_location": v3(marker.location),
            "local_plus_z": v3(marker.matrix_local.to_3x3() @ Vector((0.0, 0.0, 1.0))),
        }
    validation = json.loads((run / "validation.json").read_text(encoding="utf-8"))
    thresholds = validation["material"]["emission"]["threshold_srgb_8bit"]
    base_image = next(image for image in bpy.data.images if Path(bpy.path.abspath(image.filepath)).name == f"{item_id}_BaseColor.png")
    emission_image = next(image for image in bpy.data.images if Path(bpy.path.abspath(image.filepath)).name == f"{item_id}_Emission.png")
    base_pixels = np.empty(base_image.size[0] * base_image.size[1] * 4, dtype=np.float32)
    base_image.pixels.foreach_get(base_pixels)
    rgb = np.rint(linear_to_srgb(base_pixels.reshape((base_image.size[1], base_image.size[0], 4))[:, :, :3]) * 255.0).astype(np.int16)
    red, green, blue = rgb[:, :, 0], rgb[:, :, 1], rgb[:, :, 2]
    expected = (
        (red >= thresholds["r_min"]) & (green >= thresholds["g_min"]) & (blue <= thresholds["b_max"])
        & ((red - green) >= thresholds["r_minus_g_min"])
        & ((green - blue) >= thresholds["g_minus_b_min"])
        & ((red - blue) >= thresholds["r_minus_b_min"])
    )
    emission_pixels = np.empty(emission_image.size[0] * emission_image.size[1] * 4, dtype=np.float32)
    emission_image.pixels.foreach_get(emission_pixels)
    emission_rgba = emission_pixels.reshape((emission_image.size[1], emission_image.size[0], 4))
    actual = emission_rgba[:, :, 0] >= 0.5
    emission_audit = {
        "base_color_image": base_image.filepath,
        "emission_image": emission_image.filepath,
        "thresholds_srgb_8bit": thresholds,
        "expected_pixels": int(expected.sum()),
        "actual_pixels": int(actual.sum()),
        "binary_rgb": bool(np.all(np.isclose(emission_rgba[:, :, :3], 0.0, atol=1e-6) | np.isclose(emission_rgba[:, :, :3], 1.0, atol=1e-6))),
        "opaque_alpha": bool(np.all(np.isclose(emission_rgba[:, :, 3], 1.0, atol=1e-6))),
        "exact_global_rgb_family_match": bool(np.array_equal(expected, actual)),
    }
    oriented = outward + inward
    checks = {
        "single_root": len(roots) == 1,
        "single_mesh": len(meshes) == 1,
        "no_degenerate_triangles": degenerate == 0,
        "required_markers": all(markers[name] is not None for name in markers),
        "muzzle_plus_z": markers["Muzzle"] is not None and markers["Muzzle"]["local_plus_z"] == [0.0, 0.0, 1.0],
        "culling_views_rendered": len(render_paths) == 5 and all(Path(path).exists() for path in render_paths.values()),
        "emission_exact_global_rgb_family": emission_audit["binary_rgb"] and emission_audit["opaque_alpha"] and emission_audit["exact_global_rgb_family_match"],
    }
    report = {
        "item_id": item_id,
        "blend": str(blend_path),
        "root_count": len(roots),
        "root_names": [obj.name for obj in roots],
        "mesh": body.name,
        "triangle_count": len(body.data.loop_triangles),
        "signed_volume": signed_volume,
        "signed_volume_note": "Diagnostic only: Tripo meshes can contain multiple open/internal shells, so global sign is not used as the exterior-winding gate; five-view backface-culling renders are authoritative.",
        "degenerate_triangles": degenerate,
        "centroid_normal_heuristic": {"outward": outward, "inward": inward, "ambiguous": ambiguous, "outward_fraction": 0.0 if oriented == 0 else outward / oriented, "note": "Supplemental only for concave/open weapon geometry."},
        "bounds": {"min": v3(minimum), "max": v3(maximum), "dimensions": v3(maximum - minimum)},
        "markers": markers,
        "emission_rgb_audit_authoritative": emission_audit,
        "culling_renders": render_paths,
        "checks": checks,
        "pass": all(checks.values()),
    }
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
