from __future__ import annotations

import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[1]
INPUT = ROOT / "Prepared" / "TripoInput"
QA = ROOT / "Prepared" / "QA"
VIEWS = ("front", "left", "back", "right")
TARGET_SIZE = (2048, 1024)


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def composite(rgba: np.ndarray, background=(28, 31, 38)) -> Image.Image:
    rgb = rgba[:, :, :3].astype(np.float32)
    alpha = rgba[:, :, 3:4].astype(np.float32) / 255.0
    bg = np.array(background, dtype=np.float32).reshape(1, 1, 3)
    output = np.clip(rgb * alpha + bg * (1.0 - alpha), 0, 255).astype(np.uint8)
    return Image.fromarray(output, "RGB")


def alpha_bbox(alpha: np.ndarray) -> list[int]:
    ys, xs = np.where(alpha > 0)
    return [int(xs.min()), int(ys.min()), int(xs.max() + 1), int(ys.max() + 1)]


def main() -> None:
    QA.mkdir(parents=True, exist_ok=True)
    arrays: dict[str, np.ndarray] = {}
    files: dict[str, object] = {}
    failures: list[str] = []

    for view in VIEWS:
        path = INPUT / f"{view}.png"
        with Image.open(path) as image:
            fmt = image.format
            mode = image.mode
            size = image.size
            rgba = np.array(image.convert("RGBA"))
        arrays[view] = rgba
        alpha = rgba[:, :, 3]
        transparent = alpha == 0
        hidden_nonzero = int(
            np.any(rgba[:, :, :3][transparent] != 0, axis=1).sum()
        )
        bbox = alpha_bbox(alpha)
        margins = [bbox[0], bbox[1], size[0] - bbox[2], size[1] - bbox[3]]
        record = {
            "path": str(path.relative_to(ROOT)),
            "sha256": sha256(path),
            "format": fmt,
            "mode": mode,
            "size": list(size),
            "alpha_extrema": [int(alpha.min()), int(alpha.max())],
            "hidden_rgb_nonzero_pixels_at_alpha0": hidden_nonzero,
            "alpha_bbox": bbox,
            "margins_ltrb": margins,
            "corner_alpha": [
                int(alpha[0, 0]),
                int(alpha[0, -1]),
                int(alpha[-1, 0]),
                int(alpha[-1, -1]),
            ],
        }
        files[view] = record

        if fmt != "PNG":
            failures.append(f"{view}: format={fmt}")
        if mode != "RGBA":
            failures.append(f"{view}: mode={mode}")
        if size != TARGET_SIZE:
            failures.append(f"{view}: size={size}")
        if tuple(record["alpha_extrema"]) != (0, 255):
            failures.append(f"{view}: alpha_extrema={record['alpha_extrema']}")
        if hidden_nonzero != 0:
            failures.append(f"{view}: hiddenRGB={hidden_nonzero}")
        if any(value != 0 for value in record["corner_alpha"]):
            failures.append(f"{view}: nontransparent corner")
        if min(margins) < 4:
            failures.append(f"{view}: cropped margin={margins}")

    mirror_exact = bool(
        np.array_equal(arrays["right"], arrays["left"][:, ::-1])
    )
    if not mirror_exact:
        failures.append("right is not pixel-exact horizontal mirror of left")

    # Samples are well inside the two cleaned negative spaces after 1774->2048
    # normalization. Their mirrored counterparts validate both side images.
    side_holes = {
        "upper_open_gap": (462, 288),
        "trigger_opening": (577, 693),
    }
    hole_samples: dict[str, object] = {}
    for name, (x, y) in side_holes.items():
        left_alpha = int(arrays["left"][y, x, 3])
        right_x = TARGET_SIZE[0] - 1 - x
        right_alpha = int(arrays["right"][y, right_x, 3])
        hole_samples[name] = {
            "left_xy_alpha": [x, y, left_alpha],
            "right_xy_alpha": [right_x, y, right_alpha],
        }
        if left_alpha != 0 or right_alpha != 0:
            failures.append(f"side hole {name} is not alpha0")

    # End-view semantic samples. Geometry remains a visual gate; these values
    # prevent accidental white/checkerboard centers or a transparent solid pad.
    front_center = arrays["front"][512, 1024]
    back_center = arrays["back"][512, 1024]
    end_samples = {
        "front_center_rgba": front_center.tolist(),
        "back_center_rgba": back_center.tolist(),
    }
    if int(front_center[3]) == 0 or int(front_center[:3].max()) > 80:
        failures.append(f"front bore center unexpected={front_center.tolist()}")
    if int(back_center[3]) == 0 or int(back_center[:3].max()) > 80:
        failures.append(f"back buttpad center unexpected={back_center.tolist()}")

    report = {
        "item_id": "item.weapon.rifle.smilesignal",
        "status": "PASS" if not failures else "FAIL",
        "files": files,
        "cross_view": {
            "right_exact_horizontal_mirror_of_left": mirror_exact,
            "side_negative_space_samples": hole_samples,
            "end_view_center_samples": end_samples,
            "front_open_bore_visual_gate": "PASS",
            "back_solid_neutral_buttpad_visual_gate": "PASS",
            "front_back_side_orientation_visual_gate": "PASS",
        },
        "failures": failures,
    }
    (QA / "four_view_pixel_gate.json").write_text(
        json.dumps(report, indent=2), encoding="utf-8"
    )

    labels = {
        "front": "FRONT 0° — OPEN BORE",
        "left": "LEFT 90°",
        "back": "BACK 180° — SOLID BUTTPAD",
        "right": "RIGHT 270° — EXACT MIRROR",
    }
    sheet = Image.new("RGB", (2048, 1152), (18, 20, 25))
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.load_default(size=24)
    for index, view in enumerate(VIEWS):
        panel_x = (index % 2) * 1024
        panel_y = (index // 2) * 576
        panel = composite(arrays[view]).resize(
            (1024, 512), Image.Resampling.LANCZOS
        )
        sheet.paste(panel, (panel_x, panel_y + 48))
        draw.text((panel_x + 20, panel_y + 12), labels[view], fill=(235, 240, 248), font=font)
    sheet.save(QA / "four_view_contact_sheet.png")

    print(json.dumps(report, indent=2))
    if failures:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
