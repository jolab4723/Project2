from __future__ import annotations

import os
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parent
SOURCE = ROOT.parent / "Tripo" / "Downloaded" / "model.glb"
OUT_DIR = ROOT / "QA" / "AnchorProbe"
OUT_DIR.mkdir(parents=True, exist_ok=True)


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def marker(name: str, location: tuple[float, float, float], color: tuple[float, float, float, float]) -> None:
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=12, radius=0.012, location=location)
    obj = bpy.context.object
    obj.name = name
    material = bpy.data.materials.new(f"M_{name}")
    material.diffuse_color = color
    material.use_nodes = True
    principled = material.node_tree.nodes.get("Principled BSDF")
    principled.inputs["Base Color"].default_value = color
    principled.inputs["Roughness"].default_value = 0.28
    obj.data.materials.append(material)


bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=os.fspath(SOURCE))

right_grip = Vector((0.205, 0.0, -0.075))
left_grip = Vector((right_grip.x - 0.326, 0.0, -0.015))
muzzle = Vector((-0.5, -0.0051376, 0.0789125))
marker("RightHandGripProbe", tuple(right_grip), (1.0, 0.02, 0.02, 1.0))
marker("LeftHandGripProbe", tuple(left_grip), (0.02, 1.0, 0.02, 1.0))
marker("MuzzleProbe", tuple(muzzle), (0.02, 0.15, 1.0, 1.0))

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1536
scene.render.resolution_y = 768
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.view_settings.look = "AgX - Medium High Contrast"
scene.world.color = (0.025, 0.025, 0.025)

camera_data = bpy.data.cameras.new("AnchorProbeCamera")
camera_data.type = "ORTHO"
camera_data.ortho_scale = 1.28
camera = bpy.data.objects.new("AnchorProbeCamera", camera_data)
scene.collection.objects.link(camera)
scene.camera = camera
target = Vector((0.0, 0.0, 0.0))

for name, direction in {"side_y_pos": (0.0, 1.0, 0.0), "side_y_neg": (0.0, -1.0, 0.0)}.items():
    camera.location = Vector(direction) * 3.0
    look_at(camera, target)
    scene.render.filepath = os.fspath(OUT_DIR / f"{name}.png")
    bpy.ops.render.render(write_still=True)
