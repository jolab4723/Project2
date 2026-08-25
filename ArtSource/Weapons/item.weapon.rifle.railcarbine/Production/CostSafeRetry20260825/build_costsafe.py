from importlib.util import module_from_spec, spec_from_file_location
from pathlib import Path

from mathutils import Vector


OUT = Path(__file__).resolve().parent
PROJECT = Path(__file__).resolve().parents[5]
BASE_SCRIPT = PROJECT / "ArtSource" / "Weapons" / "item.weapon.rifle.railcarbine" / "ProductionRegeneratedRetry" / "build_retry.py"
SPEC = spec_from_file_location("railcarbine_costsafe_base", BASE_SCRIPT)
base = module_from_spec(SPEC)
SPEC.loader.exec_module(base)

base.OUT = OUT
base.SOURCE = PROJECT / "ArtSource" / "Weapons" / "item.weapon.rifle.railcarbine" / "Tripo" / "CostSafeRetry20260825" / "Downloaded" / "item.weapon.rifle.railcarbine_raw.glb"
base.ITEM_ID = "item.weapon.rifle.railcarbine"
base.TASK_ID = "caabbc04-0a47-408d-80c1-12661dcd2659"
base.BODY_NAME = "RailCarbine_CostSafe_Body"
base.SCALE = 0.654880706921944
base.ROOT_RAW = Vector((0.17888563049853373, 0.0, -0.06940988239700376))
base.LEFT_RAW = Vector((-0.31891495601173026, 0.0, -0.04394590187265919))
base.MUZZLE_RAW = Vector((-0.5, 0.0, 0.0))
base.TARGET_TRIANGLES = 100_000
base.THRESHOLD = {
    "r_max": 90,
    "g_min": 160,
    "b_min": 160,
    "g_minus_r_min": 80,
    "b_minus_r_min": 80,
}

original_configure_material = base.configure_material


def configure_material(body, textures):
    material, records, emission = original_configure_material(body, textures)
    material.name = "M_RailCarbine_CostSafe_PBR"
    emission["designated_family"] = "cyan emitter faces and rail strips"
    return material, records, emission


base.configure_material = configure_material
original_export_fbx = base.export_fbx


def export_fbx(root, path):
    body = next(child for child in root.children_recursive if child.type == "MESH")
    material = body.material_slots[0].material
    principled = next(node for node in material.node_tree.nodes if node.bl_idname == "ShaderNodeBsdfPrincipled")
    emission = principled.inputs["Emission Color"]
    saved_links = [(link.from_socket, link.to_socket) for link in emission.links]
    saved_color = emission.default_value[:]
    saved_strength = principled.inputs["Emission Strength"].default_value
    for link in list(emission.links):
        material.node_tree.links.remove(link)
    emission.default_value = (0.0, 0.0, 0.0, 1.0)
    principled.inputs["Emission Strength"].default_value = 0.0
    original_export_fbx(root, path)
    emission.default_value = saved_color
    principled.inputs["Emission Strength"].default_value = saved_strength
    for source, destination in saved_links:
        material.node_tree.links.new(source, destination)


base.export_fbx = export_fbx
base.main()
