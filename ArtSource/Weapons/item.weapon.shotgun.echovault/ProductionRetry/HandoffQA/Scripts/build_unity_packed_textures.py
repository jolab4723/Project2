from __future__ import annotations

import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image


HANDOFF = Path(__file__).resolve().parents[1]
PRODUCTION = HANDOFF.parent
SOURCE_ORM = PRODUCTION / "Textures" / "ORM.png"
SOURCE_BASE_COLOR = PRODUCTION / "Textures" / "BaseColor.png"
SOURCE_NORMAL = PRODUCTION / "Textures" / "Normal.png"
SOURCE_EMISSION = PRODUCTION / "Textures" / "EmissionMask.png"
OUTPUT = HANDOFF / "UnityTextures"


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def stats(channel: np.ndarray) -> dict[str, float | int]:
    return {
        "min": int(channel.min()),
        "max": int(channel.max()),
        "mean": round(float(channel.mean()), 6),
    }


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    base_color = Image.open(SOURCE_BASE_COLOR).convert("RGB")
    normal = Image.open(SOURCE_NORMAL).convert("RGB")
    emission = Image.open(SOURCE_EMISSION).convert("L")
    orm = np.asarray(Image.open(SOURCE_ORM).convert("RGB"), dtype=np.uint8)
    occlusion = orm[:, :, 0]
    roughness = orm[:, :, 1]
    metallic = orm[:, :, 2]
    smoothness = np.subtract(255, roughness, dtype=np.uint8)

    metallic_smoothness = np.zeros((*metallic.shape, 4), dtype=np.uint8)
    metallic_smoothness[:, :, 0] = metallic
    metallic_smoothness[:, :, 3] = smoothness

    occlusion_rgb = np.repeat(occlusion[:, :, None], 3, axis=2)
    mos = np.stack((metallic, occlusion, smoothness), axis=2)

    outputs = {
        "BaseColor.png": base_color,
        "Normal.png": normal,
        "EmissionMask.png": emission,
        "MetallicSmoothness.png": Image.fromarray(metallic_smoothness, mode="RGBA"),
        "Occlusion.png": Image.fromarray(occlusion_rgb, mode="RGB"),
        "MOS.png": Image.fromarray(mos, mode="RGB"),
    }
    for name, image in outputs.items():
        image.save(OUTPUT / name, format="PNG", optimize=True)

    result = {
        "source": {
            "path": str(SOURCE_ORM.relative_to(PRODUCTION)),
            "size": list(orm.shape[1::-1]),
            "mode": "RGB",
            "sha256": sha256(SOURCE_ORM),
            "packing": "R=Occlusion, G=Roughness, B=Metallic"
        },
        "channel_stats": {
            "occlusion": stats(occlusion),
            "roughness": stats(roughness),
            "metallic": stats(metallic),
            "smoothness_1_minus_roughness": stats(smoothness),
        },
        "outputs": {
            "BaseColor.png": {
                "packing": "RGB Base Color; true PNG re-encode of the authored source texels",
                "sha256": sha256(OUTPUT / "BaseColor.png"),
            },
            "Normal.png": {
                "packing": "RGB tangent-space normal; true PNG re-encode of the authored source texels",
                "sha256": sha256(OUTPUT / "Normal.png"),
            },
            "EmissionMask.png": {
                "packing": "L binary global-RGB emission mask",
                "sha256": sha256(OUTPUT / "EmissionMask.png"),
            },
            "MetallicSmoothness.png": {
                "packing": "R=Metallic, G=0, B=0, A=Smoothness",
                "sha256": sha256(OUTPUT / "MetallicSmoothness.png"),
            },
            "Occlusion.png": {
                "packing": "RGB=Occlusion (Unity URP reads G)",
                "sha256": sha256(OUTPUT / "Occlusion.png"),
            },
            "MOS.png": {
                "packing": "R=Metallic, G=Occlusion, B=Smoothness",
                "sha256": sha256(OUTPUT / "MOS.png"),
            },
        },
        "unity_import": {
            "BaseColor.png": "Texture Type Default; sRGB on; white material tint",
            "Normal.png": "Texture Type Normal Map; sRGB off",
            "EmissionMask.png": "Texture Type Default; sRGB off; assign to URP Lit Emission Map",
            "MetallicSmoothness.png": "Texture Type Default; sRGB off; assign to URP Lit Metallic Map; Smoothness Source Metallic Alpha",
            "Occlusion.png": "Texture Type Default; sRGB off; assign to URP Lit Occlusion Map",
            "MOS.png": "Texture Type Default; sRGB off; custom shader packing R Metal, G Occlusion, B Smoothness",
        },
    }
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
