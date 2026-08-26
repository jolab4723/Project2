from __future__ import annotations

import hashlib
import importlib.util
from pathlib import Path

from PIL import Image, ImageChops


ROOT = Path(__file__).resolve().parents[2]
VARIANT_ROOT = Path(__file__).resolve().parent
INPUT_DIR = VARIANT_ROOT / "TripoInput"
TRIPO_DIR = VARIANT_ROOT / "Tripo"
SHARED_WORKFLOW = (
    ROOT.parent
    / "item.weapon.grenadelauncher.worldender"
    / "Tripo"
    / "h3_workflow.py"
)
ITEM_ID = "item.weapon.rifle.phaseorchid"
EXPECTED_SHA256 = {
    "front": "12556edd518e5276951dc4a7c91b41c48edeeb8d866e36ba86deabbb68a05152",
    "left": "274c88e4943f0fdcab3415acb18c391cba251dd179492655f6c055acb36cddbf",
    "back": "413999f13057f0ad5e2a3b45fb98c393cfca055ce652c3599404553d26308fae",
    "right": "0f3b51e05321d42de0591cc7ea01d081bf4dfd2e2c980927838aa7321f4c3b25",
}


def load_workflow():
    spec = importlib.util.spec_from_file_location("project2_h3_workflow", SHARED_WORKFLOW)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load {SHARED_WORKFLOW}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def validate_inputs() -> dict[str, dict]:
    records: dict[str, dict] = {}
    images: dict[str, Image.Image] = {}
    for view in ("front", "left", "back", "right"):
        path = INPUT_DIR / f"{view}.png"
        if not path.is_file():
            raise FileNotFoundError(path)
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        if digest != EXPECTED_SHA256[view]:
            raise RuntimeError(f"Unexpected {view}.png SHA-256: {digest}")
        if path.stat().st_size > 20 * 1024 * 1024:
            raise ValueError(f"{path.name} exceeds Tripo's 20 MB image limit")
        with Image.open(path) as image:
            if image.format != "PNG" or image.size != (1254, 1254) or image.mode != "RGBA":
                raise ValueError(
                    f"Invalid {view}: format={image.format}, size={image.size}, mode={image.mode}"
                )
            alpha = image.getchannel("A")
            if alpha.getextrema() != (0, 255):
                raise ValueError(f"{view}.png lacks transparent and opaque pixels")
            bbox = alpha.getbbox()
            if bbox is None or bbox[0] <= 0 or bbox[1] <= 0 or bbox[2] >= image.width or bbox[3] >= image.height:
                raise ValueError(f"{view}.png alpha bounds touch a canvas edge: {bbox}")
            images[view] = image.copy()
        records[view] = {
            "path": str(path.relative_to(ROOT)),
            "size_bytes": path.stat().st_size,
            "sha256": digest,
            "dimensions": [1254, 1254],
            "mode": "RGBA",
        }

    flipped_left = images["left"].transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    if ImageChops.difference(flipped_left, images["right"]).getbbox() is not None:
        raise ValueError("right.png is not the exact horizontal flip of left.png")
    return records


def main() -> None:
    workflow = load_workflow()
    workflow.ROOT = ROOT
    workflow.ITEM_ID = ITEM_ID
    workflow.INPUT_DIR = INPUT_DIR
    workflow.TRIPO_DIR = TRIPO_DIR
    workflow.UPLOAD_MANIFEST = TRIPO_DIR / "h3_upload_manifest.json"
    workflow.TASK_MANIFEST = TRIPO_DIR / "h3_task.json"
    workflow.RESULT_MANIFEST = TRIPO_DIR / "h3_task_result.json"
    workflow.DOWNLOAD_DIR = TRIPO_DIR / "Downloaded"
    workflow.validate_inputs = validate_inputs
    workflow.main()


if __name__ == "__main__":
    main()
