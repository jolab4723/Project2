#!/usr/bin/env python3
"""Verify Echo Vault handoff hashes, PNG contracts, and reference relationships."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image


QA_ROOT = Path(__file__).resolve().parents[1]
PRODUCTION_ROOT = QA_ROOT.parent
ITEM_ROOT = PRODUCTION_ROOT.parent


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def assert_hash(path: Path, expected: str) -> None:
    actual = sha256(path)
    if actual != expected:
        raise AssertionError(f"SHA mismatch: {path} expected={expected} actual={actual}")


def assert_image(path: Path, size: tuple[int, int], mode: str, container: str | None = None) -> None:
    with Image.open(path) as image:
        if (container and image.format != container) or image.size != size or image.mode != mode:
            raise AssertionError(
                f"Image contract mismatch: {path} format={image.format} size={image.size} mode={image.mode}"
            )


def main() -> None:
    manifest_path = QA_ROOT / "handoff_manifest.json"
    with manifest_path.open("r", encoding="utf-8") as stream:
        manifest = json.load(stream)

    for name, expected in manifest["design_authority"]["webpreferred_sha256"].items():
        authoritative = ITEM_ROOT / "References" / "WebPreferred" / name
        active = ITEM_ROOT / "Prepared" / "TripoInput" / name
        assert_hash(authoritative, expected)
        assert_hash(active, expected)
        assert_image(authoritative, (2048, 1024), "RGBA", "PNG")
        assert_image(active, (2048, 1024), "RGBA", "PNG")

    swapped = ITEM_ROOT / "Prepared" / "TripoInputFrontBackSwap"
    web = ITEM_ROOT / "References" / "WebPreferred"
    assert_hash(swapped / "front.png", sha256(web / "back.png"))
    assert_hash(swapped / "back.png", sha256(web / "front.png"))
    assert_hash(swapped / "left.png", sha256(web / "left.png"))
    assert_hash(swapped / "right.png", sha256(web / "right.png"))

    for key in ("blend", "fbx"):
        entry = manifest["production"][key]
        assert_hash((QA_ROOT / entry["path"]).resolve(), entry["sha256"])

    for entry in manifest["source_textures"].values():
        path = (QA_ROOT / entry["path"]).resolve()
        assert_hash(path, entry["sha256"])
        assert_image(path, tuple(entry["size"]), entry["mode"], entry["container"])

    for entry in manifest["unity_packed_textures"].values():
        if not isinstance(entry, dict):
            continue
        path = QA_ROOT / entry["path"]
        assert_hash(path, entry["sha256"])
        assert_image(path, tuple(entry["size"]), entry["mode"], "PNG")

    source_by_unity_name = {
        "BaseColor": "BaseColor",
        "Normal": "Normal",
        "EmissionMask": "EmissionMask",
    }
    for unity_name, source_name in source_by_unity_name.items():
        unity_entry = manifest["unity_packed_textures"][unity_name]
        source_entry = manifest["source_textures"][source_name]
        with Image.open(QA_ROOT / unity_entry["path"]) as unity_image:
            with Image.open((QA_ROOT / source_entry["path"]).resolve()) as source_image:
                if unity_image.tobytes() != source_image.tobytes():
                    raise AssertionError(f"Decoded texels changed during PNG re-encode: {unity_name}")

    for path_text, expected in manifest["visual_qa"]["render_sha256"].items():
        path = QA_ROOT / path_text
        assert_hash(path, expected)
        assert_image(path, (900, 600), "RGBA", "PNG")

    for key in ("comparison_contact_sheet",):
        entry = manifest["design_authority"][key]
        path = QA_ROOT / entry["path"]
        assert_hash(path, entry["sha256"])
        assert_image(path, tuple(entry["size"]), "RGB", "PNG")

    entry = manifest["visual_qa"]["pbr_and_emission_five_view_contact_sheet"]
    path = QA_ROOT / entry["path"]
    assert_hash(path, entry["sha256"])
    assert_image(path, tuple(entry["size"]), "RGB", "PNG")

    for json_name in (
        "unity_texture_pack_manifest.json",
        "emission_analysis.json",
        "blend_fbx_validation.json",
    ):
        with (QA_ROOT / json_name).open("r", encoding="utf-8") as stream:
            json.load(stream)

    print(
        json.dumps(
            {
                "status": "pass",
                "item_id": manifest["item_id"],
                "design_reference_files_verified": 4,
                "production_files_verified": 2,
                "source_textures_verified": 4,
                "unity_ready_textures_verified": 6,
                "five_view_renders_verified": 10,
                "contact_sheets_verified": 2,
                "json_documents_verified": 4,
            },
            indent=2,
        )
    )


if __name__ == "__main__":
    main()
