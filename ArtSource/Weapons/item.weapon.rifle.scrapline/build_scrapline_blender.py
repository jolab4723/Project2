from __future__ import annotations

import json
import subprocess
from datetime import datetime, timezone
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


ROOT = Path(__file__).resolve().parent
ITEM_ID = "item.weapon.rifle.scrapline"
TASK_ID = "759ee7dd-5700-4d00-9a14-790ece210a1c"
RAW_GLB = ROOT / "Tripo" / "Downloaded" / f"{ITEM_ID}_raw.glb"
PRODUCTION = ROOT / "Production"
TEXTURES = PRODUCTION / "Textures"
QA = PRODUCTION / "QA"
BACKUP = PRODUCTION / "Backup"
BLEND_PATH = PRODUCTION / f"{ITEM_ID}.blend"
FBX_PATH = PRODUCTION / f"{ITEM_ID}.fbx"
VALIDATION_PATH = PRODUCTION / "validation.json"

# H3 raw +X is the approved-view muzzle direction, +Z is up, +Y is width.
# Final Gunner contract: +Z muzzle, +Y up, +X width. The proper rotation is
# (X,Y,Z)_raw -> (Y,Z,X)_final; determinant is +1.
MODEL_SCALE = 0.83653846
ROOT_GRIP_RAW = Vector((0.3700, 0.0, -0.1100))
LEFT_GRIP_RAW = Vector((-0.0200, 0.0, 0.0450))
MUZZLE_RAW = Vector((-0.5000, 0.0, 0.0150))
FINAL_TARGET = 40_000
CANDIDATE_TARGETS = (100_000, 60_000, 50_000, 40_000)
VISUAL_REVIEW_APPROVED = True
VISUAL_REVIEW_BASIS = "Direct 1024px side/isometric review found 40k silhouette, octagonal emitter, long foregrip, activation lever, panel seams and status window visually equivalent to original; lowest lossless candidate selected."
EMISSION_MODE = "cyan"
EMISSION_STRENGTH = 2.0
EMISSION_ENVELOPE = {
    "space": "final root-local",
    "min": [-1.0, -1.0, -1.0],
    "max": [1.0, 1.0, 1.0],
    "description": "all authored UV islands; strict cyan threshold uniquely selects the tiny status window",
}
FINAL_PROMPT = """item.weapon.rifle.scrapline — Common Gunner rifle.
Approved H3 multiview source: simple fictional industrial energy carbine, salvaged alloy and worn black polymer, restrained yellow safety accents, one tiny cyan status window. No real firearm receiver, ammunition magazine, ejection port, iron sight, or muzzle brake. Production preserves the generated physical silhouette and PBR texture set, reconstructs emission only for the hard-bounded cyan status-window UV pixels, adds no geometry or floating effects, uses +Z muzzle/+Y up, and places both hand markers at real contact areas.
"""


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
    body.name = "Scrapline_Body"
    body.data.name = "Scrapline_Body_Mesh"
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
    emission.name = "Scrapline_Status_Emission"
    emission.colorspace_settings.name = "sRGB"
    report = json.loads(report_path.read_text(encoding="utf-8"))
    report.update({
        "path": str(output_path.relative_to(ROOT)),
        "regions_path": str(regions_path.relative_to(ROOT)),
        "report_path": str(report_path.relative_to(ROOT)),
    })
    return emission, report


def configure_material(obj: bpy.types.Object) -> tuple[dict, list[dict]]:
    if len(obj.material_slots) != 1 or obj.material_slots[0].material is None:
        raise RuntimeError("Expected exactly one imported PBR material")
    material = obj.material_slots[0].material
    material.name = "M_Scrapline_PBR"
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
    node = material.node_tree.nodes.new("ShaderNodeTexImage")
    node.name = "Scrapline Status Emission Map"
    node.image = emission
    node.interpolation = "Linear"
    material.node_tree.links.new(node.outputs["Color"], principled.inputs["Emission Color"])
    principled.inputs["Emission Strength"].default_value = EMISSION_STRENGTH
    principled.inputs["Alpha"].default_value = 1.0
    return ({
        "name": material.name,
        "base_color_texture": color.name,
        "normal_texture": normal.name,
        "orm_texture": orm.name,
        "emission_texture": emission.name,
        "emission_strength": EMISSION_STRENGTH,
        "emission_geometry_added": False,
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
        candidate, stage = duplicate_decimated(original, target, f"Scrapline_{target // 1000}k")
        candidates[target] = candidate
        all_meshes.append(candidate)
        stages.append(stage)
        comparisons[f"{target // 1000}k"] = render_compare(scene, camera, center, longest, f"{target // 1000}k", all_meshes, candidate)
    body = candidates[FINAL_TARGET]
    body.name = "Scrapline_Body"
    body.data.name = "Scrapline_Body_Mesh"
    for obj in [original, *[candidate for target, candidate in candidates.items() if target != FINAL_TARGET]]:
        bpy.data.objects.remove(obj, do_unlink=True)
    body.hide_render = False
    root = make_root(body)
    final_minimum, final_maximum = bounds(body)
    final_center, final_longest = (final_minimum + final_maximum) * 0.5, max(final_maximum - final_minimum)
    pbr_qa, emission_qa = render_final(scene, camera, final_center, final_longest, body.material_slots[0].material)
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
        "material": material_record, "textures": texture_records,
        "qa": {"neutral_pbr": pbr_qa, "emission_only": emission_qa},
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
