import sys
from pathlib import Path

import bpy


def cli_arg(name: str) -> str:
    args = sys.argv[sys.argv.index("--") + 1 :]
    return args[args.index(name) + 1]


source = Path(cli_arg("--source")).resolve()
output_dir = Path(cli_arg("--output-dir")).resolve()
output_dir.mkdir(parents=True, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(source))

mapping = {
    "Color_": "item.weapon.rifle.novalance_BaseColor.png",
    "NormalGL_": "item.weapon.rifle.novalance_NormalGL.png",
    "ORM_": "item.weapon.rifle.novalance_ORM.png",
}

for image in bpy.data.images:
    filename = next((value for prefix, value in mapping.items() if image.name.startswith(prefix)), None)
    if not filename:
        continue
    destination = output_dir / filename
    image.filepath_raw = str(destination)
    image.file_format = "PNG"
    image.save()
    print(destination)
