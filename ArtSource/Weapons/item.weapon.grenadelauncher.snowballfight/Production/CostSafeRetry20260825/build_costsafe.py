import json
from importlib.util import module_from_spec, spec_from_file_location
from pathlib import Path

import bpy
from mathutils import Vector


OUT = Path(__file__).resolve().parent
PROJECT = Path(__file__).resolve().parents[5]
BASE_SCRIPT = PROJECT / "ArtSource" / "Weapons" / "item.weapon.rifle.railcarbine" / "ProductionRegeneratedRetry" / "build_retry.py"
SPEC = spec_from_file_location("snowballfight_costsafe_base", BASE_SCRIPT)
base = module_from_spec(SPEC)
SPEC.loader.exec_module(base)

base.OUT = OUT
base.SOURCE = PROJECT / "ArtSource" / "Weapons" / "item.weapon.grenadelauncher.snowballfight" / "Tripo" / "CostSafeRetry20260825" / "Downloaded" / "item.weapon.grenadelauncher.snowballfight_raw.glb"
base.ITEM_ID = "item.weapon.grenadelauncher.snowballfight"
base.TASK_ID = "80c3f948-2931-468c-b16e-93d3c9b4082c"
base.BODY_NAME = "SnowballFight_CostSafe_Body"
base.SCALE = 0.893150685
base.ROOT_RAW = Vector((-0.1300, 0.0, -0.1050))
base.LEFT_RAW = Vector((0.2350, 0.0, -0.0830))
base.MUZZLE_RAW = Vector((0.5, 0.0, 0.0600))
base.TARGET_TRIANGLES = 100_000
base.EMISSION_STRENGTH = 2.2
base.THRESHOLD = {
    "r_min": 125,
    "g_max": 55,
    "b_max": 55,
    "r_minus_g_min": 85,
    "r_minus_b_min": 85,
}


def raw_to_final(point):
    delta = point - base.ROOT_RAW
    return Vector((delta.y, delta.z, delta.x)) * base.SCALE


def transform_body(body):
    raw_min, raw_max = base.bounds(body)
    for vertex in body.data.vertices:
        vertex.co = raw_to_final(Vector(vertex.co))
    body.data.update()
    body.location = (0, 0, 0)
    body.rotation_euler = (0, 0, 0)
    body.scale = (1, 1, 1)
    return {
        "raw_bounds": {"min": base.v3(raw_min), "max": base.v3(raw_max), "dimensions": base.v3(raw_max - raw_min)},
        "mapping": "raw (X,Y,Z) -> final (Y,Z,X), translated to trigger-grip origin",
        "scale": base.SCALE,
        "root_raw": base.v3(base.ROOT_RAW),
    }


def make_emission(color_image, path):
    width, height = color_image.size
    flat = base.np.empty(width * height * 4, dtype=base.np.float32)
    color_image.pixels.foreach_get(flat)
    rgba = flat.reshape((height, width, 4))
    rgb = base.np.rint(base.linear_to_srgb(rgba[:, :, :3]) * 255.0).astype(base.np.int16)
    r, g, b = rgb[:, :, 0], rgb[:, :, 1], rgb[:, :, 2]
    threshold = base.THRESHOLD
    selected = (
        (r >= threshold["r_min"])
        & (g <= threshold["g_max"])
        & (b <= threshold["b_max"])
        & ((r - g) >= threshold["r_minus_g_min"])
        & ((r - b) >= threshold["r_minus_b_min"])
    )
    count = int(selected.sum())
    if count == 0:
        raise RuntimeError("Saturated red RGB threshold selected no pixels")
    representative = [int(value) for value in base.np.median(rgb[selected], axis=0)]
    mask = base.np.zeros((height, width, 4), dtype=base.np.float32)
    mask[:, :, :3] = selected[:, :, None].astype(base.np.float32)
    mask[:, :, 3] = 1.0
    image = bpy.data.images.new(f"{base.ITEM_ID}_Emission", width=width, height=height, alpha=True)
    image.colorspace_settings.name = "Non-Color"
    image.pixels.foreach_set(mask.reshape(-1))
    image.filepath_raw = str(path)
    image.file_format = "PNG"
    image.save()
    linear = [round(base.srgb_to_linear(value), 7) for value in representative]
    return image, {
        "path": str(path),
        "source_base_color": color_image.filepath_raw,
        "method": "single global BaseColor sRGB saturated-red-family threshold",
        "threshold_srgb_8bit": threshold,
        "selected_pixels": count,
        "selected_fraction": round(count / float(width * height), 8),
        "representative_srgb_8bit": representative,
        "unity_linear_rgb_target": linear,
        "formula": "c<=0.04045 ? c/12.92 : ((c+0.055)/1.055)^2.4, c=sRGB/255",
        "binary_values": [0, 255],
        "designated_family": "saturated red inset core, tube, and groove pixels only",
        "uv_mesh_spatial_component_morphology_blur_raycast_exception": False,
        "non_glow_paint_color_reuse_observed": False,
    }


original_configure_material = base.configure_material


def configure_material(body, textures):
    material, records, emission = original_configure_material(body, textures)
    material.name = "M_SnowballFight_CostSafe_PBR"
    return material, records, emission


base.raw_to_final = raw_to_final
base.transform_body = transform_body
base.make_emission = make_emission
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

validation_path = OUT / "validation.json"
validation = json.loads(validation_path.read_text(encoding="utf-8"))
validation["coordinate_contract"] = {
    "raw_muzzle": "+X",
    "raw_up": "+Z",
    "final_muzzle": "+Z",
    "final_up": "+Y",
    "root_origin": "actual trigger-grip center",
}
validation["grip_reference"] = {
    "right_grip_raw": base.v3(base.ROOT_RAW),
    "left_grip_raw": base.v3(base.LEFT_RAW),
    "muzzle_raw": base.v3(base.MUZZLE_RAW),
    "left_grip_final_z_m": round(raw_to_final(base.LEFT_RAW).z, 6),
}
validation["transparent_ice_guidance"] = {
    "raw_h3_material_slots": 1,
    "separate_transparent_material_created": False,
    "reason": "Preserved the single H3 UV/material topology; no mesh or UV region split was introduced.",
    "unity_guidance": "If later isolated by authored material data, use URP/Lit Transparent with restrained alpha and preserve the BaseColor/Normal/ORM maps; current FBX intentionally remains one PBR material.",
}
validation_path.write_text(json.dumps(validation, ensure_ascii=False, indent=2), encoding="utf-8")
