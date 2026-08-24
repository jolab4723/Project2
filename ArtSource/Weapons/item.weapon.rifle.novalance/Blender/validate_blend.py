from __future__ import annotations

import json
import sys
from pathlib import Path

import bpy


def cli_arg(name: str) -> str:
    args = sys.argv[sys.argv.index("--") + 1 :]
    return args[args.index(name) + 1]


source = Path(cli_arg("--source")).resolve()
output = Path(cli_arg("--output")).resolve()
bpy.ops.wm.open_mainfile(filepath=str(source))

objects = list(bpy.context.scene.objects)
roots = [obj for obj in objects if obj.parent is None]
meshes = [obj for obj in objects if obj.type == "MESH"]
forbidden = [obj.name for obj in objects if obj.type in {"CAMERA", "LIGHT", "ARMATURE"}]
mesh_object = meshes[0]
mesh_object.data.calc_loop_triangles()
material = mesh_object.data.materials[0]
principled = next(node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED")
image_nodes = {
    node.name: node.image
    for node in material.node_tree.nodes
    if node.type == "TEX_IMAGE" and node.image is not None
}
image_report = {
    name: {
        "name": image.name,
        "size": list(image.size),
        "colorspace": image.colorspace_settings.name,
        "filepath": image.filepath,
        "resolved": bpy.path.abspath(image.filepath),
        "exists": Path(bpy.path.abspath(image.filepath)).is_file(),
    }
    for name, image in image_nodes.items()
}

direct_names = {obj.name for obj in objects if obj.parent == roots[0]}
required_images = {"BaseColor", "NormalGL", "ORM", "EmissionMap"}
linked_inputs = {
    name: bool(principled.inputs[name].is_linked)
    for name in ("Base Color", "Metallic", "Roughness", "Normal", "Emission Color")
}
checks = {
    "one_root": len(roots) == 1,
    "one_mesh": len(meshes) == 1,
    "triangles_40k_to_60k": 40000 <= len(mesh_object.data.loop_triangles) <= 60000,
    "required_direct_children": {
        "RightHandGrip",
        "LeftHandGrip",
        "Muzzle",
        mesh_object.name,
    }.issubset(direct_names),
    "no_forbidden_objects": not forbidden,
    "positive_uniform_transforms": all(
        abs(value - 1.0) < 1e-6
        for obj in (roots[0], mesh_object)
        for value in obj.scale
    ),
    "pbr_inputs_linked": all(linked_inputs.values()),
    "required_image_nodes": required_images.issubset(image_nodes),
    "images_2k_and_resolved": all(
        entry["size"] == [2048, 2048] and entry["exists"]
        for name, entry in image_report.items()
        if name in required_images
    ),
    "colorspaces": (
        image_report.get("BaseColor", {}).get("colorspace") == "sRGB"
        and image_report.get("EmissionMap", {}).get("colorspace") == "sRGB"
        and image_report.get("NormalGL", {}).get("colorspace") == "Non-Color"
        and image_report.get("ORM", {}).get("colorspace") == "Non-Color"
    ),
}

report = {
    "source": str(source),
    "blender_version": bpy.app.version_string,
    "root": roots[0].name if roots else None,
    "objects": [{"name": obj.name, "type": obj.type, "parent": obj.parent.name if obj.parent else None} for obj in objects],
    "triangles": len(mesh_object.data.loop_triangles),
    "material": material.name,
    "emission_strength": principled.inputs["Emission Strength"].default_value,
    "linked_inputs": linked_inputs,
    "images": image_report,
    "forbidden": forbidden,
    "checks": checks,
    "pass": all(checks.values()),
}
output.parent.mkdir(parents=True, exist_ok=True)
output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(report, ensure_ascii=False, indent=2))
if not report["pass"]:
    raise SystemExit(1)
