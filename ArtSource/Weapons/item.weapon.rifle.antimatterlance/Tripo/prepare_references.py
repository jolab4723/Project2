import hashlib
import json
from pathlib import Path
from shutil import copy2

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
GENERATED = Path(r"C:\Users\user\.codex\generated_images\01a03164-c608-7912-b94a-05ebdbe7fb47")
SOURCE_FILES = {
    "front": "exec-b10a4945-034a-4b06-b3db-3880ee3113f3.png",
    "left": "exec-b66e2e8d-a67a-4db2-b6c9-9deb1dd15e21.png",
    "back": "exec-f93a94d4-0e68-4cc9-b053-bf289df78fad.png",
    "right": "exec-811d64a4-ecd7-46eb-abfa-d2a822addbf0.png",
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
            if image.mode != "RGBA" or image.size != (1774, 887):
                raise ValueError(f"Unexpected source {view}: mode={image.mode}, size={image.size}")
            alpha = image.getchannel("A").getextrema()
            if alpha != (0, 255):
                raise ValueError(f"Source {view} lacks genuine alpha: {alpha}")
            resized = image.resize((2048, 1024), Image.Resampling.LANCZOS)
            resized.save(reference, "PNG")
        copy2(reference, prepared)

        with Image.open(reference) as image:
            if image.mode != "RGBA" or image.size != (2048, 1024):
                raise ValueError(f"Invalid normalized {view}: mode={image.mode}, size={image.size}")
            alpha = image.getchannel("A").getextrema()
            if alpha != (0, 255):
                raise ValueError(f"Normalized {view} lacks genuine alpha: {alpha}")
        if sha256(reference) != sha256(prepared):
            raise ValueError(f"Prepared copy differs for {view}")
        records[view] = {
            "source": str(archived.relative_to(ROOT)),
            "source_size": [1774, 887],
            "reference": str(reference.relative_to(ROOT)),
            "prepared": str(prepared.relative_to(ROOT)),
            "output_size": [2048, 1024],
            "sha256": sha256(reference),
            "normalization": "uniform 2:1 Lanczos resize; no crop or stretch",
        }

    manifest = {
        "item_id": "item.weapon.rifle.antimatterlance",
        "view_order": ["front", "left", "back", "right"],
        "records": records,
    }
    (ROOT / "Prepared" / "reference_manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8"
    )


if __name__ == "__main__":
    main()
