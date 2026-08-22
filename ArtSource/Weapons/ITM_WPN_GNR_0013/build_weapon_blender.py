"""Build the production Blender/FBX asset for ITM_WPN_GNR_0013.

This script intentionally owns only the asset under ArtSource/Weapons/ITM_WPN_GNR_0013.
It imports the downloaded P1 GLB, fixes its coordinate system, removes the opaque
P1 chamber contents, and adds an explicit glass chamber and a solid rabbit whose
face points down the final weapon +Z/muzzle axis.

Run with Blender 5.1, for example:
    blender.exe --background --python build_weapon_blender.py
"""

from __future__ import annotations

import json
import math
import sys
from datetime import datetime, timezone
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector


ROOT = Path(__file__).resolve().parent
RAW_GLB = ROOT / "Tripo" / "Downloaded" / "ITM_WPN_GNR_0013_P1_raw.glb"
PRODUCTION = ROOT / "Production"
TEXTURES = PRODUCTION / "Textures"
QA = PRODUCTION / "QA"
BLEND_PATH = PRODUCTION / "ITM_WPN_GNR_0013.blend"
FBX_PATH = PRODUCTION / "ITM_WPN_GNR_0013.fbx"
VALIDATION_PATH = QA / "validation.json"

# P1 raw dimensions are approximately 1.0226 (raw X) x .4629 x .4355 m.
# 1.18 preserves the reference proportions while producing a 1.207 m long gun.
MODEL_SCALE = 1.18

# Raw P1 maps X=forward, Z=up, Y=width. In the final gun:
#   X_final = Y_raw, Y_final = Z_raw, Z_final = X_raw
# This is a proper rotation (determinant +1), not a mirror.

# The trigger-hand centre is the final-world point used as the root origin.
# It is on the centre line of the rear pistol grip, below the receiver.
GRIP_WORLD = Vector((0.0, -0.125, -0.335))

# Region occupied by the old opaque P1 chamber/rabbit, expressed in final
# coordinates before the grip-origin translation. The surrounding frames remain.
CHAMBER_CLEAR_MIN = Vector((-0.205, -0.155, -0.040))
CHAMBER_CLEAR_MAX = Vector((0.205, 0.155, 0.425))

CHAMBER_CENTER_WORLD = Vector((0.0, 0.0, 0.185))
CHAMBER_SIZE = Vector((0.285, 0.365, 0.455))
CHAMBER_INNER_WALL = Vector((0.032, 0.032, 0.045))
RABBIT_SCALE = 0.52
# Keep the mascot low enough that the ear tips and feet both retain a real
# air gap from the inner glass surface at the current 0.52 scale.
RABBIT_CENTER_WORLD = Vector((0.0, -0.040, 0.160))


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def v3(value: Vector | tuple[float, float, float]) -> list[float]:
    return [round(float(value[i]), 6) for i in range(3)]


def local_point(world_point: Vector) -> Vector:
    """Convert a final-world point into root-local coordinates."""
    return Vector(world_point) - GRIP_WORLD


def raw_to_final(raw: Vector) -> Vector:
    """Apply the P1 raw-to-final proper rotation and scale."""
    return Vector((raw.y, raw.z, raw.x)) * MODEL_SCALE


def ensure_dirs() -> None:
    for path in (PRODUCTION, TEXTURES, QA):
        path.mkdir(parents=True, exist_ok=True)


def clear_scene() -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)


def import_raw() -> bpy.types.Object:
    if not RAW_GLB.is_file():
        raise FileNotFoundError(RAW_GLB)
    bpy.ops.import_scene.gltf(filepath=str(RAW_GLB))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if not meshes:
        raise RuntimeError("P1 GLB did not contain a mesh")
    if len(meshes) > 1:
        bpy.ops.object.select_all(action="DESELECT")
        for obj in meshes:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = meshes[0]
        bpy.ops.object.join()
        meshes = [meshes[0]]
    raw = meshes[0]
    raw.name = "WeaponBody_P1"
    raw.data.name = "WeaponBody_P1_Mesh"
    return raw


def transform_raw_mesh(raw: bpy.types.Object) -> dict:
    """Rotate/scale raw vertices, then remove the old chamber interior."""
    mesh = raw.data
    raw_bounds_min = Vector((float("inf"),) * 3)
    raw_bounds_max = Vector((float("-inf"),) * 3)
    for vertex in mesh.vertices:
        point = raw_to_final(vertex.co)
        raw_bounds_min = Vector((min(raw_bounds_min[i], point[i]) for i in range(3)))
        raw_bounds_max = Vector((max(raw_bounds_max[i], point[i]) for i in range(3)))
        vertex.co = point
    mesh.update()

    # Face-centre removal avoids leaving the original opaque shell and rabbit
    # behind the new glass. Boundary hardware remains outside this box.
    bm = bmesh.new()
    bm.from_mesh(mesh)
    removed_faces = 0
    for face in list(bm.faces):
        center = face.calc_center_median()
        if all(CHAMBER_CLEAR_MIN[i] <= center[i] <= CHAMBER_CLEAR_MAX[i] for i in range(3)):
            bm.faces.remove(face)
            removed_faces += 1
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()

    # Shift the body to the trigger-hand root origin and apply all transforms.
    for vertex in mesh.vertices:
        vertex.co -= GRIP_WORLD
    raw.location = (0.0, 0.0, 0.0)
    raw.rotation_euler = (0.0, 0.0, 0.0)
    raw.scale = (1.0, 1.0, 1.0)
    raw.data.update()

    return {
        "source_raw_bounds_final_untranslated": {
            "min": v3(raw_bounds_min),
            "max": v3(raw_bounds_max),
            "dimensions": v3(raw_bounds_max - raw_bounds_min),
        },
        "chamber_clear_box_final_untranslated": {
            "min": v3(CHAMBER_CLEAR_MIN),
            "max": v3(CHAMBER_CLEAR_MAX),
        },
        "removed_opaque_faces": removed_faces,
    }


def extract_images() -> list[dict]:
    """Write P1's embedded Color/Normal/ORM images as external PNGs."""
    records: list[dict] = []
    role_names = {
        "color": "ITM_WPN_GNR_0013_Color.png",
        "normal": "ITM_WPN_GNR_0013_Normal.png",
        "orm": "ITM_WPN_GNR_0013_ORM.png",
    }
    used_roles: set[str] = set()
    for image in list(bpy.data.images):
        lower = image.name.lower()
        role = next((key for key in role_names if key in lower), None)
        if role is None or role in used_roles:
            continue
        destination = TEXTURES / role_names[role]
        try:
            image.filepath_raw = str(destination)
            image.file_format = "PNG"
            image.save()
        except Exception:
            # Packed GLB images can require save_render on some Blender builds.
            image.save_render(str(destination))
        if role in {"normal", "orm"}:
            image.colorspace_settings.name = "Non-Color"
        else:
            image.colorspace_settings.name = "sRGB"
        used_roles.add(role)
        records.append({
            "role": role,
            "source_name": image.name,
            "path": str(destination.relative_to(ROOT)),
            "size": list(image.size),
        })
    return records


def ensure_principled_material(
    name: str,
    base_color: tuple[float, float, float, float],
    metallic: float,
    roughness: float,
    *,
    transmission: float = 0.0,
    alpha: float = 1.0,
    ior: float = 1.45,
) -> bpy.types.Material:
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    output = next((node for node in nodes if node.bl_idname == "ShaderNodeOutputMaterial"), None)
    if output is None:
        output = nodes.new("ShaderNodeOutputMaterial")
    principled = next((node for node in nodes if node.bl_idname == "ShaderNodeBsdfPrincipled"), None)
    if principled is None:
        principled = nodes.new("ShaderNodeBsdfPrincipled")
    for link in list(links):
        if link.to_node == output and link.to_socket == output.inputs.get("Surface"):
            links.remove(link)
    links.new(principled.outputs["BSDF"], output.inputs["Surface"])
    for key, value in (
        ("Base Color", base_color),
        ("Metallic", metallic),
        ("Roughness", roughness),
        ("Alpha", alpha),
        ("IOR", ior),
    ):
        if principled.inputs.get(key) is not None:
            principled.inputs[key].default_value = value
    transmission_socket = principled.inputs.get("Transmission Weight") or principled.inputs.get("Transmission")
    if transmission_socket is not None:
        transmission_socket.default_value = transmission
    material.diffuse_color = base_color
    # Blender 4/5 replaced blend_method with surface_render_method. Dithered
    # transparency is stable in Eevee while alpha=1 materials remain opaque.
    if hasattr(material, "surface_render_method"):
        material.surface_render_method = "DITHERED"
    elif hasattr(material, "blend_method"):
        material.blend_method = "BLEND" if alpha < 1.0 else "OPAQUE"
    return material


def configure_weapon_material(raw: bpy.types.Object) -> bpy.types.Material:
    if raw.data.materials:
        material = raw.data.materials[0]
    else:
        material = bpy.data.materials.new("M_Weapon_P1_Base")
        raw.data.materials.append(material)
    material.name = "M_Weapon_P1_Base"
    material.use_nodes = True
    # Keep the P1 texture/normal/ORM links, while making the exported material
    # name deterministic for Unity's SourceAssetIdentifier remap.
    principled = next(
        (node for node in material.node_tree.nodes if node.bl_idname == "ShaderNodeBsdfPrincipled"),
        None,
    )
    if principled:
        if principled.inputs.get("Metallic") and not principled.inputs["Metallic"].is_linked:
            principled.inputs["Metallic"].default_value = 0.32
        if principled.inputs.get("Roughness") and not principled.inputs["Roughness"].is_linked:
            principled.inputs["Roughness"].default_value = 0.30
        if principled.inputs.get("Alpha"):
            principled.inputs["Alpha"].default_value = 1.0
    material.diffuse_color = (0.82, 0.88, 0.95, 1.0)
    return material


def add_mesh_object(name: str, material: bpy.types.Material) -> bpy.types.Object:
    obj = bpy.context.object
    obj.name = name
    obj.data.name = f"{name}_Mesh"
    obj.data.materials.clear()
    obj.data.materials.append(material)
    return obj


def add_uv_sphere(name: str, location: Vector, scale: Vector, material: bpy.types.Material, segments: int = 32, rings: int = 20) -> bpy.types.Object:
    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=segments,
        ring_count=rings,
        location=local_point(location),
    )
    obj = add_mesh_object(name, material)
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return obj


def add_cylinder(name: str, location: Vector, radius: float, depth: float, material: bpy.types.Material, *, axis: str = "Y", vertices: int = 32) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=local_point(location))
    obj = add_mesh_object(name, material)
    if axis == "X":
        obj.rotation_euler[1] = math.radians(90.0)
    elif axis == "Z":
        obj.rotation_euler[0] = math.radians(90.0)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return obj


def add_beveled_cube(name: str, center: Vector, size: Vector, material: bpy.types.Material, bevel: float) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(location=local_point(center))
    obj = add_mesh_object(name, material)
    obj.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    modifier = obj.modifiers.new("Rounded edges", "BEVEL")
    modifier.width = bevel
    modifier.segments = 5
    modifier.limit_method = "ANGLE"
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    return obj


def join_objects(objects: list[bpy.types.Object], name: str) -> bpy.types.Object:
    objects = [obj for obj in objects if obj and obj.name in bpy.data.objects]
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    result = objects[0]
    result.name = name
    result.data.name = f"{name}_Mesh"
    return result


def create_rabbit() -> list[bpy.types.Object]:
    white = ensure_principled_material("M_RabbitWhite", (0.92, 0.95, 1.0, 1.0), 0.0, 0.70)
    eyes = ensure_principled_material("M_RabbitEyes", (0.015, 0.02, 0.025, 1.0), 0.0, 0.24)
    nose = ensure_principled_material("M_RabbitNose", (1.0, 0.32, 0.42, 1.0), 0.0, 0.45)
    cheeks = ensure_principled_material("M_RabbitCheeks", (1.0, 0.42, 0.52, 1.0), 0.0, 0.58)

    # Rabbit is intentionally modelled in final coordinates. Its face details
    # exist only on the +Z side, so +Z muzzle = frontal face, ±X = one-eye
    # profiles, and -Z = clean back of head.
    def rabbit_point(point: Vector) -> Vector:
        return RABBIT_CENTER_WORLD + (Vector(point) - RABBIT_CENTER_WORLD) * RABBIT_SCALE

    def rabbit_size(size: Vector) -> Vector:
        return Vector(size) * RABBIT_SCALE

    white_parts = [
        add_uv_sphere("RabbitHeadPart", rabbit_point(Vector((0.0, 0.055, 0.165))), rabbit_size(Vector((0.145, 0.125, 0.120))), white),
        add_uv_sphere("RabbitBodyPart", rabbit_point(Vector((0.0, -0.060, 0.090))), rabbit_size(Vector((0.125, 0.125, 0.120))), white),
        add_uv_sphere("RabbitEarL", rabbit_point(Vector((-0.060, 0.195, 0.150))), rabbit_size(Vector((0.045, 0.105, 0.038))), white),
        add_uv_sphere("RabbitEarR", rabbit_point(Vector((0.060, 0.195, 0.150))), rabbit_size(Vector((0.045, 0.105, 0.038))), white),
        add_uv_sphere("RabbitArmL", rabbit_point(Vector((-0.135, -0.050, 0.115))), rabbit_size(Vector((0.040, 0.070, 0.050))), white),
        add_uv_sphere("RabbitArmR", rabbit_point(Vector((0.135, -0.050, 0.115))), rabbit_size(Vector((0.040, 0.070, 0.050))), white),
        add_uv_sphere("RabbitFootL", rabbit_point(Vector((-0.075, -0.165, 0.105))), rabbit_size(Vector((0.065, 0.045, 0.050))), white),
        add_uv_sphere("RabbitFootR", rabbit_point(Vector((0.075, -0.165, 0.105))), rabbit_size(Vector((0.065, 0.045, 0.050))), white),
        add_uv_sphere("RabbitTail", rabbit_point(Vector((0.0, -0.055, -0.035))), rabbit_size(Vector((0.055, 0.055, 0.045))), white),
    ]
    rabbit_solid = join_objects(white_parts, "Rabbit_Solid")

    # Small facial elements sit in a shallow +Z layer. They are not mirrored
    # onto the back or the sides, which is the key orientation guarantee.
    eye_l = add_uv_sphere("RabbitEye_L", rabbit_point(Vector((-0.052, 0.065, 0.280))), rabbit_size(Vector((0.019, 0.024, 0.012))), eyes, segments=20, rings=12)
    eye_r = add_uv_sphere("RabbitEye_R", rabbit_point(Vector((0.052, 0.065, 0.280))), rabbit_size(Vector((0.019, 0.024, 0.012))), eyes, segments=20, rings=12)
    eye_pair = join_objects([eye_l, eye_r], "Rabbit_Eyes")
    rabbit_nose = add_uv_sphere("RabbitNose", rabbit_point(Vector((0.0, 0.012, 0.292))), rabbit_size(Vector((0.023, 0.018, 0.014))), nose, segments=20, rings=12)
    cheek_l = add_uv_sphere("RabbitCheek_L", rabbit_point(Vector((-0.082, 0.010, 0.282))), rabbit_size(Vector((0.030, 0.018, 0.010))), cheeks, segments=20, rings=12)
    cheek_r = add_uv_sphere("RabbitCheek_R", rabbit_point(Vector((0.082, 0.010, 0.282))), rabbit_size(Vector((0.030, 0.018, 0.010))), cheeks, segments=20, rings=12)
    cheek_pair = join_objects([cheek_l, cheek_r], "Rabbit_Cheeks")
    return [rabbit_solid, eye_pair, rabbit_nose, cheek_pair]


def create_chamber() -> list[bpy.types.Object]:
    glass = ensure_principled_material(
        "M_IceGlass",
        (0.34, 0.78, 1.0, 0.32),
        0.05,
        0.12,
        transmission=0.82,
        alpha=0.32,
        ior=1.333,
    )
    weapon = bpy.data.materials.get("M_Weapon_P1_Base") or ensure_principled_material(
        "M_Weapon_P1_Base", (0.82, 0.88, 0.95, 1.0), 0.32, 0.30
    )
    glass_obj = add_beveled_cube("CoolingChamber_Glass", CHAMBER_CENTER_WORLD, CHAMBER_SIZE, glass, 0.055)

    # Thin metal/ice-blue rims make the transparent volume readable in Lit QA
    # while retaining the original silhouette around the chamber.
    rim_radius = min(CHAMBER_SIZE.x, CHAMBER_SIZE.y) * 0.50
    rim_objects: list[bpy.types.Object] = []
    for suffix, z in (("Rear", CHAMBER_CENTER_WORLD.z - CHAMBER_SIZE.z * 0.49), ("Front", CHAMBER_CENTER_WORLD.z + CHAMBER_SIZE.z * 0.49)):
        bpy.ops.mesh.primitive_torus_add(
            major_radius=rim_radius,
            minor_radius=0.018,
            major_segments=48,
            minor_segments=12,
            location=local_point(Vector((CHAMBER_CENTER_WORLD.x, CHAMBER_CENTER_WORLD.y, z))),
            rotation=(0.0, 0.0, 0.0),
        )
        rim = add_mesh_object(f"CoolingChamber_Frame_{suffix}", weapon)
        rim_objects.append(rim)
    return [glass_obj, *rim_objects]


def create_root(objects: list[bpy.types.Object]) -> bpy.types.Object:
    root_data = bpy.data.objects.new("ITM_WPN_GNR_0013_root", None)
    root_data.empty_display_type = "PLAIN_AXES"
    root_data.empty_display_size = 0.10
    bpy.context.scene.collection.objects.link(root_data)
    for obj in objects:
        if obj and obj != root_data:
            obj.parent = root_data
            obj.matrix_parent_inverse = root_data.matrix_world.inverted()

    # Root-direct marker empties. RightHandGrip is the root origin by contract.
    marker_specs = {
        "RightHandGrip": Vector((0.0, 0.0, 0.0)),
        "LeftHandGrip": local_point(Vector((0.0, -0.235, 0.145))),
        "Muzzle": local_point(Vector((0.0, 0.015, 0.610))),
    }
    for name, location in marker_specs.items():
        marker = bpy.data.objects.new(name, None)
        # Parenting alone does not add a newly-created Empty to the active view
        # layer; link it explicitly so it is selectable/exportable.
        bpy.context.scene.collection.objects.link(marker)
        marker.empty_display_type = "ARROWS" if name == "Muzzle" else "CUBE"
        marker.empty_display_size = 0.055 if name == "Muzzle" else 0.045
        marker.location = location
        marker.rotation_euler = (0.0, 0.0, 0.0)
        marker.parent = root_data
        marker.matrix_parent_inverse = root_data.matrix_world.inverted()
        root_data["marker_contract"] = "Gunner: +Z muzzle, +Y up"
    return root_data


def apply_all_mesh_transforms(objects: list[bpy.types.Object]) -> None:
    for obj in objects:
        if obj.type != "MESH":
            continue
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
        obj.select_set(False)
        obj.data.update()


def bounds_and_counts(objects: list[bpy.types.Object]) -> tuple[Vector, Vector, int, int]:
    mesh_objects = [obj for obj in objects if obj.type == "MESH"]
    points = [obj.matrix_world @ Vector(corner) for obj in mesh_objects for corner in obj.bound_box]
    if not points:
        return Vector((0, 0, 0)), Vector((0, 0, 0)), 0, 0
    minimum = Vector((min(p[i] for p in points) for i in range(3)))
    maximum = Vector((max(p[i] for p in points) for i in range(3)))
    triangles = 0
    polygons = 0
    for obj in mesh_objects:
        obj.data.calc_loop_triangles()
        triangles += len(obj.data.loop_triangles)
        polygons += len(obj.data.polygons)
    return minimum, maximum, triangles, polygons


def orient_qa_camera(camera: bpy.types.Object, target: Vector) -> dict:
    """Orient a camera with deterministic screen-right/up basis.

    The returned values are also written into validation.json so a later QA
    reviewer can distinguish an actual model orientation issue from a rolled
    screenshot camera.
    """
    direction = (target - camera.location).normalized()
    screen_up = Vector((0.0, 1.0, 0.0))
    screen_right = direction.cross(screen_up).normalized()
    screen_up = screen_right.cross(direction).normalized()
    rotation = Matrix((screen_right, screen_up, -direction)).transposed()
    distance = (camera.location - target).length
    camera.matrix_world = rotation.to_4x4()
    camera.location = target - direction * distance
    return {
        "view_from": v3(-direction),
        "screen_right": v3(screen_right),
        "screen_up": v3(screen_up),
    }


def setup_qa_scene(root: bpy.types.Object, center: Vector, longest: float) -> tuple[list[bpy.types.Object], bpy.types.Scene]:
    scene = bpy.context.scene
    # Blender 5.1 Windows build exposes the Eevee identifier as BLENDER_EEVEE.
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1024
    scene.render.resolution_y = 1024
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.render.film_transparent = True
    scene.view_settings.look = "AgX - Medium High Contrast"

    center = Vector(center)
    camera_data = bpy.data.cameras.new("QA_Camera")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = longest * 1.30
    camera = bpy.data.objects.new("QA_Camera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera

    lights: list[bpy.types.Object] = [camera]
    for name, loc, energy, size in (
        ("QA_Key", (1.6, -1.8, 2.2), 420, 2.0),
        ("QA_Fill", (-1.4, -0.8, 1.0), 180, 1.8),
        ("QA_Rim", (0.2, 2.0, 1.5), 280, 1.6),
    ):
        data = bpy.data.lights.new(name=name, type="AREA")
        data.energy = energy
        data.shape = "DISK"
        data.size = size
        light = bpy.data.objects.new(name, data)
        scene.collection.objects.link(light)
        light.location = center + Vector(loc) * longest * 0.65
        light.rotation_euler = (center - light.location).to_track_quat("-Z", "Y").to_euler()
        lights.append(light)
    world = bpy.data.worlds.new("QA_World")
    world.color = (0.008, 0.012, 0.02)
    scene.world = world
    scene["qa_render_convention"] = "+Y screen-up; +Z front/muzzle rabbit face; -X left side profile; +X right side profile; -Z rear"
    scene["qa_look_at"] = v3(center)
    return lights, scene


def render_qa(lights: list[bpy.types.Object], scene: bpy.types.Scene, center: Vector, longest: float) -> dict:
    camera = scene.camera
    distance = longest * 3.2
    target = Vector(center)
    front_target = local_point(CHAMBER_CENTER_WORLD)
    views = {
        "front": Vector((0.0, 0.0, distance)),
        "left": Vector((-distance, 0.0, 0.0)),
        "right": Vector((distance, 0.0, 0.0)),
        "rear": Vector((0.0, 0.0, -distance)),
        "isometric": Vector((-distance * 0.85, -distance * 0.80, distance * 0.72)),
    }
    records = {}
    for name, offset in views.items():
        view_target = front_target if name == "front" else target
        camera.location = view_target + offset
        basis = orient_qa_camera(camera, view_target)
        if name in {"front", "rear"}:
            camera.data.ortho_scale = max(0.64, longest * 0.62)
        elif name in {"left", "right"}:
            camera.data.ortho_scale = longest * 1.22
        else:
            camera.data.ortho_scale = longest * 1.35
        output = QA / f"{name}.png"
        scene.render.filepath = str(output)
        bpy.ops.render.render(write_still=True)
        records[name] = {
            "path": str(output.relative_to(ROOT)),
            "size": [scene.render.resolution_x, scene.render.resolution_y],
            "camera_basis": basis,
        }
    return records


def render_rabbit_orientation_qa(scene: bpy.types.Scene) -> dict:
    """Render the mascot alone to prove it has no fused multi-view face."""
    camera = scene.camera
    target = local_point(RABBIT_CENTER_WORLD)
    distance = 1.0
    visible_names = {"Rabbit_Solid", "Rabbit_Eyes", "RabbitNose", "Rabbit_Cheeks"}
    hidden_state = {}
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH":
            continue
        hidden_state[obj.name] = obj.hide_render
        obj.hide_render = obj.name not in visible_names

    views = {
        "rabbit_front": Vector((0.0, 0.0, distance)),
        "rabbit_left": Vector((-distance, 0.0, 0.0)),
        "rabbit_right": Vector((distance, 0.0, 0.0)),
        "rabbit_rear": Vector((0.0, 0.0, -distance)),
    }
    records = {}
    for name, offset in views.items():
        camera.location = target + offset
        basis = orient_qa_camera(camera, target)
        camera.data.ortho_scale = 0.38
        output = QA / f"{name}.png"
        scene.render.filepath = str(output)
        bpy.ops.render.render(write_still=True)
        records[name] = {
            "path": str(output.relative_to(ROOT)),
            "size": [scene.render.resolution_x, scene.render.resolution_y],
            "camera_basis": basis,
        }

    for name, state in hidden_state.items():
        if bpy.data.objects.get(name):
            bpy.data.objects[name].hide_render = state
    return records


def object_bounds(obj: bpy.types.Object) -> tuple[Vector, Vector]:
    points = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    return (
        Vector((min(point[i] for point in points) for i in range(3))),
        Vector((max(point[i] for point in points) for i in range(3))),
    )


def combined_object_bounds(names: tuple[str, ...]) -> tuple[Vector, Vector]:
    objects = [bpy.data.objects[name] for name in names if bpy.data.objects.get(name)]
    if not objects:
        return Vector((0.0, 0.0, 0.0)), Vector((0.0, 0.0, 0.0))
    bounds = [object_bounds(obj) for obj in objects]
    return (
        Vector((min(item[0][i] for item in bounds) for i in range(3))),
        Vector((max(item[1][i] for item in bounds) for i in range(3))),
    )


def collect_validation(
    root: bpy.types.Object,
    raw_info: dict,
    texture_records: list[dict],
    qa_records: dict,
    rabbit_qa_records: dict,
) -> dict:
    objects = list(bpy.context.scene.objects)
    minimum, maximum, triangles, polygons = bounds_and_counts(objects)
    mesh_objects = [obj for obj in objects if obj.type == "MESH"]
    root_children = [obj for obj in root.children]
    marker_records = {}
    for name in ("RightHandGrip", "LeftHandGrip", "Muzzle"):
        marker = bpy.data.objects.get(name)
        marker_records[name] = {
            "exists": marker is not None,
            "parent": marker.parent.name if marker and marker.parent else None,
            "local_location": v3(marker.location) if marker else None,
            "local_rotation_euler": v3(marker.rotation_euler) if marker else None,
            "local_plus_z": v3(marker.matrix_local.to_3x3() @ Vector((0, 0, 1))) if marker else None,
        }
    banned = [obj.name for obj in objects if obj.type in {"CAMERA", "LIGHT", "ARMATURE"}]
    non_unit_scale = [
        {"name": obj.name, "scale": v3(obj.scale)}
        for obj in objects
        if obj.type == "MESH" and any(abs(float(s) - 1.0) > 1e-5 for s in obj.scale)
    ]
    material_names = sorted({slot.material.name for obj in mesh_objects for slot in obj.material_slots if slot.material})
    rabbit_min, rabbit_max = combined_object_bounds(("Rabbit_Solid", "Rabbit_Eyes", "RabbitNose", "Rabbit_Cheeks"))
    chamber_inner_center = local_point(CHAMBER_CENTER_WORLD)
    chamber_inner_half = CHAMBER_SIZE * 0.5 - CHAMBER_INNER_WALL
    chamber_inner_min = chamber_inner_center - chamber_inner_half
    chamber_inner_max = chamber_inner_center + chamber_inner_half
    rabbit_margin = 0.010
    rabbit_inside_inner = all(
        rabbit_min[i] >= chamber_inner_min[i] + rabbit_margin
        and rabbit_max[i] <= chamber_inner_max[i] - rabbit_margin
        for i in range(3)
    )
    rabbit_solid_min, rabbit_solid_max = combined_object_bounds(("Rabbit_Solid",))
    face_min, face_max = combined_object_bounds(("Rabbit_Eyes", "RabbitNose", "Rabbit_Cheeks"))
    rabbit_center_local = local_point(RABBIT_CENTER_WORLD)
    feature_only_on_muzzle_side = face_min.z > rabbit_center_local.z
    expected_camera_checks = {
        "front_from_plus_z": qa_records.get("front", {}).get("camera_basis", {}).get("view_from", [0, 0, 0])[2] > 0.90,
        "left_from_minus_x": qa_records.get("left", {}).get("camera_basis", {}).get("view_from", [0, 0, 0])[0] < -0.99,
        "right_from_plus_x": qa_records.get("right", {}).get("camera_basis", {}).get("view_from", [0, 0, 0])[0] > 0.99,
        "rear_from_minus_z": qa_records.get("rear", {}).get("camera_basis", {}).get("view_from", [0, 0, 0])[2] < -0.99,
        "cardinal_views_world_y_up": all(
            qa_records.get(name, {}).get("camera_basis", {}).get("screen_up") == [0.0, 1.0, 0.0]
            for name in ("front", "left", "right", "rear")
        ),
    }
    conditions = {
        "single_root": root is not None and root.name == "ITM_WPN_GNR_0013_root",
        "root_direct_markers": all(marker_records[name]["parent"] == root.name for name in marker_records),
        "right_grip_at_root": marker_records["RightHandGrip"]["local_location"] == [0.0, 0.0, 0.0],
        "muzzle_plus_z": marker_records["Muzzle"]["local_plus_z"] == [0.0, 0.0, 1.0],
        "no_negative_or_nonuniform_mesh_scale": not non_unit_scale,
        "no_camera_light_armature_in_export_set": not banned,
        "glass_mesh_present": bpy.data.objects.get("CoolingChamber_Glass") is not None,
        "rabbit_solid_present": bpy.data.objects.get("Rabbit_Solid") is not None,
        "rabbit_face_materials_present": all(name in material_names for name in ("M_RabbitEyes", "M_RabbitNose", "M_RabbitCheeks")),
        "principled_materials": all(
            mat.use_nodes and any(node.bl_idname == "ShaderNodeBsdfPrincipled" for node in mat.node_tree.nodes)
            for mat in bpy.data.materials
            if mat.name.startswith(("M_Weapon", "M_Ice", "M_Rabbit"))
        ),
        "rabbit_inside_glass_inner_with_margin": rabbit_inside_inner,
        "rabbit_face_features_only_on_plus_z": feature_only_on_muzzle_side,
        "qa_camera_basis": all(expected_camera_checks.values()),
        "qa_renders_present": all((QA / f"{name}.png").is_file() for name in ("front", "left", "right", "rear", "isometric")),
        "rabbit_orientation_renders_present": all(
            (QA / f"rabbit_{name}.png").is_file()
            for name in ("front", "left", "right", "rear")
        ),
    }
    return {
        "asset": "ITM_WPN_GNR_0013",
        "generated_at": utc_now(),
        "blender_version": bpy.app.version_string,
        "coordinate_contract": {
            "raw_muzzle": "+X",
            "raw_up": "+Z",
            "final_muzzle": "+Z",
            "final_up": "+Y",
            "rabbit_face": "+Z",
            "root_origin": "trigger right-hand grip centre",
        },
        "raw_processing": raw_info,
        "root": root.name if root else None,
        "root_children": sorted(child.name for child in root_children),
        "markers": marker_records,
        "bounds_root_local": {"min": v3(minimum), "max": v3(maximum), "dimensions": v3(maximum - minimum)},
        "rabbit_bounds_root_local": {"min": v3(rabbit_min), "max": v3(rabbit_max), "dimensions": v3(rabbit_max - rabbit_min)},
        "rabbit_solid_bounds_root_local": {"min": v3(rabbit_solid_min), "max": v3(rabbit_solid_max)},
        "rabbit_face_feature_bounds_root_local": {"min": v3(face_min), "max": v3(face_max)},
        "chamber_inner_bounds_root_local": {"min": v3(chamber_inner_min), "max": v3(chamber_inner_max), "margin": rabbit_margin},
        "mesh_count": len(mesh_objects),
        "triangle_count": triangles,
        "polygon_count": polygons,
        "material_names": material_names,
        "materials": [
            {
                "name": mat.name,
                "principled": any(node.bl_idname == "ShaderNodeBsdfPrincipled" for node in mat.node_tree.nodes) if mat.use_nodes else False,
            }
            for mat in bpy.data.materials
            if mat.name.startswith(("M_Weapon", "M_Ice", "M_Rabbit"))
        ],
        "textures": texture_records,
        "qa_renders": qa_records,
        "rabbit_orientation_renders": rabbit_qa_records,
        "qa_camera_checks": expected_camera_checks,
        "non_unit_scale_objects": non_unit_scale,
        "banned_export_objects": banned,
        "checks": conditions,
        "pass": all(conditions.values()),
        "notes": [
            "P1 opaque chamber interior was removed by face-centre spatial culling.",
            "Rabbit facial details are only on +Z; side views expose one eye/profile and rear view exposes the back of the head.",
            "Sol must independently inspect the exported FBX reimport and Unity material remap before import.",
        ],
    }


def export_fbx(root: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for obj in root.children_recursive:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=str(FBX_PATH),
        use_selection=True,
        object_types={"EMPTY", "MESH"},
        add_leaf_bones=False,
        apply_unit_scale=False,
        use_space_transform=False,
        bake_space_transform=False,
        axis_forward="Z",
        axis_up="Y",
        path_mode="COPY",
        embed_textures=False,
    )


def remove_qa_objects(lights: list[bpy.types.Object]) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    for obj in list(lights):
        if obj.name in bpy.data.objects:
            obj.select_set(True)
    bpy.ops.object.delete()


def main() -> None:
    ensure_dirs()
    clear_scene()
    raw = import_raw()
    raw_info = transform_raw_mesh(raw)
    texture_records = extract_images()
    configure_weapon_material(raw)
    root = create_root([raw])

    chamber_objects = create_chamber()
    rabbit_objects = create_rabbit()
    # New objects are parented under the single root after their transforms are applied.
    for obj in [*chamber_objects, *rabbit_objects]:
        obj.parent = root
        obj.matrix_parent_inverse = root.matrix_world.inverted()
    apply_all_mesh_transforms([raw, *chamber_objects, *rabbit_objects])

    minimum, maximum, _, _ = bounds_and_counts(list(bpy.context.scene.objects))
    center = (minimum + maximum) * 0.5
    longest = max(maximum - minimum)
    lights, scene = setup_qa_scene(root, center, longest)
    qa_records = render_qa(lights, scene, center, longest)
    rabbit_qa_records = render_rabbit_orientation_qa(scene)
    # QA-only cameras/lights are removed before both .blend and FBX export.
    remove_qa_objects(lights)

    validation = collect_validation(root, raw_info, texture_records, qa_records, rabbit_qa_records)
    VALIDATION_PATH.write_text(json.dumps(validation, ensure_ascii=False, indent=2), encoding="utf-8")
    export_fbx(root)
    # Save the clean production scene after FBX export.
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
    print(json.dumps({
        "blend": str(BLEND_PATH),
        "fbx": str(FBX_PATH),
        "validation": str(VALIDATION_PATH),
        "pass": validation["pass"],
        "triangles": validation["triangle_count"],
        "removed_opaque_faces": raw_info["removed_opaque_faces"],
    }, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
