from __future__ import annotations

import json
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector


ROOT = Path(bpy.data.filepath).resolve().parent
BLEND_PATH = ROOT / "item.weapon.rifle.railcarbine.blend"
FBX_PATH = ROOT / "item.weapon.rifle.railcarbine.fbx"
REPORT_PATH = ROOT / "QA" / "normal_fix_validation.json"
REIMPORT_BLEND_PATH = ROOT / "QA" / "item.weapon.rifle.railcarbine_fbx_reimport_normals_fixed.blend"
ROOT_NAME = "item.weapon.rifle.railcarbine_root"
MESH_NAME = "RailCarbine_Regenerated_Body"
FRONT_EPSILON = 0.001


def mesh_stats(obj: bpy.types.Object) -> dict:
    mesh = obj.data
    max_z = max(vertex.co.z for vertex in mesh.vertices)
    front_faces = [
        polygon
        for polygon in mesh.polygons
        if max(mesh.vertices[index].co.z for index in polygon.vertices)
        > max_z - FRONT_EPSILON
    ]
    return {
        "vertices": len(mesh.vertices),
        "polygons": len(mesh.polygons),
        "triangles": sum(len(polygon.vertices) - 2 for polygon in mesh.polygons),
        "loops": len(mesh.loops),
        "uv_layers": len(mesh.uv_layers),
        "material_slots": len(obj.material_slots),
        "has_custom_normals": bool(mesh.has_custom_normals),
        "smooth_faces": sum(1 for polygon in mesh.polygons if polygon.use_smooth),
        "max_z": max_z,
        "front_faces": len(front_faces),
        "front_normal_plus_z": sum(1 for polygon in front_faces if polygon.normal.z > 0.5),
        "front_normal_minus_z": sum(1 for polygon in front_faces if polygon.normal.z < -0.5),
    }


def recalculate_outside(obj: bpy.types.Object) -> int:
    mesh = obj.data
    original_normals = [polygon.normal.copy() for polygon in mesh.polygons]

    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    if mesh.has_custom_normals:
        bpy.ops.mesh.customdata_custom_splitnormals_clear()

    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.faces.ensure_lookup_table()
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()

    return sum(
        1
        for old_normal, polygon in zip(original_normals, mesh.polygons)
        if old_normal.dot(polygon.normal) < 0.0
    )


def hierarchy(root: bpy.types.Object) -> list[bpy.types.Object]:
    result = [root]
    for child in root.children:
        result.extend(hierarchy(child))
    return result


def export_fbx(root: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    for obj in hierarchy(root):
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=str(FBX_PATH),
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


def render_muzzle_qa(body: bpy.types.Object) -> None:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 512
    scene.render.resolution_y = 512
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.world.color = (0.035, 0.045, 0.06)

    for slot in body.material_slots:
        if slot.material is not None and hasattr(slot.material, "use_backface_culling"):
            slot.material.use_backface_culling = True

    camera_data = bpy.data.cameras.new("NormalsQaCamera")
    camera = bpy.data.objects.new("NormalsQaCamera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 0.13
    camera.location = (0.0, 0.075, 0.82)
    target = Vector((0.0, 0.075, 0.439))
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()

    key_data = bpy.data.lights.new("NormalsQaKey", type="AREA")
    key_data.energy = 800.0
    key_data.shape = "DISK"
    key_data.size = 0.35
    key = bpy.data.objects.new("NormalsQaKey", key_data)
    scene.collection.objects.link(key)
    key.location = (0.25, 0.30, 0.70)
    key.rotation_euler = (target - key.location).to_track_quat("-Z", "Y").to_euler()

    scene.render.filepath = str(ROOT / "QA" / "normals_fixed_muzzle_blender.png")
    bpy.ops.render.render(write_still=True)


body = bpy.data.objects[MESH_NAME]
root = bpy.data.objects[ROOT_NAME]
before = mesh_stats(body)
changed_faces = recalculate_outside(body)
after = mesh_stats(body)

assert changed_faces in {0, 83133}, changed_faces
assert after["vertices"] == before["vertices"]
assert after["polygons"] == before["polygons"]
assert after["triangles"] == before["triangles"]
assert after["loops"] == before["loops"]
assert after["uv_layers"] == before["uv_layers"] == 1
assert after["material_slots"] == before["material_slots"] == 1
assert after["front_faces"] == before["front_faces"] == 1584
assert after["front_normal_plus_z"] == 1512
assert after["front_normal_minus_z"] == 0
assert not after["has_custom_normals"]

bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
render_muzzle_qa(body)
export_fbx(root)

for obj in list(bpy.data.objects):
    bpy.data.objects.remove(obj, do_unlink=True)
bpy.ops.import_scene.fbx(filepath=str(FBX_PATH), use_custom_normals=True)

reimport_root = bpy.data.objects[ROOT_NAME]
reimport_body = bpy.data.objects[MESH_NAME]
reimport = mesh_stats(reimport_body)
marker_names = ["RightHandGrip", "LeftHandGrip", "Muzzle"]
markers = {
    name: {
        "parent": bpy.data.objects[name].parent.name if bpy.data.objects[name].parent else None,
        "local_location": [round(value, 9) for value in bpy.data.objects[name].location],
        "local_rotation": [round(value, 9) for value in bpy.data.objects[name].rotation_euler],
        "local_scale": [round(value, 9) for value in bpy.data.objects[name].scale],
    }
    for name in marker_names
}

assert reimport["vertices"] == after["vertices"]
assert reimport["triangles"] == after["triangles"]
assert reimport["uv_layers"] == after["uv_layers"]
assert reimport["material_slots"] == after["material_slots"]
assert reimport["front_normal_plus_z"] == 1512
assert reimport["front_normal_minus_z"] == 0
assert all(markers[name]["parent"] == ROOT_NAME for name in marker_names)
assert markers["RightHandGrip"]["local_location"] == [0.0, 0.0, 0.0]
assert all(
    abs(actual - expected) <= 0.000001
    for actual, expected in zip(
        markers["Muzzle"]["local_location"],
        [0.0, 0.082783, 0.444589],
    )
)

bpy.ops.wm.save_as_mainfile(filepath=str(REIMPORT_BLEND_PATH))

report = {
    "item_id": "item.weapon.rifle.railcarbine",
    "cause": (
        "Imported custom split normals were globally inward on 83,133 of 99,999 faces. "
        "URP back-face culling therefore hid the muzzle emitters and side surfaces from "
        "their exterior viewing directions."
    ),
    "fix": (
        "Clear the invalid custom split normals and recalculate all connected face normals "
        "outward. No vertices, triangles, UVs, materials, textures, transforms, or markers "
        "were added, deleted, or moved."
    ),
    "original_diagnosis": {
        "inward_face_windings": 83133,
        "total_face_windings": 99999,
        "muzzle_front_faces": 1584,
        "muzzle_front_minus_z": 1512,
        "muzzle_front_plus_z": 0,
    },
    "changed_face_windings_this_run": changed_faces,
    "before": before,
    "after": after,
    "fbx_reimport": reimport,
    "markers": markers,
    "checks": {
        "geometry_counts_preserved": True,
        "uv_and_material_preserved": True,
        "muzzle_front_faces_point_plus_z": True,
        "inward_front_faces_remaining": False,
        "grip_and_muzzle_markers_preserved": True,
        "fbx_reimport_passed": True,
    },
    "pass": True,
}
REPORT_PATH.parent.mkdir(parents=True, exist_ok=True)
REPORT_PATH.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(report, ensure_ascii=False, indent=2))
