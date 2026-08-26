from __future__ import annotations

import hashlib
import importlib.util
import os
import sys
from pathlib import Path

from PIL import Image, ImageChops


ROOT = Path(__file__).resolve().parents[2]
VARIANT_ROOT = Path(__file__).resolve().parent
INPUT_DIR = VARIANT_ROOT / "TripoInput"
ATTEMPT = os.environ.get("WORLDENDER_H3_ATTEMPT", "1").strip()
if ATTEMPT not in {"1", "2"}:
    raise RuntimeError("WORLDENDER_H3_ATTEMPT must be 1 or 2")
TRIPO_DIR = VARIANT_ROOT / ("Tripo" if ATTEMPT == "1" else "TripoAttempt2")
LEGACY_WORKFLOW = ROOT / "Tripo" / "h3_workflow.py"
EXPECTED_SHA256 = {
    "front": "3985a0a06c5dff165198fa3e17c132939b48447b68be9a638066d14b9dd48382",
    "left": "6ff870efc2780959160c9ac60634493421281d63e44993184bf72ae0ee58904d",
    "back": "a999107be3a9a1fd97ec73e828e647b6748cdeb1edd1428387b38e3678c2a856",
    "right": "da7b51f3b86378bf95a72519882de6b6d087b928bee2ad209fa4cf8e60965f67",
}


def load_workflow():
    spec = importlib.util.spec_from_file_location("worldender_h3_workflow", LEGACY_WORKFLOW)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load {LEGACY_WORKFLOW}")
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
            raise ValueError(f"{path.name} exceeds Tripo's 20 MB limit")
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
    if ATTEMPT == "2":
        first_result = VARIANT_ROOT / "Tripo" / "h3_task_result.json"
        if not first_result.is_file():
            raise RuntimeError("Attempt 2 requires the preserved Attempt 1 result manifest")
    workflow = load_workflow()
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
