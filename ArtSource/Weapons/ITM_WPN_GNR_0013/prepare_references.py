import os
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parent
DEPENDENCIES = ROOT / ".tools" / "rembg"
MODEL_CACHE = ROOT / ".tools" / "models"
REFERENCES = ROOT / "References"
EXTRACTED = ROOT / "Prepared" / "BiRefNet"

sys.path.insert(0, str(DEPENDENCIES))
os.environ["REMBG_HOME"] = str(MODEL_CACHE)

from PIL import Image
from rembg import new_session, remove


def main() -> None:
    EXTRACTED.mkdir(parents=True, exist_ok=True)
    session = new_session("birefnet-general", providers=["CPUExecutionProvider"])

    for view_name in ("left", "front", "back", "right"):
        source_path = REFERENCES / f"{view_name}.png"
        output_path = EXTRACTED / f"{view_name}.png"

        with Image.open(source_path) as source:
            extracted = remove(
                source.convert("RGBA"),
                session=session,
                decontaminate=True,
            )
            extracted.save(output_path)

        with Image.open(output_path) as saved:
            alpha = saved.getchannel("A")
            print(
                f"{view_name}: size={saved.size} "
                f"alpha={alpha.getextrema()} bbox={alpha.getbbox()}"
            )


if __name__ == "__main__":
    main()
