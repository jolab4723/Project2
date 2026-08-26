import bpy
import json
import os


ROOT = os.path.dirname(os.path.abspath(__file__))
SOURCE = os.path.normpath(os.path.join(ROOT, "..", "Tripo", "Downloaded", "model.glb"))
TEXTURE_DIR = os.path.join(ROOT, "Textures")
QA_DIR = os.path.join(ROOT, "QA")
os.makedirs(TEXTURE_DIR, exist_ok=True)
os.makedirs(QA_DIR, exist_ok=True)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=SOURCE)

material_report = []
role_images = {}
for material in bpy.data.materials:
    nodes = material.node_tree.nodes if material.use_nodes and material.node_tree else []
    principled = next((node for node in nodes if node.type == "BSDF_PRINCIPLED"), None)
    entry = {
        "name": material.name,
        "use_nodes": material.use_nodes,
        "nodes": [],
        "principled": {},
    }
    for node in nodes:
        node_entry = {"name": node.name, "type": node.type}
        if node.type == "TEX_IMAGE" and node.image:
            node_entry["image"] = node.image.name
            node_entry["colorspace"] = node.image.colorspace_settings.name
        entry["nodes"].append(node_entry)
    if principled:
        for socket_name in ("Base Color", "Metallic", "Roughness", "Alpha", "Normal"):
            socket = principled.inputs.get(socket_name)
            if socket is None:
                continue
            socket_info = {"default_value": list(socket.default_value) if hasattr(socket.default_value, "__len__") else socket.default_value}
            if socket.is_linked:
                link = socket.links[0]
                socket_info["linked_from_node"] = link.from_node.name
                socket_info["linked_from_type"] = link.from_node.type
                socket_info["linked_from_socket"] = link.from_socket.name
                stack = [link.from_node]
                seen = set()
                while stack:
                    current = stack.pop()
                    if current.as_pointer() in seen:
                        continue
                    seen.add(current.as_pointer())
                    if current.type == "TEX_IMAGE" and current.image:
                        socket_info["source_image"] = current.image.name
                        role_images[socket_name] = current.image
                        break
                    for input_socket in current.inputs:
                        for upstream in input_socket.links:
                            stack.append(upstream.from_node)
            entry["principled"][socket_name] = socket_info
    material_report.append(entry)

output_names = {
    "Base Color": "item.weapon.shotgun.embercoil_BaseColor.png",
    "Metallic": "item.weapon.shotgun.embercoil_MetallicRoughness.png",
    "Roughness": "item.weapon.shotgun.embercoil_MetallicRoughness.png",
    "Normal": "item.weapon.shotgun.embercoil_Normal.png",
}
saved = {}
saved_pointers = set()
for role, image in role_images.items():
    if image.as_pointer() in saved_pointers:
        saved[role] = saved.get(next((r for r, i in role_images.items() if i == image and r in saved), role))
        continue
    filename = output_names.get(role, f"item.weapon.shotgun.embercoil_{role.replace(' ', '')}.png")
    path = os.path.join(TEXTURE_DIR, filename)
    image.filepath_raw = path
    image.file_format = "PNG"
    image.save()
    saved_pointers.add(image.as_pointer())
    saved[role] = path

images_report = []
for image in bpy.data.images:
    if image.type != "IMAGE":
        continue
    images_report.append({
        "name": image.name,
        "size": list(image.size),
        "channels": image.channels,
        "colorspace": image.colorspace_settings.name,
        "packed": bool(image.packed_file),
        "filepath": image.filepath,
    })

report = {
    "source": SOURCE,
    "materials": material_report,
    "images": images_report,
    "saved_role_images": saved,
}
with open(os.path.join(QA_DIR, "raw_pbr_inspection.json"), "w", encoding="utf-8") as handle:
    json.dump(report, handle, indent=2)
print(json.dumps(report, indent=2))
