from __future__ import annotations

import json
from datetime import datetime, timezone
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


PRODUCTION = Path(__file__).resolve().parent
ROOT = PRODUCTION.parent
ITEM_ID = "item.weapon.rifle.smilesignal"
FBX = PRODUCTION / f"{ITEM_ID}.fbx"
VALIDATION = PRODUCTION / "validation.json"
QA = PRODUCTION / "QA" / "Reimport"
REPORT = QA / "reimport_validation.json"
TEXTURES = PRODUCTION / "Textures"


def v3(value):
    return [round(float(value[index]), 6) for index in range(3)]


def triangle_count(obj):
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def root_local_point(root, obj, point):
    return root.matrix_world.inverted() @ obj.matrix_world @ point


def bounds_in_root(root, mesh):
    points = [root_local_point(root, mesh, vertex.co) for vertex in mesh.data.vertices]
    minimum = Vector(tuple(min(point[index] for point in points) for index in range(3)))
    maximum = Vector(tuple(max(point[index] for point in points) for index in range(3)))
    return minimum, maximum


def nearest_surface_distance(root, mesh, marker):
    marker_point = root.matrix_world.inverted() @ marker.matrix_world.translation
    return min((root_local_point(root, mesh, vertex.co) - marker_point).length for vertex in mesh.data.vertices)


def stable_orient(camera, target):
    direction = (target - camera.location).normalized()
    world_up = Vector((0.0, 1.0, 0.0))
    if abs(direction.dot(world_up)) > 0.995:
        world_up = Vector((0.0, 0.0, 1.0))
    right = direction.cross(world_up).normalized()
    up = right.cross(direction).normalized()
    camera.rotation_euler = Matrix((right, up, -direction)).transposed().to_euler()


def snapshot_materials(mesh):
    records = []
    for slot in mesh.material_slots:
        material = slot.material
        images = []
        if material and material.use_nodes:
            for node in material.node_tree.nodes:
                if node.bl_idname == "ShaderNodeTexImage" and node.image:
                    images.append(
                        {
                            "node": node.name,
                            "image": node.image.name,
                            "filepath": bpy.path.abspath(node.image.filepath),
                            "colorspace": node.image.colorspace_settings.name,
                        }
                    )
        records.append(
            {
                "slot": slot.name,
                "material": material.name if material else None,
                "use_nodes": bool(material and material.use_nodes),
                "images": images,
            }
        )
    return records


def load_image(suffix, colorspace):
    path = TEXTURES / f"{ITEM_ID}_{suffix}.png"
    image = bpy.data.images.load(str(path), check_existing=False)
    image.name = f"Reimport_{suffix}"
    image.colorspace_settings.name = colorspace
    return image


def configure_qa_material(mesh):
    base = load_image("BaseColor", "sRGB")
    normal = load_image("Normal", "Non-Color")
    orm = load_image("ORM", "Non-Color")
    emission = load_image("Emission", "sRGB")
    material = bpy.data.materials.new("M_SmileSignal_ReimportQA")
    material.use_nodes = True
    material.use_backface_culling = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    output.location = (720, 0)
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.location = (440, 0)
    bsdf.inputs["Emission Strength"].default_value = 2.5
    links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])
    base_node = nodes.new("ShaderNodeTexImage")
    base_node.image = base
    base_node.location = (-620, 220)
    links.new(base_node.outputs["Color"], bsdf.inputs["Base Color"])
    orm_node = nodes.new("ShaderNodeTexImage")
    orm_node.image = orm
    orm_node.location = (-620, -40)
    separate = nodes.new("ShaderNodeSeparateColor")
    separate.mode = "RGB"
    separate.location = (-320, -40)
    links.new(orm_node.outputs["Color"], separate.inputs["Color"])
    links.new(separate.outputs["Green"], bsdf.inputs["Roughness"])
    links.new(separate.outputs["Blue"], bsdf.inputs["Metallic"])
    normal_node = nodes.new("ShaderNodeTexImage")
    normal_node.image = normal
    normal_node.location = (-620, -320)
    normal_map = nodes.new("ShaderNodeNormalMap")
    normal_map.location = (-300, -300)
    links.new(normal_node.outputs["Color"], normal_map.inputs["Color"])
    links.new(normal_map.outputs["Normal"], bsdf.inputs["Normal"])
    emission_node = nodes.new("ShaderNodeTexImage")
    emission_node.image = emission
    emission_node.location = (-20, -420)
    links.new(emission_node.outputs["Color"], bsdf.inputs["Emission Color"])
    mesh.data.materials.clear()
    mesh.data.materials.append(material)
    return material, emission


def setup_render(center, longest):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1024
    scene.render.resolution_y = 1024
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = True
    scene.view_settings.look = "AgX - Medium High Contrast"
    camera_data = bpy.data.cameras.new("ReimportQA_Camera")
    camera_data.type = "ORTHO"
    camera = bpy.data.objects.new("ReimportQA_Camera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    for name, offset, energy, size in (
        ("Reimport_Key", (1.8, -1.8, 2.2), 1150.0, 2.5),
        ("Reimport_Fill", (-1.5, -1.0, 0.9), 650.0, 2.3),
        ("Reimport_Rim", (0.3, 2.0, 1.5), 850.0, 2.0),
        ("Reimport_Top", (0.0, 0.2, 2.7), 420.0, 1.7),
    ):
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.size = size * longest
        light = bpy.data.objects.new(name, data)
        scene.collection.objects.link(light)
        light.location = center + Vector(offset) * longest
        stable_orient(light, center)
    world = bpy.data.worlds.new("ReimportQA_World")
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (0.025, 0.03, 0.045, 1.0)
    background.inputs["Strength"].default_value = 0.25
    scene.world = world
    return scene, camera


def render_eight(scene, camera, center, longest, prefix):
    distance = longest * 3.2
    views = {
        "front": Vector((0.0, 0.0, distance)),
        "back": Vector((0.0, 0.0, -distance)),
        "left": Vector((-distance, 0.0, 0.0)),
        "right": Vector((distance, 0.0, 0.0)),
        "top": Vector((0.0, distance, 0.0)),
        "bottom": Vector((0.0, -distance, 0.0)),
        "iso_left": Vector((-distance, distance * 0.72, distance)),
        "iso_right": Vector((distance, distance * 0.72, distance)),
    }
    outputs = {}
    for name, offset in views.items():
        camera.location = center + offset
        stable_orient(camera, center)
        camera.data.ortho_scale = longest * (0.82 if name in {"front", "back"} else 1.10)
        output = QA / f"{prefix}_{name}.png"
        scene.render.filepath = str(output)
        bpy.ops.render.render(write_still=True)
        outputs[name] = str(output.relative_to(ROOT))
    return outputs


def main():
    validation = json.loads(VALIDATION.read_text(encoding="utf-8"))
    expected_triangles = validation["decimation"]["selected_triangles"]
    QA.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(FBX), use_custom_normals=True)
    imported_objects = list(bpy.context.scene.objects)
    roots = [obj for obj in imported_objects if obj.parent is None]
    root = bpy.data.objects.get(ITEM_ID)
    meshes = [obj for obj in imported_objects if obj.type == "MESH"]
    mesh = meshes[0] if len(meshes) == 1 else None
    markers = {name: bpy.data.objects.get(name) for name in ("RightHandGrip", "LeftHandGrip", "Muzzle")}
    errors = []
    if len(roots) != 1:
        errors.append(f"expected one root, got {len(roots)}")
    if root is None:
        errors.append("missing item root")
    if mesh is None:
        errors.append(f"expected one mesh, got {len(meshes)}")
    if any(marker is None for marker in markers.values()):
        errors.append("missing marker empty")
    triangles = triangle_count(mesh) if mesh else None
    if triangles != expected_triangles:
        errors.append(f"triangle mismatch: expected {expected_triangles}, got {triangles}")

    raw_materials = snapshot_materials(mesh) if mesh else []
    marker_records = {}
    contacts = {}
    if root and mesh:
        for name, marker in markers.items():
            if marker is None:
                continue
            local = root.matrix_world.inverted() @ marker.matrix_world
            marker_records[name] = {
                "parent": marker.parent.name if marker.parent else None,
                "local_location": v3(local.translation),
                "local_scale": v3(local.to_scale()),
                "local_plus_z": v3(local.to_3x3().normalized() @ Vector((0.0, 0.0, 1.0))),
            }
            contacts[name] = round(nearest_surface_distance(root, mesh, marker), 6)
            if marker.parent != root:
                errors.append(f"{name} not direct root child")

    right = marker_records.get("RightHandGrip", {}).get("local_location")
    left = marker_records.get("LeftHandGrip", {}).get("local_location")
    muzzle = marker_records.get("Muzzle", {})
    if right != [0.0, 0.0, 0.0]:
        errors.append(f"RightHandGrip mismatch: {right}")
    if not left or abs(left[2] - 0.27) > 1e-4:
        errors.append(f"LeftHandGrip +Z mismatch: {left}")
    if muzzle.get("local_plus_z") != [0.0, 0.0, 1.0]:
        errors.append(f"Muzzle +Z mismatch: {muzzle.get('local_plus_z')}")

    prohibited = [
        {"name": obj.name, "type": obj.type}
        for obj in imported_objects
        if obj.type in {"CAMERA", "LIGHT", "ARMATURE"} or "collider" in obj.name.lower()
    ]
    if prohibited:
        errors.append(f"prohibited FBX objects: {prohibited}")
    non_uniform = []
    for obj in imported_objects:
        scale = obj.scale
        if min(scale) <= 0.0 or max(scale) - min(scale) > 1e-5:
            non_uniform.append({"name": obj.name, "scale": v3(scale)})
    if non_uniform:
        errors.append(f"non-positive/non-uniform transforms: {non_uniform}")

    bounds_record = None
    pbr_views = emission_views = cull_views = {}
    if root and mesh:
        minimum, maximum = bounds_in_root(root, mesh)
        bounds_record = {"min": v3(minimum), "max": v3(maximum), "dimensions": v3(maximum - minimum)}
        muzzle_location = muzzle.get("local_location")
        if not muzzle_location or abs(muzzle_location[2] - maximum.z) > 1e-3:
            errors.append(f"Muzzle is not at +Z bound: marker={muzzle_location}, maxZ={maximum.z}")

        material, emission_image = configure_qa_material(mesh)
        center = (minimum + maximum) * 0.5
        longest = max(maximum - minimum)
        scene, camera = setup_render(center, longest)
        pbr_views = render_eight(scene, camera, center, longest, "pbr")

        emission_only = bpy.data.materials.new("Reimport_EmissionOnly")
        emission_only.use_nodes = True
        emission_bsdf = emission_only.node_tree.nodes.get("Principled BSDF")
        emission_bsdf.inputs["Base Color"].default_value = (0.005, 0.005, 0.005, 1.0)
        emission_bsdf.inputs["Roughness"].default_value = 0.7
        emission_bsdf.inputs["Emission Strength"].default_value = 5.0
        emission_node = emission_only.node_tree.nodes.new("ShaderNodeTexImage")
        emission_node.image = emission_image
        emission_only.node_tree.links.new(emission_node.outputs["Color"], emission_bsdf.inputs["Emission Color"])
        mesh.data.materials[0] = emission_only
        emission_views = render_eight(scene, camera, center, longest, "emission")

        neutral = bpy.data.materials.new("Reimport_BackfaceCull")
        neutral.use_nodes = True
        neutral.use_backface_culling = True
        neutral_bsdf = neutral.node_tree.nodes.get("Principled BSDF")
        neutral_bsdf.inputs["Base Color"].default_value = (0.18, 0.48, 0.78, 1.0)
        neutral_bsdf.inputs["Metallic"].default_value = 0.05
        neutral_bsdf.inputs["Roughness"].default_value = 0.48
        mesh.data.materials[0] = neutral
        cull_views = render_eight(scene, camera, center, longest, "cull")

    external = {}
    for suffix in ("BaseColor", "Normal", "ORM", "Occlusion", "MetallicSmoothness", "Emission"):
        path = TEXTURES / f"{ITEM_ID}_{suffix}.png"
        external[suffix] = {"path": str(path.relative_to(ROOT)), "exists": path.is_file()}
    if not all(record["exists"] for record in external.values()):
        errors.append("external PBR texture set incomplete")
    if not raw_materials or any(record["material"] is None for record in raw_materials):
        errors.append("FBX material slot missing")

    report = {
        "verified_at": datetime.now(timezone.utc).isoformat(),
        "blender_version": bpy.app.version_string,
        "method": "factory-empty Blender scene -> FBX import -> hierarchy/axis/transform/triangle/material audit -> external PBR/emission/culling 8-view renders",
        "fbx": str(FBX.relative_to(ROOT)),
        "top_level_roots": [obj.name for obj in roots],
        "root": root.name if root else None,
        "direct_children": sorted(child.name for child in root.children) if root else [],
        "object_count": len(imported_objects),
        "mesh_count": len(meshes),
        "triangles": triangles,
        "markers": marker_records,
        "marker_nearest_surface_distance_m": contacts,
        "bounds_root_local": bounds_record,
        "fbx_imported_materials_before_qa_override": raw_materials,
        "external_pbr_textures": external,
        "prohibited_objects": prohibited,
        "non_positive_or_non_uniform": non_uniform,
        "qa": {"pbr_8_view": pbr_views, "emission_8_view": emission_views, "culling_8_view": cull_views},
        "errors": errors,
        "pass": not errors,
        "visual_gate": "pending manual inspection",
    }
    REPORT.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    validation["reimport"] = {
        "status": "passed" if report["pass"] else "failed",
        "report": str(REPORT.relative_to(ROOT)),
        "triangles": triangles,
        "root": report["root"],
        "markers": marker_records,
        "marker_nearest_surface_distance_m": contacts,
        "errors": errors,
        "visual_gate": "pending manual inspection",
    }
    validation["checks"]["reimport_verified"] = report["pass"]
    validation["pass"] = all(validation["checks"].values())
    VALIDATION.write_text(json.dumps(validation, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"report": str(REPORT), "pass": report["pass"], "errors": errors, "markers": marker_records, "contacts": contacts}, ensure_ascii=False, indent=2))
    if errors:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
