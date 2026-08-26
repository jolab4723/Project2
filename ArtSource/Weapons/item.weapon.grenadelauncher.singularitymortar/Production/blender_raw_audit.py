import json
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[1]
RAW = ROOT / "Tripo" / "Downloaded" / "item.weapon.grenadelauncher.singularitymortar_raw.glb"
OUT = ROOT / "Production" / "raw_audit.json"
SIDE = ROOT / "Production" / "raw_side.png"

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(RAW))

objects = []
all_points = []
triangles = 0
for obj in bpy.context.scene.objects:
    record = {
        "name": obj.name,
        "type": obj.type,
        "location": list(obj.location),
        "rotation_euler": list(obj.rotation_euler),
        "scale": list(obj.scale),
    }
    if obj.type == "MESH":
        mesh = obj.data
        mesh.calc_loop_triangles()
        tri_count = len(mesh.loop_triangles)
        triangles += tri_count
        world_points = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
        all_points.extend(world_points)
        record.update(
            {
                "vertices": len(mesh.vertices),
                "triangles": tri_count,
                "uv_layers": [layer.name for layer in mesh.uv_layers],
                "materials": [slot.material.name if slot.material else None for slot in obj.material_slots],
                "bounds_min": list(map(min, zip(*world_points))),
                "bounds_max": list(map(max, zip(*world_points))),
            }
        )
    objects.append(record)

materials = []
for material in bpy.data.materials:
    nodes = []
    images = []
    if material.use_nodes and material.node_tree:
        for node in material.node_tree.nodes:
            nodes.append(node.bl_idname)
            if node.type == "TEX_IMAGE" and node.image:
                images.append(
                    {
                        "name": node.image.name,
                        "filepath": node.image.filepath,
                        "size": list(node.image.size),
                        "colorspace": node.image.colorspace_settings.name,
                    }
                )
    materials.append({"name": material.name, "nodes": nodes, "images": images})

if all_points:
    bounds_min = list(map(min, zip(*all_points)))
    bounds_max = list(map(max, zip(*all_points)))
    dimensions = [bounds_max[i] - bounds_min[i] for i in range(3)]
else:
    bounds_min = bounds_max = dimensions = [0, 0, 0]

report = {
    "blender_version": bpy.app.version_string,
    "raw_glb": str(RAW.relative_to(ROOT)),
    "object_count": len(objects),
    "mesh_count": sum(1 for obj in objects if obj["type"] == "MESH"),
    "triangles": triangles,
    "bounds_min": bounds_min,
    "bounds_max": bounds_max,
    "dimensions": dimensions,
    "objects": objects,
    "materials": materials,
}
if all_points:
    mesh_objects = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    bin_count = 80
    width = (bounds_max[0] - bounds_min[0]) / bin_count
    bins = [[] for _ in range(bin_count)]
    for obj in mesh_objects:
        for vertex in obj.data.vertices:
            point = obj.matrix_world @ vertex.co
            index = min(bin_count - 1, max(0, int((point.x - bounds_min[0]) / width)))
            bins[index].append(point)
    profile = []
    for index, points in enumerate(bins):
        lo = bounds_min[0] + index * width
        hi = lo + width
        if points:
            profile.append(
                {
                    "x": (lo + hi) * 0.5,
                    "min_z": min(point.z for point in points),
                    "max_z": max(point.z for point in points),
                    "min_y": min(point.y for point in points),
                    "max_y": max(point.y for point in points),
                    "vertices": len(points),
                }
            )
    report["x_profile"] = profile
OUT.write_text(json.dumps(report, indent=2), encoding="utf-8")

camera_data = bpy.data.cameras.new("AuditCamera")
camera = bpy.data.objects.new("AuditCamera", camera_data)
bpy.context.scene.collection.objects.link(camera)
camera.location = (0.0, -2.5, 0.0)
camera.rotation_euler = ((Vector((0, 0, 0)) - camera.location).to_track_quat("-Z", "Y").to_euler())
camera.data.type = "ORTHO"
camera.data.ortho_scale = 1.15
bpy.context.scene.camera = camera
for location, energy, size in (((-1.5, -2.0, 2.0), 900, 3.0), ((1.5, -1.0, -0.5), 600, 2.0)):
    light_data = bpy.data.lights.new("AuditArea", "AREA")
    light_data.energy = energy
    light_data.shape = "DISK"
    light_data.size = size
    light = bpy.data.objects.new("AuditArea", light_data)
    bpy.context.scene.collection.objects.link(light)
    light.location = location
    light.rotation_euler = ((Vector((0, 0, 0)) - light.location).to_track_quat("-Z", "Y").to_euler())
bpy.context.scene.render.engine = "BLENDER_EEVEE"
bpy.context.scene.render.resolution_x = 1024
bpy.context.scene.render.resolution_y = 512
bpy.context.scene.render.resolution_percentage = 100
bpy.context.scene.render.film_transparent = True
bpy.context.scene.render.image_settings.file_format = "PNG"
bpy.context.scene.render.filepath = str(SIDE)
bpy.ops.render.render(write_still=True)
print(json.dumps(report, indent=2))
