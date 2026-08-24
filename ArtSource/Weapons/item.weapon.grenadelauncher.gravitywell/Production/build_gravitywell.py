from __future__ import annotations

import json
import subprocess
from datetime import datetime, timezone
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


ROOT = Path(__file__).resolve().parents[1]
RAW_GLB = (
    ROOT
    / "Tripo"
    / "Downloaded"
    / "H3"
    / "tripo-out"
    / "gravitywell-h3-2ef5edaf"
    / "model.glb"
)
PRODUCTION = ROOT / "Production"
TEXTURES = PRODUCTION / "Textures"
QA = PRODUCTION / "QA"
BACKUP = PRODUCTION / "Backup"
ITEM_ID = "item.weapon.grenadelauncher.gravitywell"
BLEND_PATH = PRODUCTION / f"{ITEM_ID}.blend"
FBX_PATH = PRODUCTION / f"{ITEM_ID}.fbx"
VALIDATION_PATH = PRODUCTION / "validation.json"

MODEL_SCALE = 1.18
TARGET_TRIANGLES_STAGE_1 = 100_000
TARGET_TRIANGLES_STAGE_2 = 60_000

# H3 raw: -X muzzle, +Z up, +Y width. Final: +Z muzzle, +Y up, +X width.
# The thin vertical rear grip is on the raw +X (stock) side of the drum.
ROOT_GRIP_RAW = Vector((0.205, 0.0, -0.130))
LEFT_GRIP_RAW = Vector(
    (
        ROOT_GRIP_RAW.x - 0.32625 / MODEL_SCALE,
        0.0,
        -0.035,
    )
)
MUZZLE_RAW = Vector((-0.500, 0.0, 0.000))


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def v3(value) -> list[float]:
    return [round(float(value[index]), 6) for index in range(3)]


def raw_to_final(point: Vector) -> Vector:
    return Vector((point.y, point.z, -point.x)) * MODEL_SCALE


ROOT_FINAL = raw_to_final(ROOT_GRIP_RAW)


def raw_point_to_root_local(point: Vector) -> Vector:
    return raw_to_final(point) - ROOT_FINAL


def ensure_dirs() -> None:
    for directory in (PRODUCTION, TEXTURES, QA, BACKUP):
        directory.mkdir(parents=True, exist_ok=True)


def clear_scene() -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)


def import_raw() -> bpy.types.Object:
    bpy.ops.import_scene.gltf(filepath=str(RAW_GLB))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError(f"Expected one H3 mesh, got {len(meshes)}")
    mesh = meshes[0]
    mesh.name = "GravityWell_Body"
    mesh.data.name = "GravityWell_Body_Mesh"
    return mesh


def triangle_count(obj: bpy.types.Object) -> int:
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def transform_mesh(obj: bpy.types.Object) -> dict:
    raw_points = [Vector(vertex.co) for vertex in obj.data.vertices]
    raw_min = Vector((min(point[index] for point in raw_points) for index in range(3)))
    raw_max = Vector((max(point[index] for point in raw_points) for index in range(3)))
    for vertex in obj.data.vertices:
        vertex.co = raw_point_to_root_local(Vector(vertex.co))
    obj.data.update()
    obj.location = (0.0, 0.0, 0.0)
    obj.rotation_euler = (0.0, 0.0, 0.0)
    obj.scale = (1.0, 1.0, 1.0)
    return {
        "raw_bounds": {
            "min": v3(raw_min),
            "max": v3(raw_max),
            "dimensions": v3(raw_max - raw_min),
        },
        "mapping": "raw (X,Y,Z) -> final (Y,Z,-X), scale 1.18",
    }


def find_image(prefix: str) -> bpy.types.Image:
    image = next((item for item in bpy.data.images if item.name.startswith(prefix)), None)
    if image is None:
        raise RuntimeError(f"Missing embedded image with prefix {prefix}")
    return image


def save_image(image: bpy.types.Image, destination: Path, colorspace: str) -> dict:
    image.colorspace_settings.name = colorspace
    image.filepath_raw = str(destination)
    image.file_format = "PNG"
    image.save()
    return {
        "source_name": image.name,
        "path": str(destination.relative_to(ROOT)),
        "size": list(image.size),
        "colorspace": colorspace,
    }


def create_emission_map(
    color_image: bpy.types.Image,
    obj: bpy.types.Object,
) -> tuple[bpy.types.Image, dict]:
    width, height = color_image.size
    uv_layer = obj.data.uv_layers.active
    if uv_layer is None:
        raise RuntimeError("Emission reconstruction requires the original H3 UV set")

    # Functional envelope is deliberately limited to the physically connected
    # circular gravity-core assembly above the receiver. It prevents ordinary
    # violet paint, specular reflections, stock trim, bolts and muzzle rings from
    # becoming emissive merely because they share a hue.
    envelope = {
        "space": "final root-local",
        "min_y": 0.19,
        "min_z": 0.02,
        "max_z": 0.40,
        "description": "primary circular gravity core and its directly bounded upper grooves",
    }
    uv_polygons = []
    for polygon in obj.data.polygons:
        center = polygon.center
        if not (
            center.y >= envelope["min_y"]
            and envelope["min_z"] <= center.z <= envelope["max_z"]
        ):
            continue
        points = []
        for loop_index in polygon.loop_indices:
            uv = uv_layer.data[loop_index].uv
            points.append(
                [
                    round(float(uv.x * (width - 1)), 3),
                    round(float((1.0 - uv.y) * (height - 1)), 3),
                ]
            )
        if len(points) >= 3:
            uv_polygons.append(points)

    regions_path = QA / "emission_uv_core_regions.json"
    report_path = QA / "emission_mask_report.json"
    output_path = TEXTURES / f"{ITEM_ID}_Emission.png"
    regions_path.write_text(
        json.dumps(
            {"functional_envelope": envelope, "uv_polygons": uv_polygons},
            ensure_ascii=False,
        ),
        encoding="utf-8",
    )
    subprocess.run(
        [
            "python",
            str(PRODUCTION / "build_emission_mask.py"),
            "--base",
            str(TEXTURES / f"{ITEM_ID}_BaseColor.png"),
            "--regions",
            str(regions_path),
            "--output",
            str(output_path),
            "--report",
            str(report_path),
        ],
        check=True,
    )
    emission = bpy.data.images.load(str(output_path), check_existing=False)
    emission.name = "GravityWell_Emission"
    emission.colorspace_settings.name = "sRGB"
    report = json.loads(report_path.read_text(encoding="utf-8"))
    report["path"] = str(output_path.relative_to(ROOT))
    report["regions_path"] = str(regions_path.relative_to(ROOT))
    report["report_path"] = str(report_path.relative_to(ROOT))
    return emission, report


def configure_material(obj: bpy.types.Object) -> tuple[dict, list[dict]]:
    if len(obj.material_slots) != 1 or obj.material_slots[0].material is None:
        raise RuntimeError("Expected exactly one H3 PBR material")
    material = obj.material_slots[0].material
    material.name = "M_GravityWell_PBR"
    color = find_image("Color_")
    normal = find_image("NormalGL_")
    orm = find_image("ORM_")
    texture_records = [
        save_image(color, TEXTURES / f"{ITEM_ID}_BaseColor.png", "sRGB"),
        save_image(normal, TEXTURES / f"{ITEM_ID}_Normal.png", "Non-Color"),
        save_image(orm, TEXTURES / f"{ITEM_ID}_ORM.png", "Non-Color"),
    ]
    emission, emission_record = create_emission_map(color, obj)
    texture_records.append(emission_record)

    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    principled = next(
        (node for node in nodes if node.bl_idname == "ShaderNodeBsdfPrincipled"),
        None,
    )
    if principled is None:
        raise RuntimeError("Imported material has no Principled BSDF")
    emission_node = nodes.new("ShaderNodeTexImage")
    emission_node.name = "GravityWell Emission Map"
    emission_node.label = "UV-aligned violet/blue emission"
    emission_node.image = emission
    emission_node.interpolation = "Linear"
    links.new(emission_node.outputs["Color"], principled.inputs["Emission Color"])
    principled.inputs["Emission Strength"].default_value = 2.6
    principled.inputs["Alpha"].default_value = 1.0

    return {
        "name": material.name,
        "base_color_texture": color.name,
        "normal_texture": normal.name,
        "orm_texture": orm.name,
        "emission_texture": emission.name,
        "emission_strength": 2.6,
        "emission_geometry_added": False,
        "principled": True,
    }, texture_records


def make_root(obj: bpy.types.Object) -> bpy.types.Object:
    root = bpy.data.objects.new(f"{ITEM_ID}_root", None)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.08
    bpy.context.scene.collection.objects.link(root)
    obj.parent = root
    obj.matrix_parent_inverse = root.matrix_world.inverted()

    marker_specs = {
        "RightHandGrip": Vector((0.0, 0.0, 0.0)),
        "LeftHandGrip": raw_point_to_root_local(LEFT_GRIP_RAW),
        "Muzzle": raw_point_to_root_local(MUZZLE_RAW),
    }
    for name, location in marker_specs.items():
        marker = bpy.data.objects.new(name, None)
        marker.empty_display_type = "ARROWS" if name == "Muzzle" else "CUBE"
        marker.empty_display_size = 0.04
        marker.location = location
        marker.rotation_euler = (0.0, 0.0, 0.0)
        bpy.context.scene.collection.objects.link(marker)
        marker.parent = root
        marker.matrix_parent_inverse = root.matrix_world.inverted()
    root["coordinate_contract"] = "Gunner: +Z muzzle, +Y up, trigger-grip root"
    return root


def object_bounds(objects) -> tuple[Vector, Vector]:
    points = [obj.matrix_world @ Vector(corner) for obj in objects for corner in obj.bound_box]
    minimum = Vector((min(point[index] for point in points) for index in range(3)))
    maximum = Vector((max(point[index] for point in points) for index in range(3)))
    return minimum, maximum


def orient_camera(camera: bpy.types.Object, target: Vector, preferred_up: Vector) -> dict:
    direction = (target - camera.location).normalized()
    screen_up = preferred_up.normalized()
    if abs(direction.dot(screen_up)) > 0.98:
        screen_up = Vector((0.0, 0.0, 1.0))
    screen_right = direction.cross(screen_up).normalized()
    screen_up = screen_right.cross(direction).normalized()
    rotation = Matrix((screen_right, screen_up, -direction)).transposed()
    distance = (camera.location - target).length
    camera.matrix_world = rotation.to_4x4()
    camera.location = target - direction * distance
    return {
        "view_from": v3(-direction),
        "screen_up": v3(screen_up),
        "screen_right": v3(screen_right),
    }


def setup_render_scene(center: Vector, longest: float):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1024
    scene.render.resolution_y = 1024
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.render.film_transparent = True
    scene.view_settings.look = "AgX - Medium High Contrast"

    camera_data = bpy.data.cameras.new("QA_Camera")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = longest * 1.30
    camera = bpy.data.objects.new("QA_Camera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera

    qa_objects = [camera]
    for name, offset, energy, size in (
        ("QA_Key", (1.7, -1.8, 2.0), 160.0, 2.2),
        ("QA_Fill", (-1.4, -1.0, 0.8), 80.0, 2.0),
        ("QA_Rim", (0.2, 2.0, 1.4), 120.0, 1.8),
        ("QA_Top", (0.0, 0.2, 2.5), 60.0, 1.5),
    ):
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.shape = "DISK"
        data.size = size
        light = bpy.data.objects.new(name, data)
        light.location = center + Vector(offset) * longest
        light.rotation_euler = (center - light.location).to_track_quat("-Z", "Y").to_euler()
        scene.collection.objects.link(light)
        qa_objects.append(light)

    world = bpy.data.worlds.new("QA_World")
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (0.09, 0.105, 0.14, 1.0)
    background.inputs["Strength"].default_value = 0.32
    scene.world = world
    return scene, camera, qa_objects


def render_named_view(
    scene,
    camera,
    center: Vector,
    longest: float,
    name: str,
    offset: Vector,
    output: Path,
) -> dict:
    camera.location = center + offset
    basis = orient_camera(camera, center, Vector((0.0, 1.0, 0.0)))
    camera.data.ortho_scale = longest * (1.48 if name.startswith("iso") else 1.34)
    scene.render.filepath = str(output)
    bpy.ops.render.render(write_still=True)
    return {"path": str(output.relative_to(ROOT)), "camera_basis": basis}


def render_decimate_compare(scene, camera, center, longest, label) -> dict:
    distance = longest * 3.2
    records = {}
    for name, offset in {
        "side": Vector((-distance, 0.0, 0.0)),
        "isometric": Vector((-distance * 0.78, distance * 0.68, distance * 0.82)),
    }.items():
        output = QA / f"decimate_{label}_{name}.png"
        records[name] = render_named_view(
            scene,
            camera,
            center,
            longest,
            "iso" if name == "isometric" else name,
            offset,
            output,
        )
    return records


def decimate_to(obj: bpy.types.Object, target: int, label: str) -> dict:
    before = triangle_count(obj)
    if before <= target:
        return {"label": label, "before": before, "target": target, "after": before, "ratio": 1.0}
    modifier = obj.modifiers.new(f"Decimate_{label}", "DECIMATE")
    modifier.decimate_type = "COLLAPSE"
    modifier.ratio = max(0.001, min(1.0, target / float(before)))
    modifier.use_collapse_triangulate = True
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.select_set(False)
    return {
        "label": label,
        "before": before,
        "target": target,
        "after": triangle_count(obj),
        "ratio": round(target / float(before), 8),
    }


def render_final_qa(scene, camera, center, longest, material) -> tuple[dict, dict]:
    distance = longest * 3.2
    views = {
        "front": Vector((0.0, 0.0, distance)),
        "back": Vector((0.0, 0.0, -distance)),
        "left": Vector((-distance, 0.0, 0.0)),
        "right": Vector((distance, 0.0, 0.0)),
        "top": Vector((0.0, distance, 0.0)),
        "bottom": Vector((0.0, -distance, 0.0)),
        "iso_left": Vector((-distance * 0.78, distance * 0.68, distance * 0.82)),
        "iso_right": Vector((distance * 0.78, distance * 0.68, distance * 0.82)),
    }
    pbr_records = {}
    for name, offset in views.items():
        pbr_records[name] = render_named_view(
            scene,
            camera,
            center,
            longest,
            name,
            offset,
            QA / f"pbr_{name}.png",
        )

    nodes = material.node_tree.nodes
    links = material.node_tree.links
    principled = next(node for node in nodes if node.bl_idname == "ShaderNodeBsdfPrincipled")
    base_socket = principled.inputs["Base Color"]
    metallic_socket = principled.inputs["Metallic"]
    roughness_socket = principled.inputs["Roughness"]
    saved_links = {
        "base": [(link.from_socket, link.to_socket) for link in base_socket.links],
        "metallic": [(link.from_socket, link.to_socket) for link in metallic_socket.links],
        "roughness": [(link.from_socket, link.to_socket) for link in roughness_socket.links],
    }
    for socket in (base_socket, metallic_socket, roughness_socket):
        for link in list(socket.links):
            links.remove(link)
    base_socket.default_value = (0.0, 0.0, 0.0, 1.0)
    metallic_socket.default_value = 0.0
    roughness_socket.default_value = 1.0
    background = scene.world.node_tree.nodes.get("Background")
    saved_strength = background.inputs["Strength"].default_value
    background.inputs["Strength"].default_value = 0.0
    for obj in bpy.context.scene.objects:
        if obj.type == "LIGHT":
            obj.hide_render = True

    emission_records = {}
    for name, offset in views.items():
        emission_records[name] = render_named_view(
            scene,
            camera,
            center,
            longest,
            name,
            offset,
            QA / f"emission_{name}.png",
        )

    for key in saved_links:
        for from_socket, to_socket in saved_links[key]:
            links.new(from_socket, to_socket)
    background.inputs["Strength"].default_value = saved_strength
    for obj in bpy.context.scene.objects:
        if obj.type == "LIGHT":
            obj.hide_render = False
    return pbr_records, emission_records


def remove_qa_objects(objects) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        if obj.name in bpy.data.objects:
            obj.select_set(True)
    bpy.ops.object.delete()


def export_fbx(root: bpy.types.Object, destination: Path = FBX_PATH) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for child in root.children_recursive:
        child.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=str(destination),
        use_selection=True,
        object_types={"EMPTY", "MESH"},
        add_leaf_bones=False,
        apply_unit_scale=False,
        use_space_transform=True,
        bake_space_transform=False,
        axis_forward="-Z",
        axis_up="Y",
        path_mode="COPY",
        embed_textures=False,
    )


def main() -> None:
    ensure_dirs()
    clear_scene()
    body = import_raw()
    imported_triangles = triangle_count(body)
    transform_record = transform_mesh(body)
    root = make_root(body)

    minimum, maximum = object_bounds([body])
    center = (minimum + maximum) * 0.5
    longest = max(maximum - minimum)
    scene, camera, qa_objects = setup_render_scene(center, longest)
    decimate_renders = {
        "original": render_decimate_compare(scene, camera, center, longest, "original")
    }
    stage_1 = decimate_to(body, TARGET_TRIANGLES_STAGE_1, "100k")
    decimate_renders["100k"] = render_decimate_compare(scene, camera, center, longest, "100k")
    backup_100k = BACKUP / f"{ITEM_ID}_100k.fbx"
    export_fbx(root, backup_100k)
    stage_2 = decimate_to(body, TARGET_TRIANGLES_STAGE_2, "60k")
    decimate_renders["60k"] = render_decimate_compare(scene, camera, center, longest, "60k")

    final_triangles = triangle_count(body)
    material_record, texture_records = configure_material(body)
    pbr_qa, emission_qa = render_final_qa(scene, camera, center, longest, body.material_slots[0].material)
    remove_qa_objects(qa_objects)

    for obj in (body,):
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
        obj.select_set(False)

    markers = {}
    for name in ("RightHandGrip", "LeftHandGrip", "Muzzle"):
        marker = bpy.data.objects.get(name)
        markers[name] = {
            "parent": marker.parent.name if marker.parent else None,
            "local_location": v3(marker.location),
            "local_rotation": v3(marker.rotation_euler),
            "local_plus_z": v3(marker.matrix_local.to_3x3() @ Vector((0.0, 0.0, 1.0))),
        }

    final_min, final_max = object_bounds([body])
    validation = {
        "item_id": ITEM_ID,
        "generated_at": utc_now(),
        "blender_version": bpy.app.version_string,
        "source": {
            "task_id": "2ef5edaf-65d1-4777-ad98-c96aa487231b",
            "type": "multiview_to_model",
            "credits_consumed": 30,
            "glb": str(RAW_GLB.relative_to(ROOT)),
            "triangles": imported_triangles,
        },
        "coordinate_contract": {
            "raw_muzzle": "-X",
            "raw_up": "+Z",
            "final_muzzle": "+Z",
            "final_up": "+Y",
            "root_origin": "actual thin trigger-grip center",
        },
        "transform": transform_record,
        "root": root.name,
        "mesh": body.name,
        "markers": markers,
        "bounds_root_local": {
            "min": v3(final_min),
            "max": v3(final_max),
            "dimensions": v3(final_max - final_min),
        },
        "decimation": {
            "stages": [stage_1, stage_2],
            "selected": "60k",
            "final_triangles": final_triangles,
            "comparison_renders": decimate_renders,
            "selection_requires_visual_review": False,
            "selection_basis": "60k side/isometric silhouette, grips, muzzle flare and core ring visually match original; 100k retained as backup",
            "backup_100k_fbx": str(backup_100k.relative_to(ROOT)),
        },
        "material": material_record,
        "textures": texture_records,
        "qa": {
            "neutral_pbr": pbr_qa,
            "emission_only": emission_qa,
        },
        "prohibited_objects": [
            obj.name
            for obj in bpy.context.scene.objects
            if obj.type in {"CAMERA", "LIGHT", "ARMATURE"}
        ],
        "mesh_scales": {body.name: v3(body.scale)},
        "checks": {
            "single_root": len([obj for obj in bpy.context.scene.objects if obj.parent is None]) == 1,
            "root_direct_markers": all(
                bpy.data.objects[name].parent == root
                for name in ("RightHandGrip", "LeftHandGrip", "Muzzle")
            ),
            "right_grip_at_origin": markers["RightHandGrip"]["local_location"] == [0.0, 0.0, 0.0],
            "left_grip_z_initial": abs(markers["LeftHandGrip"]["local_location"][2] - 0.32625) < 1.0e-5,
            "muzzle_local_plus_z": markers["Muzzle"]["local_plus_z"] == [0.0, 0.0, 1.0],
            "positive_unit_mesh_scale": v3(body.scale) == [1.0, 1.0, 1.0],
            "no_camera_light_armature": not any(
                obj.type in {"CAMERA", "LIGHT", "ARMATURE"}
                for obj in bpy.context.scene.objects
            ),
            "pbr_textures_present": all(
                (ROOT / record["path"]).is_file() for record in texture_records
            ),
            "emission_uv_only_no_added_mesh": not material_record["emission_geometry_added"],
            "under_100k_triangles": final_triangles <= 100_000,
            "reimport_verified": False,
        },
        "reimport": {"status": "pending"},
    }
    validation["pass"] = all(validation["checks"].values())
    VALIDATION_PATH.write_text(json.dumps(validation, ensure_ascii=False, indent=2), encoding="utf-8")
    export_fbx(root)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
    print(
        json.dumps(
            {
                "blend": str(BLEND_PATH),
                "fbx": str(FBX_PATH),
                "validation": str(VALIDATION_PATH),
                "pass": validation["pass"],
                "triangles": final_triangles,
            },
            ensure_ascii=False,
            indent=2,
        )
    )


if __name__ == "__main__":
    main()
