"""Convert Tripo ORM channels into Unity URP/Lit-ready textures."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image, ImageOps


ROOT = Path(__file__).resolve().parent
TEXTURES = ROOT / "Production" / "Textures"
ORM_PATH = TEXTURES / "ITM_WPN_GNR_0013_ORM.png"
METALLIC_SMOOTHNESS_PATH = TEXTURES / "ITM_WPN_GNR_0013_MetallicSmoothness.png"
OCCLUSION_PATH = TEXTURES / "ITM_WPN_GNR_0013_Occlusion.png"
REPORT_PATH = ROOT / "Production" / "QA" / "unity_texture_conversion.json"


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def extrema(channel: Image.Image) -> list[int]:
    minimum, maximum = channel.getextrema()
    return [int(minimum), int(maximum)]


def main() -> None:
    if not ORM_PATH.is_file():
        raise FileNotFoundError(ORM_PATH)

    with Image.open(ORM_PATH) as source:
        orm = source.convert("RGBA")
        occlusion, roughness, metallic, _ = orm.split()
        smoothness = ImageOps.invert(roughness)
        opaque = Image.new("L", orm.size, 255)

        metallic_smoothness = Image.merge(
            "RGBA",
            (metallic, metallic, metallic, smoothness),
        )
        metallic_smoothness.save(METALLIC_SMOOTHNESS_PATH, format="PNG")

        # URP/Lit reads occlusion from the green channel.
        occlusion_map = Image.merge(
            "RGBA",
            (opaque, occlusion, opaque, opaque),
        )
        occlusion_map.save(OCCLUSION_PATH, format="PNG")

        report = {
            "source": str(ORM_PATH.relative_to(ROOT)),
            "source_sha256": sha256(ORM_PATH),
            "size": list(orm.size),
            "source_channels": {
                "R_occlusion": extrema(occlusion),
                "G_roughness": extrema(roughness),
                "B_metallic": extrema(metallic),
            },
            "outputs": {
                "metallic_smoothness": {
                    "path": str(METALLIC_SMOOTHNESS_PATH.relative_to(ROOT)),
                    "R": "source B metallic",
                    "A": "1 - source G roughness",
                    "sha256": sha256(METALLIC_SMOOTHNESS_PATH),
                },
                "occlusion": {
                    "path": str(OCCLUSION_PATH.relative_to(ROOT)),
                    "G": "source R occlusion",
                    "sha256": sha256(OCCLUSION_PATH),
                },
            },
        }

    REPORT_PATH.write_text(
        json.dumps(report, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
