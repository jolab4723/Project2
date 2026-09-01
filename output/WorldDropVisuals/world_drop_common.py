from __future__ import annotations

import json
import math
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parent
PROJECT_ROOT = ROOT.parent.parent
GENERATED_DIR = ROOT / "generated"
UNITY_MODEL_DIR = PROJECT_ROOT / "Assets" / "SW" / "Models" / "WorldDropVisuals"


def reset_scene() -> None:
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)

    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.engine = "BLENDER_EEVEE"
    scene.view_settings.look = "AgX - Medium High Contrast"


def make_material(name: str, color: tuple[float, float, float, float]) -> bpy.types.Material:
    material = bpy.data.materials.new(name=name)
    material.use_nodes = True
    principled = material.node_tree.nodes.get("Principled BSDF")
    principled.inputs["Base Color"].default_value = color
    principled.inputs["Metallic"].default_value = 0.42
    principled.inputs["Roughness"].default_value = 0.48
    return material


def _finish_part(
    obj: bpy.types.Object,
    name: str,
    material: bpy.types.Material,
    bevel: float,
    bevel_segments: int = 2,
) -> bpy.types.Object:
    obj.name = name
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)

    if bevel > 0.0:
        modifier = obj.modifiers.new(name="ProductionBevel", type="BEVEL")
        modifier.width = bevel
        modifier.segments = bevel_segments
        modifier.limit_method = "ANGLE"
        modifier.angle_limit = math.radians(24.0)
        modifier.harden_normals = True
        bpy.ops.object.modifier_apply(modifier=modifier.name)

    obj.data.materials.clear()
    obj.data.materials.append(material)
    obj.select_set(False)
    return obj


def box(
    name: str,
    size: tuple[float, float, float],
    location: tuple[float, float, float],
    material: bpy.types.Material,
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
    bevel: float = 0.012,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(location=location, rotation=rotation)
    obj = bpy.context.object
    obj.dimensions = size
    return _finish_part(obj, name, material, bevel)


def cylinder(
    name: str,
    radius: float,
    depth: float,
    location: tuple[float, float, float],
    material: bpy.types.Material,
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
    vertices: int = 20,
    bevel: float = 0.008,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=radius,
        depth=depth,
        end_fill_type="NGON",
        location=location,
        rotation=rotation,
    )
    return _finish_part(bpy.context.object, name, material, bevel)


def cone(
    name: str,
    radius1: float,
    radius2: float,
    depth: float,
    location: tuple[float, float, float],
    material: bpy.types.Material,
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
    vertices: int = 20,
    bevel: float = 0.006,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cone_add(
        vertices=vertices,
        radius1=radius1,
        radius2=radius2,
        depth=depth,
        end_fill_type="NGON",
        location=location,
        rotation=rotation,
    )
    return _finish_part(bpy.context.object, name, material, bevel)


def sphere(
    name: str,
    radius: float,
    scale: tuple[float, float, float],
    location: tuple[float, float, float],
    material: bpy.types.Material,
    segments: int = 20,
    rings: int = 10,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=segments,
        ring_count=rings,
        radius=radius,
        location=location,
    )
    obj = bpy.context.object
    obj.scale = scale
    return _finish_part(obj, name, material, 0.0)


def torus(
    name: str,
    major_radius: float,
    minor_radius: float,
    location: tuple[float, float, float],
    material: bpy.types.Material,
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
    major_segments: int = 20,
    minor_segments: int = 6,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_torus_add(
        align="WORLD",
        major_segments=major_segments,
        minor_segments=minor_segments,
        location=location,
        rotation=rotation,
        major_radius=major_radius,
        minor_radius=minor_radius,
    )
    return _finish_part(bpy.context.object, name, material, 0.0)


def custom_mesh(
    name: str,
    vertices: list[tuple[float, float, float]],
    faces: list[tuple[int, ...]],
    material: bpy.types.Material,
    location: tuple[float, float, float] = (0.0, 0.0, 0.0),
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
    bevel: float = 0.01,
) -> bpy.types.Object:
    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    obj.rotation_euler = rotation
    return _finish_part(obj, name, material, bevel)


def join_and_finalize(
    asset_name: str,
    parts: list[bpy.types.Object],
    material: bpy.types.Material,
) -> bpy.types.Object:
    if not parts:
        raise RuntimeError(f"{asset_name} has no mesh parts")

    bpy.ops.object.select_all(action="DESELECT")
    for part in parts:
        if part is None or part.type != "MESH":
            raise RuntimeError(f"{asset_name} contains a non-mesh part")
        part.select_set(True)

    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = asset_name
    obj.data.name = f"{asset_name}_Mesh"

    obj.data.materials.clear()
    obj.data.materials.append(material)

    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    bpy.ops.object.shade_smooth_by_angle()

    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66.0), island_margin=0.02)
    bpy.ops.object.mode_set(mode="OBJECT")

    world_corners = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    minimum = Vector((min(v.x for v in world_corners), min(v.y for v in world_corners), min(v.z for v in world_corners)))
    maximum = Vector((max(v.x for v in world_corners), max(v.y for v in world_corners), max(v.z for v in world_corners)))
    center = (minimum + maximum) * 0.5
    for vertex in obj.data.vertices:
        vertex.co -= center
    obj.location = (0.0, 0.0, 0.0)
    bpy.context.view_layer.update()

    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if tuple(round(value, 6) for value in obj.scale) != (1.0, 1.0, 1.0):
        raise RuntimeError(f"{asset_name} scale is not applied")
    return obj


def evaluated_triangles(obj: bpy.types.Object) -> int:
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = obj.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh()
    try:
        mesh.calc_loop_triangles()
        return len(mesh.loop_triangles)
    finally:
        evaluated.to_mesh_clear()


def object_metrics(obj: bpy.types.Object) -> dict[str, object]:
    dimensions = [round(float(value), 6) for value in obj.dimensions]
    triangles = evaluated_triangles(obj)
    horizontal_radius = 0.5 * math.sqrt(dimensions[0] ** 2 + dimensions[1] ** 2)
    return {
        "name": obj.name,
        "mesh_objects": 1,
        "triangles": triangles,
        "material_slots": len(obj.data.materials),
        "dimensions_m": dimensions,
        "maximum_extent_m": round(max(dimensions), 6),
        "horizontal_radius_m": round(horizontal_radius, 6),
        "location": [round(float(value), 6) for value in obj.location],
        "rotation_euler": [round(float(value), 6) for value in obj.rotation_euler],
        "scale": [round(float(value), 6) for value in obj.scale],
        "uv_layers": len(obj.data.uv_layers),
        "blender_version": bpy.app.version_string,
    }


def validate_contract(metrics: dict[str, object]) -> None:
    triangles = int(metrics["triangles"])
    if triangles < 500 or triangles > 2000:
        raise RuntimeError(f"{metrics['name']} triangle gate failed: {triangles}")
    if int(metrics["mesh_objects"]) != 1 or int(metrics["material_slots"]) != 1:
        raise RuntimeError(f"{metrics['name']} mesh/material gate failed")
    if float(metrics["maximum_extent_m"]) > 0.9:
        raise RuntimeError(f"{metrics['name']} extent gate failed: {metrics['maximum_extent_m']}")
    if float(metrics["horizontal_radius_m"]) > 0.5:
        raise RuntimeError(f"{metrics['name']} horizontal radius gate failed")
    if metrics["scale"] != [1.0, 1.0, 1.0]:
        raise RuntimeError(f"{metrics['name']} transform gate failed")
    if int(metrics["uv_layers"]) < 1:
        raise RuntimeError(f"{metrics['name']} has no UV layer")


def export_asset(obj: bpy.types.Object, asset_name: str) -> dict[str, object]:
    GENERATED_DIR.mkdir(parents=True, exist_ok=True)
    UNITY_MODEL_DIR.mkdir(parents=True, exist_ok=True)

    blend_path = GENERATED_DIR / f"{asset_name}.blend"
    glb_path = GENERATED_DIR / f"{asset_name}.glb"
    fbx_path = UNITY_MODEL_DIR / f"{asset_name}.fbx"

    bpy.context.scene["world_drop_asset"] = asset_name
    bpy.context.scene["generator"] = "generate_world_drop_visuals.py"
    blend_backup_path = blend_path.with_suffix(".blend1")
    if blend_backup_path.exists():
        blend_backup_path.unlink()
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), check_existing=False)

    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.gltf(
        filepath=str(glb_path),
        export_format="GLB",
        use_selection=True,
        export_apply=True,
        export_yup=True,
        export_materials="EXPORT",
        export_cameras=False,
        export_lights=False,
        export_animations=False,
    )
    bpy.ops.export_scene.fbx(
        filepath=str(fbx_path),
        use_selection=True,
        object_types={"MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z",
        axis_up="Y",
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="AUTO",
        embed_textures=False,
    )

    metrics = object_metrics(obj)
    metrics.update({
        "blend": blend_path.relative_to(PROJECT_ROOT).as_posix(),
        "glb": glb_path.relative_to(PROJECT_ROOT).as_posix(),
        "fbx": fbx_path.relative_to(PROJECT_ROOT).as_posix(),
    })
    return metrics


def write_json(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2), encoding="utf-8")
