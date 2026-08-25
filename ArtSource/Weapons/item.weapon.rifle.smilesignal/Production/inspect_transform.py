from pathlib import Path

import bpy
from mathutils import Matrix, Vector


ROOT = Path(__file__).resolve().parents[1]
GLB = ROOT / "Tripo" / "RetryAfterExpired20260825" / "Downloaded" / "item.weapon.rifle.smilesignal_raw.glb"
RIGHT_HAND_RAW = Vector((0.24, 0.0, -0.105))
ORIENTATION = Matrix(
    (
        (0.0, -1.0, 0.0, 0.0),
        (0.0, 0.0, 1.0, 0.0),
        (-1.0, 0.0, 0.0, 0.0),
        (0.0, 0.0, 0.0, 1.0),
    )
)
TRANSFORM = Matrix.Scale(0.82, 4) @ ORIENTATION @ Matrix.Translation(-RIGHT_HAND_RAW)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(GLB))
obj = next(item for item in bpy.context.scene.objects if item.type == "MESH")
obj.data.transform(TRANSFORM)
obj.data.update()
points = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
minimum = Vector(tuple(min(point[i] for point in points) for i in range(3)))
maximum = Vector(tuple(max(point[i] for point in points) for i in range(3)))
print("MIN", tuple(round(v, 6) for v in minimum))
print("MAX", tuple(round(v, 6) for v in maximum))
print("DIM", tuple(round(v, 6) for v in maximum - minimum))
print("RAW_MUZZLE_TO_WORLD", tuple(round(v, 6) for v in (TRANSFORM @ Vector((-0.5, 0.0, 0.0)))))
print("RAW_UP_TO_WORLD", tuple(round(v, 6) for v in ((Matrix.Scale(0.82, 4) @ ORIENTATION).to_3x3() @ Vector((0.0, 0.0, 1.0)))))
direction = Vector((1.0, 0.0, 0.0))
rotation = direction.to_track_quat("-Z", "Y").to_matrix()
print("CAMERA_LOCAL_X_WORLD", tuple(round(v, 6) for v in (rotation @ Vector((1.0, 0.0, 0.0)))))
print("CAMERA_LOCAL_Y_WORLD", tuple(round(v, 6) for v in (rotation @ Vector((0.0, 1.0, 0.0)))))
print("CAMERA_LOCAL_MINUS_Z_WORLD", tuple(round(v, 6) for v in (rotation @ Vector((0.0, 0.0, -1.0)))))
