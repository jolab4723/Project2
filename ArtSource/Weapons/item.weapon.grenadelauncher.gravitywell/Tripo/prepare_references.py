import hashlib
import json
from pathlib import Path
from shutil import copy2

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
GENERATED = Path(r"C:\Users\user\.codex\generated_images\01a03164-996f-7f02-81a4-4f010c7033ea")
SOURCE_FILES = {
    "front": "exec-efb5dc88-b0a4-4905-870f-277bd6e0054a.png",
    "left": "exec-63d900f0-f522-4f57-ac81-9e917ec8d053.png",
    "back": "exec-4cf59126-a519-4220-b0b2-c0f9ffa32abb.png",
    "right": "exec-1c08f837-ed6f-4e63-b9c8-b2d292593957.png",
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
            if width < height * 2:
                canvas_size = (height * 2, height)
            elif width > height * 2:
                canvas_height = (width + 1) // 2
                canvas_size = (canvas_height * 2, canvas_height)
            else:
                canvas_size = image.size
            canvas = Image.new("RGBA", canvas_size, (0, 0, 0, 0))
            canvas.alpha_composite(
                image,
                ((canvas.width - width) // 2, (canvas.height - height) // 2),
            )
            resized = canvas.resize((2048, 1024), Image.Resampling.LANCZOS)
            resized.save(reference, "PNG")
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
            "sha256": sha256(reference),
            "normalization": (
                "transparent center-pad to 2:1 when required, then uniform "
                "Lanczos resize; no crop or stretch"
            ),
        }

    manifest = {
        "item_id": "item.weapon.grenadelauncher.gravitywell",
        "view_order": ["front", "left", "back", "right"],
        "records": records,
    }
    (ROOT / "Prepared" / "reference_manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8"
    )


if __name__ == "__main__":
    main()
