import hashlib
import json
from pathlib import Path
from shutil import copy2

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
GENERATED = Path(r"C:\Users\user\.codex\generated_images\01a03164-996f-7f02-81a4-4f010c7033ea")
SOURCE_FILES = {
    "front": "exec-803734b7-56dd-4cb0-9c03-9526bb29a73e.png",
    "left": "exec-fafbaad9-519e-474c-a65f-1f15ab3b42d1.png",
    "back": "exec-3df6e110-9dd3-4dab-84fe-b5d85c1359c5.png",
    "right": "exec-98e95ad4-0c18-42f7-afd8-bdf4e2236e8c.png",
}


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main() -> None:
    source_dir = ROOT / "References" / "SourceOriginal"
    reference_dir = ROOT / "References"
    input_dir = ROOT / "Prepared" / "TripoInput"
    source_dir.mkdir(parents=True, exist_ok=True)
    input_dir.mkdir(parents=True, exist_ok=True)

    records = {}
    for view, filename in SOURCE_FILES.items():
        source = GENERATED / filename
        archived = source_dir / f"{view}.png"
        reference = reference_dir / f"{view}.png"
        prepared = input_dir / f"{view}.png"
        copy2(source, archived)

        with Image.open(source) as image:
            image.load()
            if image.mode != "RGBA":
                raise ValueError(f"Unexpected source {view}: mode={image.mode}")
            alpha = image.getchannel("A").getextrema()
            if alpha != (0, 255):
                raise ValueError(f"Source {view} lacks genuine alpha: {alpha}")
            source_size = list(image.size)
            width, height = image.size
            if width > 2048 or height > 1024:
                scale = min(2048 / width, 1024 / height)
                placed_image = image.resize(
                    (round(width * scale), round(height * scale)),
                    Image.Resampling.LANCZOS,
                )
            else:
                scale = 1.0
                placed_image = image.copy()
            canvas = Image.new("RGBA", (2048, 1024), (0, 0, 0, 0))
            placement = (
                (canvas.width - placed_image.width) // 2,
                (canvas.height - placed_image.height) // 2,
            )
            canvas.alpha_composite(
                placed_image,
                placement,
            )
            canvas.save(reference, "PNG")
        copy2(reference, prepared)

        with Image.open(reference) as image:
            if image.mode != "RGBA" or image.size != (2048, 1024):
                raise ValueError(
                    f"Invalid normalized {view}: mode={image.mode}, size={image.size}"
                )
            alpha = image.getchannel("A").getextrema()
            if alpha != (0, 255):
                raise ValueError(f"Normalized {view} lacks genuine alpha: {alpha}")
        if sha256(reference) != sha256(prepared):
            raise ValueError(f"Prepared copy differs for {view}")
        records[view] = {
            "source": str(archived.relative_to(ROOT)),
            "source_size": source_size,
            "reference": str(reference.relative_to(ROOT)),
            "prepared": str(prepared.relative_to(ROOT)),
            "output_size": [2048, 1024],
            "placed_size": list(placed_image.size),
            "placement": list(placement),
            "uniform_scale": scale,
            "sha256": sha256(reference),
            "normalization": (
                "transparent center-pad into 2048x1024; only oversized sources "
                "are uniformly downscaled; no crop or stretch"
            ),
        }

    manifest = {
        "item_id": "item.weapon.rifle.railcarbine",
        "view_order": ["front", "left", "back", "right"],
        "records": records,
    }
    (ROOT / "Prepared" / "reference_manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8"
    )


if __name__ == "__main__":
    main()
