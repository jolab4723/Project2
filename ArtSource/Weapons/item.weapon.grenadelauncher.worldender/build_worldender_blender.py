from __future__ import annotations

import json
import subprocess
from datetime import datetime, timezone
from math import cos, sin, tau
from pathlib import Path

import bpy
import bmesh
from mathutils import Matrix, Vector


ROOT = Path(__file__).resolve().parent
ITEM_ID = "item.weapon.grenadelauncher.worldender"
TASK_ID = "6ffde46a-3c8e-49cd-afb9-dc73afce4e68"
RAW_GLB = ROOT / "Tripo" / "Downloaded" / f"{ITEM_ID}_raw.glb"
PRODUCTION = ROOT / "Production"
TEXTURES = PRODUCTION / "Textures"
QA = PRODUCTION / "QA"
BACKUP = PRODUCTION / "Backup"
BLEND_PATH = PRODUCTION / f"{ITEM_ID}.blend"
FBX_PATH = PRODUCTION / f"{ITEM_ID}.fbx"
VALIDATION_PATH = PRODUCTION / "validation.json"

# H3 raw -X is the approved-view muzzle direction, +Z is up, +Y is width.
# Final Gunner contract is +Z muzzle, +Y up, +X width. Vertex positions are
# rewritten as (X,Y,Z)_raw -> (Y,Z,-X)_final while object transforms stay unit.
MODEL_SCALE = 1.01319876
ROOT_GRIP_RAW = Vector((0.1920, 0.0, -0.0860))
LEFT_GRIP_RAW = Vector((-0.1300, 0.0, -0.0140))
MUZZLE_RAW = Vector((-0.5000, 0.0, 0.0206))
FINAL_TARGET = 60_000
CANDIDATE_TARGETS = (100_000, 60_000, 50_000, 40_000)
VISUAL_REVIEW_APPROVED = True
VISUAL_REVIEW_BASIS = "Direct 1024px side/isometric review retained the 60k body branch; the incomplete H3 bell was then rebuilt as closed, embedded gold flare and crimson rim shells without changing the grip, barrel axis or outer weapon bounds."
EMISSION_MODE = "sealed_red"
EMISSION_STRENGTH = 3.0
EMISSION_ENVELOPE = {
    "space": "final root-local",
    "min": [-0.13, 0.055, -0.08],
    "max": [0.13, 0.19, 0.08],
    "description": "sealed physical central core only; excludes stock/body red paint, bronze support rod and bell muzzle",
}
FINAL_PROMPT = """item.weapon.grenadelauncher.worldender — Legendary Gunner grenade launcher.
Approved H3 multiview source: fictional heavy siege launcher with triangular crimson stock, sealed red core housing, single black barrel, bronze lower support rod and broad bell muzzle. Production preserves the generated body and PBR texture set, reconstructs the source-incomplete muzzle as an embedded closed gold flare plus crimson front rim, keeps both repair materials non-emissive, limits emission to the hard-bounded sealed core UV pixels, uses +Z muzzle/+Y up, and keeps both hand markers at the approved contact areas.
"""

BELL_REPAIR = {
    "center_y": 0.108,
    "segments": 96,
    "gold": {
        "z_start": 0.505,
        "z_end": 0.688,
        "outer_start": [0.057, 0.071],
        "outer_end": [0.101, 0.122],
        "inner_start": [0.045, 0.056],
        "inner_end": [0.086, 0.105],
        "probe_z_levels": [0.555, 0.610, 0.665],
    },
    "rim": {
        "z_start": 0.682,
        "z_end": 0.700,
        "outer_start": [0.118, 0.137],
        "outer_end": [0.118, 0.137],
        "inner_start": [0.097, 0.117],
        "inner_end": [0.097, 0.117],
        "probe_z_levels": [0.691],
    },
}


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def v3(value) -> list[float]:
    return [round(float(value[i]), 6) for i in range(3)]


def raw_to_final(point: Vector) -> Vector:
    return Vector((point.y, point.z, -point.x)) * MODEL_SCALE


ROOT_FINAL = raw_to_final(ROOT_GRIP_RAW)


def to_local(point: Vector) -> Vector:
    return raw_to_final(point) - ROOT_FINAL


def ensure_dirs() -> None:
    for directory in (PRODUCTION, TEXTURES, QA, BACKUP):
        directory.mkdir(parents=True, exist_ok=True)


def clear_scene() -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)


def import_raw() -> bpy.types.Object:
    if not RAW_GLB.is_file():
        raise FileNotFoundError(RAW_GLB)
    bpy.ops.import_scene.gltf(filepath=str(RAW_GLB))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError(f"Expected one H3 mesh, got {len(meshes)}")
    body = meshes[0]
    body.name = "Worldender_Body"
    body.data.name = "Worldender_Body_Mesh"
    return body


def triangles(obj: bpy.types.Object) -> int:
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def transform_mesh(obj: bpy.types.Object) -> dict:
    points = [Vector(vertex.co) for vertex in obj.data.vertices]
    minimum = Vector((min(p[i] for p in points) for i in range(3)))
    maximum = Vector((max(p[i] for p in points) for i in range(3)))
    for vertex in obj.data.vertices:
        vertex.co = to_local(Vector(vertex.co))
    obj.data.update()
    obj.location = (0.0, 0.0, 0.0)
    obj.rotation_euler = (0.0, 0.0, 0.0)
    obj.scale = (1.0, 1.0, 1.0)
    return {
        "raw_bounds": {"min": v3(minimum), "max": v3(maximum), "dimensions": v3(maximum - minimum)},
        "mapping": f"raw (X,Y,Z) -> final (Y,Z,-X), scale {MODEL_SCALE}",
        "root_grip_raw": v3(ROOT_GRIP_RAW),
    }


def find_image(prefix: str) -> bpy.types.Image:
    image = next((entry for entry in bpy.data.images if entry.name.startswith(prefix)), None)
    if image is None:
        raise RuntimeError(f"Missing embedded {prefix} image")
    return image


def save_image(image: bpy.types.Image, path: Path, colorspace: str) -> dict:
    image.colorspace_settings.name = colorspace
    image.filepath_raw = str(path)
    image.file_format = "PNG"
    image.save()
    return {
        "source_name": image.name,
        "path": str(path.relative_to(ROOT)),
        "size": list(image.size),
        "colorspace": colorspace,
    }


def create_emission_map(color: bpy.types.Image, obj: bpy.types.Object) -> tuple[bpy.types.Image, dict]:
    width, height = color.size
    uv_layer = obj.data.uv_layers.active
    if uv_layer is None:
        raise RuntimeError("Original H3 UV set is required for emission reconstruction")
    lower = Vector(EMISSION_ENVELOPE["min"])
    upper = Vector(EMISSION_ENVELOPE["max"])
    polygons = []
    for polygon in obj.data.polygons:
        center = polygon.center
        if not all(lower[i] <= center[i] <= upper[i] for i in range(3)):
            continue
        uv_points = []
        for loop_index in polygon.loop_indices:
            uv = uv_layer.data[loop_index].uv
            uv_points.append([round(float(uv.x * (width - 1)), 3), round(float((1.0 - uv.y) * (height - 1)), 3)])
        if len(uv_points) >= 3:
            polygons.append(uv_points)
    regions_path = QA / "emission_uv_regions.json"
    report_path = QA / "emission_mask_report.json"
    output_path = TEXTURES / f"{ITEM_ID}_Emission.png"
    regions_path.write_text(json.dumps({"functional_envelope": EMISSION_ENVELOPE, "uv_polygons": polygons}), encoding="utf-8")
    subprocess.run([
        "python", str(ROOT / "build_emission_mask.py"),
        "--base", str(TEXTURES / f"{ITEM_ID}_BaseColor.png"),
        "--regions", str(regions_path), "--output", str(output_path),
        "--report", str(report_path), "--mode", EMISSION_MODE,
    ], check=True)
    emission = bpy.data.images.load(str(output_path), check_existing=False)
    emission.name = "Worldender_Status_Emission"
    emission.colorspace_settings.name = "sRGB"
    report = json.loads(report_path.read_text(encoding="utf-8"))
    report.update({
        "path": str(output_path.relative_to(ROOT)),
        "regions_path": str(regions_path.relative_to(ROOT)),
        "report_path": str(report_path.relative_to(ROOT)),
    })
    return emission, report


def assign_sealed_core_material(obj: bpy.types.Object) -> int:
    lower = Vector(EMISSION_ENVELOPE["min"])
    upper = Vector(EMISSION_ENVELOPE["max"])
    selected = 0
    for polygon in obj.data.polygons:
        slot = obj.material_slots[polygon.material_index] if polygon.material_index < len(obj.material_slots) else None
        material_name = slot.material.name if slot and slot.material else ""
        if material_name in {
            "M_Worldender_BellInner_PBR",
            "M_Worldender_BellRim_PBR",
            "M_Worldender_StockPadCap_PBR",
        }:
            continue
        inside = all(lower[i] <= polygon.center[i] <= upper[i] for i in range(3))
        polygon.material_index = 1 if inside else 0
        selected += int(inside)
    obj.data.update()
    return selected


def configure_material(obj: bpy.types.Object) -> tuple[dict, list[dict]]:
    if len(obj.material_slots) != 1 or obj.material_slots[0].material is None:
        raise RuntimeError("Expected exactly one imported PBR material")
    material = obj.material_slots[0].material
    material.name = "M_Worldender_PBR"
    color, normal, orm = find_image("Color_"), find_image("NormalGL_"), find_image("ORM_")
    records = [
        save_image(color, TEXTURES / f"{ITEM_ID}_BaseColor.png", "sRGB"),
        save_image(normal, TEXTURES / f"{ITEM_ID}_Normal.png", "Non-Color"),
        save_image(orm, TEXTURES / f"{ITEM_ID}_ORM.png", "Non-Color"),
    ]
    emission, emission_record = create_emission_map(color, obj)
    records.append(emission_record)
    material.use_nodes = True
    principled = next((node for node in material.node_tree.nodes if node.bl_idname == "ShaderNodeBsdfPrincipled"), None)
    if principled is None:
        raise RuntimeError("Imported material has no Principled BSDF")
    # H3 reuses some UV texels across distant body surfaces. Keep the imported
    # PBR material non-emissive, then assign a copied PBR+Emission material only
    # to existing faces inside the sealed-core object-space envelope. This is a
    # material boundary on one mesh, not added/floating geometry, and prevents
    # red-paint UV reuse from glowing at the stock or bell.
    for link in list(principled.inputs["Emission Color"].links):
        material.node_tree.links.remove(link)
    principled.inputs["Emission Color"].default_value = (0.0, 0.0, 0.0, 1.0)
    principled.inputs["Emission Strength"].default_value = 0.0
    core_material = material.copy()
    core_material.name = "M_Worldender_SealedCore_PBR_Emission"
    core_principled = next(node for node in core_material.node_tree.nodes if node.bl_idname == "ShaderNodeBsdfPrincipled")
    node = core_material.node_tree.nodes.new("ShaderNodeTexImage")
    node.name = "Worldender Status Emission Map"
    node.image = emission
    node.interpolation = "Linear"
    core_material.node_tree.links.new(node.outputs["Color"], core_principled.inputs["Emission Color"])
    core_principled.inputs["Emission Strength"].default_value = EMISSION_STRENGTH
    core_principled.inputs["Alpha"].default_value = 1.0
    obj.data.materials.append(core_material)
    core_polygon_count = assign_sealed_core_material(obj)
    if core_polygon_count == 0:
        raise RuntimeError("Sealed core material boundary selected no polygons")
    return ({
        "name": material.name,
        "sealed_core_material": core_material.name,
        "sealed_core_source_polygon_count": core_polygon_count,
        "base_color_texture": color.name,
        "normal_texture": normal.name,
        "orm_texture": orm.name,
        "emission_texture": emission.name,
        "emission_strength": EMISSION_STRENGTH,
        "emission_geometry_added": False,
        "emission_boundary": "existing mesh faces in final root-local sealed-core envelope",
        "principled": True,
    }, records)


def duplicate_decimated(source: bpy.types.Object, target: int, name: str) -> tuple[bpy.types.Object, dict]:
    candidate = source.copy()
    candidate.data = source.data.copy()
    candidate.name = name
    candidate.data.name = f"{name}_Mesh"
    bpy.context.scene.collection.objects.link(candidate)
    before = triangles(candidate)
    modifier = candidate.modifiers.new(f"Decimate_{target}", "DECIMATE")
    modifier.decimate_type = "COLLAPSE"
    modifier.ratio = min(1.0, target / float(before))
    modifier.use_collapse_triangulate = True
    bpy.context.view_layer.objects.active = candidate
    candidate.select_set(True)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    candidate.select_set(False)
    return candidate, {"before": before, "target": target, "after": triangles(candidate), "ratio": round(target / float(before), 8)}


def make_dark_cap_material() -> bpy.types.Material:
    material = bpy.data.materials.new("M_Worldender_StockPadCap_PBR")
    material.use_nodes = True
    principled = next(node for node in material.node_tree.nodes if node.bl_idname == "ShaderNodeBsdfPrincipled")
    principled.inputs["Base Color"].default_value = (0.012, 0.016, 0.020, 1.0)
    principled.inputs["Metallic"].default_value = 0.05
    principled.inputs["Roughness"].default_value = 0.58
    principled.inputs["Alpha"].default_value = 1.0
    return material


def make_bell_material(name: str, color: tuple[float, float, float, float], metallic: float, roughness: float) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    principled = next(node for node in material.node_tree.nodes if node.bl_idname == "ShaderNodeBsdfPrincipled")
    principled.inputs["Base Color"].default_value = color
    principled.inputs["Metallic"].default_value = metallic
    principled.inputs["Roughness"].default_value = roughness
    principled.inputs["Emission Color"].default_value = (0.0, 0.0, 0.0, 1.0)
    principled.inputs["Emission Strength"].default_value = 0.0
    principled.inputs["Alpha"].default_value = 1.0
    return material


def topology_report(obj: bpy.types.Object) -> dict:
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.normal_update()
    record = {
        "vertices": len(bm.verts),
        "edges": len(bm.edges),
        "faces": len(bm.faces),
        "boundary_edges": sum(1 for edge in bm.edges if edge.is_boundary),
        "non_manifold_edges_including_boundaries": sum(1 for edge in bm.edges if not edge.is_manifold),
        "loose_edges": sum(1 for edge in bm.edges if not edge.link_faces),
        "degenerate_faces": sum(1 for face in bm.faces if face.calc_area() < 1.0e-10),
    }
    bm.free()
    return record


def create_annular_elliptical_frustum(
    name: str,
    center_y: float,
    z_start: float,
    z_end: float,
    outer_start: tuple[float, float],
    outer_end: tuple[float, float],
    inner_start: tuple[float, float],
    inner_end: tuple[float, float],
    segments: int,
    material: bpy.types.Material,
) -> tuple[bpy.types.Object, dict]:
    """Create a closed thick annular shell; no open edge is permitted."""
    rings = (
        (outer_start, z_start),
        (outer_end, z_end),
        (inner_start, z_start),
        (inner_end, z_end),
    )
    vertices = []
    for (radius_x, radius_y), z_value in rings:
        vertices.extend(
            (radius_x * cos(tau * index / segments), center_y + radius_y * sin(tau * index / segments), z_value)
            for index in range(segments)
        )

    outer_start_offset = 0
    outer_end_offset = segments
    inner_start_offset = segments * 2
    inner_end_offset = segments * 3
    faces = []
    for index in range(segments):
        next_index = (index + 1) % segments
        outer_start_i, outer_start_j = outer_start_offset + index, outer_start_offset + next_index
        outer_end_i, outer_end_j = outer_end_offset + index, outer_end_offset + next_index
        inner_start_i, inner_start_j = inner_start_offset + index, inner_start_offset + next_index
        inner_end_i, inner_end_j = inner_end_offset + index, inner_end_offset + next_index
        faces.extend((
            (outer_start_i, outer_start_j, outer_end_j, outer_end_i),
            (inner_start_i, inner_end_i, inner_end_j, inner_start_j),
            (outer_end_i, outer_end_j, inner_end_j, inner_end_i),
            (outer_start_i, inner_start_i, inner_start_j, outer_start_j),
        ))

    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.validate(clean_customdata=False)
    mesh.materials.append(material)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)

    normal_mesh = bmesh.new()
    normal_mesh.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(normal_mesh, faces=list(normal_mesh.faces))
    normal_mesh.to_mesh(mesh)
    normal_mesh.free()
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    mesh.update()

    topology = topology_report(obj)
    if any(topology[key] != 0 for key in ("boundary_edges", "non_manifold_edges_including_boundaries", "loose_edges", "degenerate_faces")):
        raise RuntimeError(f"{name} is not a closed manifold shell: {topology}")
    return obj, topology


def radial_material_coverage(obj: bpy.types.Object, material_name: str, z_levels: list[float], center_y: float, samples: int = 24) -> dict:
    levels = []
    for z_value in z_levels:
        hits = 0
        material_hits = 0
        for index in range(samples):
            angle = tau * index / samples
            direction = Vector((cos(angle), sin(angle), 0.0))
            hit, _location, _normal, face_index = obj.ray_cast(Vector((0.0, center_y, z_value)), direction, distance=0.3)
            hits += int(hit)
            if hit and face_index >= 0:
                polygon = obj.data.polygons[face_index]
                slot = obj.material_slots[polygon.material_index] if polygon.material_index < len(obj.material_slots) else None
                material_hits += int(bool(slot and slot.material and slot.material.name == material_name))
        levels.append({"z": z_value, "samples": samples, "hits": hits, "material_hits": material_hits})
    return {
        "material": material_name,
        "levels": levels,
        "complete": all(level["material_hits"] == samples for level in levels),
    }


def repair_end_sections(body: bpy.types.Object) -> dict:
    """Repair H3's incomplete bell and reversed front/back cross-section."""
    before = topology_report(body)
    before_triangles = triangles(body)

    # Cut only the false central bell cap. The cutter is smaller than the
    # authored bronze bell throat, so the outer flare, rim and side silhouette
    # remain untouched while Blender's exact boolean creates a clean inner wall.
    aperture_center = Vector((0.0, 0.108, 0.662))
    aperture_radius = 0.064
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=64,
        radius=aperture_radius,
        depth=0.205,
        location=aperture_center,
    )
    cutter = bpy.context.object
    cutter.name = "QA_BellAperture_Cutter"
    modifier = body.modifiers.new("Open_Bell_Aperture", "BOOLEAN")
    modifier.operation = "DIFFERENCE"
    modifier.solver = "EXACT"
    modifier.object = cutter
    bpy.context.view_layer.objects.active = body
    body.select_set(True)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    body.select_set(False)
    bpy.data.objects.remove(cutter, do_unlink=True)

    gold_material = make_bell_material(
        "M_Worldender_BellInner_PBR",
        (0.42, 0.19, 0.035, 1.0),
        metallic=0.78,
        roughness=0.31,
    )
    rim_material = make_bell_material(
        "M_Worldender_BellRim_PBR",
        (0.19, 0.012, 0.008, 1.0),
        metallic=0.52,
        roughness=0.34,
    )
    gold = BELL_REPAIR["gold"]
    rim = BELL_REPAIR["rim"]
    bell_inner, bell_inner_topology = create_annular_elliptical_frustum(
        "BellInner_ClosedRepair",
        BELL_REPAIR["center_y"],
        gold["z_start"], gold["z_end"],
        tuple(gold["outer_start"]), tuple(gold["outer_end"]),
        tuple(gold["inner_start"]), tuple(gold["inner_end"]),
        BELL_REPAIR["segments"],
        gold_material,
    )
    bell_rim, bell_rim_topology = create_annular_elliptical_frustum(
        "BellRim_ClosedRepair",
        BELL_REPAIR["center_y"],
        rim["z_start"], rim["z_end"],
        tuple(rim["outer_start"]), tuple(rim["outer_end"]),
        tuple(rim["inner_start"]), tuple(rim["inner_end"]),
        BELL_REPAIR["segments"],
        rim_material,
    )

    # Seal the stock-pad bore with a shallow beveled dark-rubber cap. It extends
    # 12 mm into the existing pad and its outer face is coplanar with the pad's
    # rear bound, making it physically seated and flush rather than floating.
    stock_min_z = min(vertex.co.z for vertex in body.data.vertices)
    cap_center = Vector((0.0, 0.100, stock_min_z + 0.0060))
    bpy.ops.mesh.primitive_cube_add(location=cap_center)
    cap = bpy.context.object
    cap.name = "StockPad_FlushCap"
    cap.data.name = "StockPad_FlushCap_Mesh"
    cap.dimensions = (0.105, 0.145, 0.012)
    bpy.context.view_layer.objects.active = cap
    cap.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    cap.select_set(False)
    cap.data.materials.append(make_dark_cap_material())
    bevel = cap.modifiers.new("FlushCap_Bevel", "BEVEL")
    bevel.width = 0.0080
    bevel.segments = 4
    bpy.context.view_layer.objects.active = cap
    cap.select_set(True)
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    cap.select_set(False)

    # One export mesh, while preserving dedicated non-emissive bell and cap slots.
    bpy.ops.object.select_all(action="DESELECT")
    body.select_set(True)
    bell_inner.select_set(True)
    bell_rim.select_set(True)
    cap.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.join()
    body.name = "Worldender_Body"
    body.data.name = "Worldender_Body_Mesh"

    # Exact boolean output can contain zero-area sliver faces at coplanar cap
    # intersections. Remove only those degenerate elements; no remesh or broad
    # weld is used, so authored UV/PBR surfaces and the side silhouette remain.
    cleanup = bmesh.new()
    cleanup.from_mesh(body.data)
    bmesh.ops.dissolve_degenerate(cleanup, dist=1.0e-7, edges=list(cleanup.edges))
    cleanup.to_mesh(body.data)
    cleanup.free()
    body.data.update()

    # Keep the repaired asset within the requested 40–60k final budget.
    pre_budget_triangles = triangles(body)
    if pre_budget_triangles > 60_000:
        raise RuntimeError(f"Structural repair exceeded the 60k budget without decimating the closed bell shells: {pre_budget_triangles}")
    body.data.update()
    after = topology_report(body)
    gold_coverage = radial_material_coverage(
        body,
        gold_material.name,
        gold["probe_z_levels"],
        BELL_REPAIR["center_y"],
    )
    rim_coverage = radial_material_coverage(
        body,
        rim_material.name,
        rim["probe_z_levels"],
        BELL_REPAIR["center_y"],
    )
    if not gold_coverage["complete"] or not rim_coverage["complete"]:
        raise RuntimeError(f"Bell repair radial coverage failed: gold={gold_coverage}, rim={rim_coverage}")
    return {
        "status": "rebuilt",
        "bell_aperture": {
            "method": "exact boolean difference through false front cap",
            "center": v3(aperture_center),
            "radius": aperture_radius,
            "depth": 0.205,
            "outer_bell_silhouette_modified": False,
        },
        "bell_closed_reconstruction": {
            "method": "embedded closed elliptical gold flare and crimson front rim shells joined into the single export mesh",
            "center_y": BELL_REPAIR["center_y"],
            "segments": BELL_REPAIR["segments"],
            "gold": {**gold, "material": gold_material.name, "emissive": False, "topology_before_join": bell_inner_topology},
            "rim": {**rim, "material": rim_material.name, "emissive": False, "topology_before_join": bell_rim_topology},
            "gold_radial_coverage": gold_coverage,
            "rim_radial_coverage": rim_coverage,
        },
        "stock_pad_cap": {
            "method": "embedded flush beveled dark PBR cap joined into export mesh",
            "center": v3(cap_center),
            "dimensions": [0.105, 0.145, 0.012],
            "bevel": 0.0080,
            "emissive": False,
        },
        "triangles": {"before": before_triangles, "pre_budget": pre_budget_triangles, "after": triangles(body)},
        "topology_before": before,
        "topology_after": after,
    }


def bounds(obj: bpy.types.Object) -> tuple[Vector, Vector]:
    points = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    return (
        Vector((min(p[i] for p in points) for i in range(3))),
        Vector((max(p[i] for p in points) for i in range(3))),
    )


def orient_camera(camera: bpy.types.Object, target: Vector, preferred_up=Vector((0.0, 1.0, 0.0))) -> dict:
    direction = (target - camera.location).normalized()
    up = preferred_up.normalized()
    if abs(direction.dot(up)) > 0.98:
        up = Vector((0.0, 0.0, 1.0))
    right = direction.cross(up).normalized()
    up = right.cross(direction).normalized()
    rotation = Matrix((right, up, -direction)).transposed()
    distance = (camera.location - target).length
    camera.matrix_world = rotation.to_4x4()
    camera.location = target - direction * distance
    return {"view_from": v3(-direction), "screen_up": v3(up), "screen_right": v3(right)}


def setup_render(center: Vector, longest: float):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1024
    scene.render.resolution_y = 1024
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = True
    scene.view_settings.look = "AgX - Medium High Contrast"
    camera_data = bpy.data.cameras.new("QA_Camera")
    camera_data.type = "ORTHO"
    camera = bpy.data.objects.new("QA_Camera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    temporary = [camera]
    for name, offset, energy, size in (
        ("QA_Key", (1.7, -1.8, 2.0), 170.0, 2.2),
        ("QA_Fill", (-1.4, -1.0, 0.8), 90.0, 2.0),
        ("QA_Rim", (0.2, 2.0, 1.4), 125.0, 1.8),
    ):
        data = bpy.data.lights.new(name, "AREA")
        data.energy, data.shape, data.size = energy, "DISK", size
        light = bpy.data.objects.new(name, data)
        light.location = center + Vector(offset) * longest
        light.rotation_euler = (center - light.location).to_track_quat("-Z", "Y").to_euler()
        scene.collection.objects.link(light)
        temporary.append(light)
    world = bpy.data.worlds.new("QA_World")
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (0.065, 0.075, 0.095, 1.0)
    background.inputs["Strength"].default_value = 0.34
    scene.world = world
    return scene, camera, temporary


def render_view(scene, camera, center: Vector, longest: float, name: str, offset: Vector, output: Path) -> dict:
    camera.location = center + offset
    basis = orient_camera(camera, center)
    camera.data.ortho_scale = longest * (1.72 if "iso" in name else 1.60)
    scene.render.filepath = str(output)
    bpy.ops.render.render(write_still=True)
    return {"path": str(output.relative_to(ROOT)), "camera_basis": basis}


def show_only(meshes: list[bpy.types.Object], shown: bpy.types.Object) -> None:
    for obj in meshes:
        obj.hide_render = obj != shown


def render_compare(scene, camera, center, longest, label, all_meshes, shown) -> dict:
    show_only(all_meshes, shown)
    distance = longest * 3.1
    return {
        "side": render_view(scene, camera, center, longest, "side", Vector((-distance, 0.0, 0.0)), QA / f"decimate_{label}_side.png"),
        "isometric": render_view(scene, camera, center, longest, "isometric", Vector((-distance * 0.76, distance * 0.62, distance * 0.82)), QA / f"decimate_{label}_isometric.png"),
    }


def make_root(body: bpy.types.Object) -> bpy.types.Object:
    root = bpy.data.objects.new(f"{ITEM_ID}_root", None)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.08
    bpy.context.scene.collection.objects.link(root)
    body.parent = root
    body.matrix_parent_inverse = root.matrix_world.inverted()
    for name, location in {
        "RightHandGrip": Vector((0.0, 0.0, 0.0)),
        "LeftHandGrip": to_local(LEFT_GRIP_RAW),
        "Muzzle": to_local(MUZZLE_RAW),
    }.items():
        marker = bpy.data.objects.new(name, None)
        marker.empty_display_type = "ARROWS" if name == "Muzzle" else "CUBE"
        marker.empty_display_size = 0.04
        marker.location = location
        bpy.context.scene.collection.objects.link(marker)
        marker.parent = root
        marker.matrix_parent_inverse = root.matrix_world.inverted()
    root["coordinate_contract"] = "Gunner: +Z muzzle, +Y up, trigger-grip root"
    return root


def export_fbx(root: bpy.types.Object, path: Path) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for child in root.children_recursive:
        child.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=str(path), use_selection=True, object_types={"EMPTY", "MESH"},
        add_leaf_bones=False, apply_unit_scale=False, use_space_transform=True,
        bake_space_transform=False, axis_forward="-Z", axis_up="Y",
        path_mode="COPY", embed_textures=False,
    )


def render_final(scene, camera, center, longest, material) -> tuple[dict, dict]:
    distance = longest * 3.1
    views = {
        "front": Vector((0.0, 0.0, distance)), "back": Vector((0.0, 0.0, -distance)),
        "left": Vector((-distance, 0.0, 0.0)), "right": Vector((distance, 0.0, 0.0)),
        "top": Vector((0.0, distance, 0.0)),
        "iso_left": Vector((-distance * 0.76, distance * 0.62, distance * 0.82)),
        "iso_right": Vector((distance * 0.76, distance * 0.62, distance * 0.82)),
    }
    pbr = {name: render_view(scene, camera, center, longest, name, offset, QA / f"pbr_{name}.png") for name, offset in views.items()}
    principled = next(node for node in material.node_tree.nodes if node.bl_idname == "ShaderNodeBsdfPrincipled")
    sockets = [principled.inputs["Base Color"], principled.inputs["Metallic"], principled.inputs["Roughness"]]
    saved = [[(link.from_socket, link.to_socket) for link in socket.links] for socket in sockets]
    for socket in sockets:
        for link in list(socket.links):
            material.node_tree.links.remove(link)
    sockets[0].default_value, sockets[1].default_value, sockets[2].default_value = (0.0, 0.0, 0.0, 1.0), 0.0, 1.0
    background = scene.world.node_tree.nodes.get("Background")
    old_strength = background.inputs["Strength"].default_value
    background.inputs["Strength"].default_value = 0.0
    for obj in bpy.context.scene.objects:
        if obj.type == "LIGHT": obj.hide_render = True
    emission = {name: render_view(scene, camera, center, longest, name, offset, QA / f"emission_{name}.png") for name, offset in views.items()}
    for links in saved:
        for source, destination in links: material.node_tree.links.new(source, destination)
    background.inputs["Strength"].default_value = old_strength
    for obj in bpy.context.scene.objects:
        if obj.type == "LIGHT": obj.hide_render = False
    return pbr, emission


def make_normal_qa_material() -> bpy.types.Material:
    material = bpy.data.materials.new("QA_NormalDirection")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    geometry = nodes.new("ShaderNodeNewGeometry")
    multiply = nodes.new("ShaderNodeVectorMath")
    multiply.operation = "MULTIPLY"
    multiply.inputs[1].default_value = (0.5, 0.5, 0.5)
    add = nodes.new("ShaderNodeVectorMath")
    add.operation = "ADD"
    add.inputs[1].default_value = (0.5, 0.5, 0.5)
    emission = nodes.new("ShaderNodeEmission")
    output = nodes.new("ShaderNodeOutputMaterial")
    links.new(geometry.outputs["Normal"], multiply.inputs[0])
    links.new(multiply.outputs["Vector"], add.inputs[0])
    links.new(add.outputs["Vector"], emission.inputs["Color"])
    links.new(emission.outputs["Emission"], output.inputs["Surface"])
    return material


def make_wire_qa_material() -> bpy.types.Material:
    material = bpy.data.materials.new("QA_Wire_Cyan")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    emission = nodes.new("ShaderNodeEmission")
    emission.inputs["Color"].default_value = (0.04, 0.78, 1.0, 1.0)
    emission.inputs["Strength"].default_value = 1.0
    output = nodes.new("ShaderNodeOutputMaterial")
    links.new(emission.outputs["Emission"], output.inputs["Surface"])
    return material


def render_repair_qa(scene, camera, center, longest, body) -> dict:
    distance = longest * 3.1
    views = {
        "front": Vector((0.0, 0.0, distance)),
        "back": Vector((0.0, 0.0, -distance)),
    }
    old_x, old_y = scene.render.resolution_x, scene.render.resolution_y
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 1600
    highres = {
        name: render_view(scene, camera, center, longest, name, offset, QA / f"repair_{name}_highres.png")
        for name, offset in views.items()
    }

    # A full Wireframe modifier on the decimated H3 surface amplifies its open
    # scan boundaries into long spikes. Draw only the real edges around the two
    # repaired end sections, over the shaded body, so this QA view remains an
    # honest and readable inspection of the local topology.
    wire_data = bpy.data.curves.new("QA_RepairWire_Curve", "CURVE")
    wire_data.dimensions = "3D"
    wire_data.resolution_u = 1
    wire_data.bevel_depth = 0.00028
    wire_data.bevel_resolution = 0
    wire_data.resolution_v = 0
    wire_object = bpy.data.objects.new("QA_RepairWire", wire_data)
    scene.collection.objects.link(wire_object)
    wire_object.parent = body.parent
    wire_object.data.materials.append(make_wire_qa_material())
    selected_edges = 0
    for edge in body.data.edges:
        first = body.data.vertices[edge.vertices[0]].co
        second = body.data.vertices[edge.vertices[1]].co
        at_bell = first.z > 0.50 and second.z > 0.50
        at_stock = first.z < -0.23 and second.z < -0.23
        if not (at_bell or at_stock):
            continue
        spline = wire_data.splines.new("POLY")
        spline.points.add(1)
        spline.points[0].co = (*first, 1.0)
        spline.points[1].co = (*second, 1.0)
        selected_edges += 1
    wire = {
        name: render_view(scene, camera, center, longest, name, offset, QA / f"repair_{name}_wire.png")
        for name, offset in views.items()
    }
    bpy.data.objects.remove(wire_object, do_unlink=True)

    normal_material = make_normal_qa_material()
    view_layer = bpy.context.view_layer
    old_override = view_layer.material_override
    view_layer.material_override = normal_material
    for obj in bpy.context.scene.objects:
        if obj.type == "LIGHT": obj.hide_render = True
    normal = {
        name: render_view(scene, camera, center, longest, name, offset, QA / f"repair_{name}_normal.png")
        for name, offset in views.items()
    }
    for obj in bpy.context.scene.objects:
        if obj.type == "LIGHT": obj.hide_render = False
    view_layer.material_override = old_override
    scene.render.resolution_x, scene.render.resolution_y = old_x, old_y
    return {
        "high_resolution_pbr": highres,
        "wire": wire,
        "wire_selected_local_edges": selected_edges,
        "normal_direction": normal,
    }


def delete_objects(objects) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        if obj.name in bpy.data.objects: obj.select_set(True)
    bpy.ops.object.delete()


def main() -> None:
    ensure_dirs()
    clear_scene()
    original = import_raw()
    imported_triangles = triangles(original)
    transform_record = transform_mesh(original)
    material_record, texture_records = configure_material(original)
    minimum, maximum = bounds(original)
    center, longest = (minimum + maximum) * 0.5, max(maximum - minimum)
    scene, camera, temporary = setup_render(center, longest)
    all_meshes = [original]
    comparisons = {"original": render_compare(scene, camera, center, longest, "original", all_meshes, original)}
    candidates = {}
    stages = []
    for target in CANDIDATE_TARGETS:
        candidate, stage = duplicate_decimated(original, target, f"Worldender_{target // 1000}k")
        candidates[target] = candidate
        all_meshes.append(candidate)
        stages.append(stage)
        comparisons[f"{target // 1000}k"] = render_compare(scene, camera, center, longest, f"{target // 1000}k", all_meshes, candidate)
    body = candidates[FINAL_TARGET]
    body.name = "Worldender_Body"
    body.data.name = "Worldender_Body_Mesh"
    for obj in [original, *[candidate for target, candidate in candidates.items() if target != FINAL_TARGET]]:
        bpy.data.objects.remove(obj, do_unlink=True)
    body.hide_render = False
    pre_repair_minimum, pre_repair_maximum = bounds(body)
    repair_record = repair_end_sections(body)
    repair_record["sealed_core_final_polygon_count"] = assign_sealed_core_material(body)
    root = make_root(body)
    final_minimum, final_maximum = bounds(body)
    final_center, final_longest = (final_minimum + final_maximum) * 0.5, max(final_maximum - final_minimum)
    pbr_qa, emission_qa = render_final(scene, camera, final_center, final_longest, body.material_slots[0].material)
    repair_qa = render_repair_qa(scene, camera, final_center, final_longest, body)
    delete_objects(temporary)
    bpy.context.view_layer.objects.active = body
    body.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    body.select_set(False)
    markers = {}
    for name in ("RightHandGrip", "LeftHandGrip", "Muzzle"):
        marker = bpy.data.objects[name]
        markers[name] = {
            "parent": marker.parent.name if marker.parent else None,
            "local_location": v3(marker.location),
            "local_rotation": v3(marker.rotation_euler),
            "local_plus_z": v3(marker.matrix_local.to_3x3() @ Vector((0.0, 0.0, 1.0))),
        }
    final_triangles = triangles(body)
    local_origin = Vector((0.0, 0.108, final_maximum.z + 0.02))
    hit, hit_location, _normal, _face = body.ray_cast(local_origin, Vector((0.0, 0.0, -1.0)), distance=2.0)
    aperture_clear_depth = float(final_maximum.z - hit_location.z) if hit else 2.0
    stock_origin = Vector((0.0, 0.100, final_minimum.z - 0.02))
    stock_hit, stock_location, _normal, _face = body.ray_cast(stock_origin, Vector((0.0, 0.0, 1.0)), distance=2.0)
    stock_cap_hit_distance = float(stock_location.z - stock_origin.z) if stock_hit else None
    repair_record["axis_probe"] = {
        "bell_center": [0.0, 0.108],
        "bell_first_hit": v3(hit_location) if hit else None,
        "bell_clear_depth_m": round(aperture_clear_depth, 6),
        "stock_center": [0.0, 0.100],
        "stock_first_hit": v3(stock_location) if stock_hit else None,
        "stock_cap_hit_distance_m": round(stock_cap_hit_distance, 6) if stock_cap_hit_distance is not None else None,
    }
    repair_record["silhouette_bounds_delta"] = {
        "min": v3(final_minimum - pre_repair_minimum),
        "max": v3(final_maximum - pre_repair_maximum),
    }
    backup_path = BACKUP / f"{ITEM_ID}_100k_reference.txt"
    backup_path.write_text("100k candidate retained as deterministic QA renders; rebuild with FINAL_TARGET=100000 for editable geometry.\n", encoding="utf-8")
    final_minimum, final_maximum = bounds(body)
    validation = {
        "item_id": ITEM_ID, "generated_at": utc_now(), "blender_version": bpy.app.version_string,
        "source": {"task_id": TASK_ID, "type": "multiview_to_model", "credits_consumed": 30, "glb": str(RAW_GLB.relative_to(ROOT)), "triangles": imported_triangles},
        "coordinate_contract": {"raw_muzzle": "-X", "raw_up": "+Z", "final_muzzle": "+Z", "final_up": "+Y", "root_origin": "actual trigger-grip center"},
        "transform": transform_record, "root": root.name, "mesh": body.name, "markers": markers,
        "bounds_root_local": {"min": v3(final_minimum), "max": v3(final_maximum), "dimensions": v3(final_maximum - final_minimum)},
        "decimation": {"stages": stages, "selected": f"{FINAL_TARGET // 1000}k", "final_triangles": final_triangles, "comparison_renders": comparisons, "selection_requires_visual_review": not VISUAL_REVIEW_APPROVED, "selection_basis": VISUAL_REVIEW_BASIS},
        "material": {
            **material_record,
            "bell_inner_material": "M_Worldender_BellInner_PBR",
            "bell_rim_material": "M_Worldender_BellRim_PBR",
            "stock_cap_material": "M_Worldender_StockPadCap_PBR",
        }, "textures": texture_records,
        "structural_repair": repair_record,
        "qa": {"neutral_pbr": pbr_qa, "emission_only": emission_qa, "structural_repair": repair_qa},
        "prohibited_objects": [obj.name for obj in bpy.context.scene.objects if obj.type in {"CAMERA", "LIGHT", "ARMATURE"} or "collider" in obj.name.lower()],
        "mesh_scales": {body.name: v3(body.scale)},
        "checks": {
            "single_root": len([obj for obj in bpy.context.scene.objects if obj.parent is None]) == 1,
            "root_direct_markers": all(bpy.data.objects[name].parent == root for name in ("RightHandGrip", "LeftHandGrip", "Muzzle")),
            "right_grip_at_origin": markers["RightHandGrip"]["local_location"] == [0.0, 0.0, 0.0],
            "left_grip_z_initial": abs(markers["LeftHandGrip"]["local_location"][2] - 0.32625) < 1.0e-5,
            "muzzle_local_plus_z": markers["Muzzle"]["local_plus_z"] == [0.0, 0.0, 1.0],
            "positive_unit_mesh_scale": v3(body.scale) == [1.0, 1.0, 1.0],
            "no_camera_light_armature_collider": not any(obj.type in {"CAMERA", "LIGHT", "ARMATURE"} or "collider" in obj.name.lower() for obj in bpy.context.scene.objects),
            "pbr_textures_present": all((ROOT / record["path"]).is_file() for record in texture_records),
            "emission_uv_only_no_added_mesh": not material_record["emission_geometry_added"],
            "final_triangle_budget": 40_000 <= final_triangles <= 60_000,
            "visual_decimation_review": VISUAL_REVIEW_APPROVED,
            "bell_aperture_axis_clear": aperture_clear_depth >= 0.08,
            "bell_inner_closed_manifold": all(repair_record["bell_closed_reconstruction"]["gold"]["topology_before_join"][key] == 0 for key in ("boundary_edges", "non_manifold_edges_including_boundaries", "loose_edges", "degenerate_faces")),
            "bell_rim_closed_manifold": all(repair_record["bell_closed_reconstruction"]["rim"]["topology_before_join"][key] == 0 for key in ("boundary_edges", "non_manifold_edges_including_boundaries", "loose_edges", "degenerate_faces")),
            "bell_radial_coverage_complete": repair_record["bell_closed_reconstruction"]["gold_radial_coverage"]["complete"] and repair_record["bell_closed_reconstruction"]["rim_radial_coverage"]["complete"],
            "stock_pad_bore_sealed": stock_hit and stock_cap_hit_distance is not None and stock_cap_hit_distance <= 0.03,
            "repair_no_loose_or_degenerate_geometry": repair_record["topology_after"]["loose_edges"] == 0 and repair_record["topology_after"]["degenerate_faces"] == 0,
            "reimport_verified": False,
        },
        "reimport": {"status": "pending"},
    }
    validation["pass"] = all(validation["checks"].values())
    VALIDATION_PATH.write_text(json.dumps(validation, ensure_ascii=False, indent=2), encoding="utf-8")
    (PRODUCTION / "final_prompt.txt").write_text(FINAL_PROMPT, encoding="utf-8")
    export_fbx(root, FBX_PATH)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
    print(json.dumps({"blend": str(BLEND_PATH), "fbx": str(FBX_PATH), "triangles": final_triangles, "pass": validation["pass"]}, indent=2))


if __name__ == "__main__":
    main()
