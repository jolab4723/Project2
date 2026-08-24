import json
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[1]
RAW = ROOT / "Tripo" / "Downloaded" / "item.weapon.shotgun.riotpipe_raw.glb"
OUT = ROOT / "Production" / "component_audit.json"

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(RAW))
source = next(obj for obj in bpy.context.scene.objects if obj.type == "MESH")
bpy.context.view_layer.objects.active = source
source.select_set(True)
bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.mesh.separate(type="LOOSE")
bpy.ops.object.mode_set(mode="OBJECT")

records = []
for obj in [item for item in bpy.context.selected_objects if item.type == "MESH"]:
    mesh = obj.data
    mesh.calc_loop_triangles()
    points = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    records.append(
        {
            "name": obj.name,
            "vertices": len(mesh.vertices),
            "triangles": len(mesh.loop_triangles),
            "bounds_min": list(map(min, zip(*points))),
            "bounds_max": list(map(max, zip(*points))),
            "dimensions": list(obj.dimensions),
        }
    )

records.sort(key=lambda record: record["triangles"], reverse=True)
report = {"component_count": len(records), "components": records}
OUT.write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps(report, indent=2))
