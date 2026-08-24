import argparse
import hashlib
import json
import os
import sys
import time
import urllib.error
import urllib.request
from datetime import datetime, timezone
from decimal import Decimal
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parent
BASE_URL = "https://openapi.tripo3d.ai/v3"
INPUT_DIR = ROOT / "Prepared" / "TripoInput"
TRIPO_DIR = ROOT / "Tripo"
UPLOAD_MANIFEST = TRIPO_DIR / "p1_upload_manifest.json"
TASK_MANIFEST = TRIPO_DIR / "p1_task.json"
DOWNLOAD_DIR = TRIPO_DIR / "Downloaded"
VIEWS = ("front", "left", "back", "right")
EXPECTED_MODEL = "P1-20260311"
EXPECTED_DETAILED_COST = Decimal("60")


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def api_key() -> str:
    value = os.environ.get("TRIPO_API_KEY", "").strip()
    if not value:
        raise RuntimeError("TRIPO_API_KEY is not set")
    return value


def request_json(method: str, path: str, payload: dict | None = None) -> dict:
    body = None if payload is None else json.dumps(payload).encode("utf-8")
    request = urllib.request.Request(
        f"{BASE_URL}{path}",
        data=body,
        method=method,
        headers={
            "Authorization": f"Bearer {api_key()}",
            "Content-Type": "application/json",
            "User-Agent": "Project2-Weapon-P1-Workflow/1.0",
        },
    )
    try:
        with urllib.request.urlopen(request, timeout=60) as response:
            result = json.loads(response.read().decode("utf-8"))
    except urllib.error.HTTPError as error:
        details = error.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"Tripo HTTP {error.code}: {details}") from error
    if result.get("code") != 0:
        raise RuntimeError(f"Tripo API error: {json.dumps(result, ensure_ascii=False)}")
    return result["data"]


def query_balance() -> dict:
    data = request_json("GET", "/account/balance")
    return {
        "balance": str(Decimal(str(data["balance"]))),
        "frozen": str(Decimal(str(data["frozen"]))),
        "checked_at": utc_now(),
    }


def validate_inputs() -> dict[str, dict]:
    records = {}
    for view in VIEWS:
        path = INPUT_DIR / f"{view}.png"
        if not path.is_file():
            raise FileNotFoundError(path)
        if path.stat().st_size > 20 * 1024 * 1024:
            raise ValueError(f"{path.name} exceeds the 20 MB image limit")
        with Image.open(path) as image:
            if image.format != "PNG" or image.size != (2048, 2048) or image.mode != "RGBA":
                raise ValueError(
                    f"{path.name} must be a 2048x2048 RGBA PNG; got "
                    f"format={image.format}, size={image.size}, mode={image.mode}"
                )
            alpha_extrema = image.getchannel("A").getextrema()
            if alpha_extrema[0] != 0 or alpha_extrema[1] != 255:
                raise ValueError(f"{path.name} does not contain both transparent and opaque pixels")
        records[view] = {
            "path": str(path.relative_to(ROOT)),
            "size_bytes": path.stat().st_size,
            "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
        }
    return records


def upload_file(path: Path) -> str:
    presign = request_json("POST", "/files/presign", {"format": "png"})
    upload_request = urllib.request.Request(
        presign["presigned_url"],
        data=path.read_bytes(),
        method="PUT",
        headers={
            "Content-Type": "application/octet-stream",
            "User-Agent": "Project2-Weapon-P1-Workflow/1.0",
        },
    )
    try:
        with urllib.request.urlopen(upload_request, timeout=180) as response:
            if response.status not in (200, 201, 204):
                raise RuntimeError(f"Unexpected upload status {response.status}")
    except urllib.error.HTTPError as error:
        details = error.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"Upload HTTP {error.code}: {details}") from error
    return presign["file_token"]


def command_balance(_: argparse.Namespace) -> None:
    print(json.dumps(query_balance(), ensure_ascii=False, indent=2))


def command_upload(_: argparse.Namespace) -> None:
    input_records = validate_inputs()
    uploaded = {}
    for view in VIEWS:
        token = upload_file(ROOT / input_records[view]["path"])
        uploaded[view] = {**input_records[view], "file_token": token}
        print(f"uploaded {view}", flush=True)
    manifest = {
        "model": EXPECTED_MODEL,
        "uploaded_at": utc_now(),
        "views": uploaded,
    }
    TRIPO_DIR.mkdir(parents=True, exist_ok=True)
    UPLOAD_MANIFEST.write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8"
    )
    print(str(UPLOAD_MANIFEST))


def load_upload_manifest() -> dict:
    if not UPLOAD_MANIFEST.is_file():
        raise FileNotFoundError(f"Upload manifest not found: {UPLOAD_MANIFEST}")
    manifest = json.loads(UPLOAD_MANIFEST.read_text(encoding="utf-8"))
    current = validate_inputs()
    for view in VIEWS:
        if manifest["views"][view]["sha256"] != current[view]["sha256"]:
            raise RuntimeError(f"{view}.png changed after upload; upload again")
    return manifest


def command_submit(args: argparse.Namespace) -> None:
    if args.confirm_consume != "CONSUME_60_CREDITS":
        raise RuntimeError("Refusing to submit without --confirm-consume CONSUME_60_CREDITS")
    if Decimal(args.expected_cost) != EXPECTED_DETAILED_COST:
        raise RuntimeError(
            f"Expected cost guard must be {EXPECTED_DETAILED_COST}, got {args.expected_cost}"
        )

    balance = query_balance()
    actual_balance = Decimal(balance["balance"])
    expected_balance = Decimal(args.expected_balance)
    if actual_balance != expected_balance or Decimal(balance["frozen"]) != Decimal("0"):
        raise RuntimeError(
            "Balance guard failed: "
            f"expected available={expected_balance}, frozen=0; "
            f"actual available={actual_balance}, frozen={balance['frozen']}"
        )
    if actual_balance < EXPECTED_DETAILED_COST:
        raise RuntimeError("Insufficient balance")

    upload_manifest = load_upload_manifest()
    payload = {
        "inputs": [
            {view: upload_manifest["views"][view]["file_token"]}
            for view in VIEWS
        ],
        "model": EXPECTED_MODEL,
        "texture_alignment": "original_image",
        "orientation": "align_image",
        "face_limit": 20000,
        "texture": True,
        "pbr": True,
        "texture_quality": "detailed",
        "export_uv": True,
    }
    result = request_json("POST", "/generation/multiview-to-model", payload)
    task_manifest = {
        "task_id": result["task_id"],
        "submitted_at": utc_now(),
        "balance_before": balance,
        "expected_cost": str(EXPECTED_DETAILED_COST),
        "request": payload,
    }
    TASK_MANIFEST.write_text(
        json.dumps(task_manifest, ensure_ascii=False, indent=2), encoding="utf-8"
    )
    print(json.dumps({"task_id": result["task_id"]}, indent=2))


def task_id_from_args(args: argparse.Namespace) -> str:
    if args.task_id:
        return args.task_id
    if not TASK_MANIFEST.is_file():
        raise FileNotFoundError("No task manifest; pass --task-id")
    return json.loads(TASK_MANIFEST.read_text(encoding="utf-8"))["task_id"]


def command_status(args: argparse.Namespace) -> None:
    data = request_json("GET", f"/tasks/{task_id_from_args(args)}")
    print(json.dumps(data, ensure_ascii=False, indent=2))


def command_wait(args: argparse.Namespace) -> None:
    task_id = task_id_from_args(args)
    deadline = time.monotonic() + args.timeout
    last_progress = None
    while True:
        data = request_json("GET", f"/tasks/{task_id}")
        progress = data.get("progress")
        if progress != last_progress:
            print(f"{data.get('status')} {progress}%", flush=True)
            last_progress = progress
        if data.get("status") in {"success", "failed", "cancelled"}:
            result_path = TRIPO_DIR / "p1_task_result.json"
            result_path.write_text(
                json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8"
            )
            print(str(result_path))
            if data.get("status") != "success":
                raise RuntimeError(f"Task ended with status {data.get('status')}")
            return
        if time.monotonic() >= deadline:
            raise TimeoutError(f"Task did not finish within {args.timeout} seconds")
        time.sleep(args.interval)


def download_url(url: str, destination: Path) -> None:
    request = urllib.request.Request(
        url, headers={"User-Agent": "Project2-Weapon-P1-Workflow/1.0"}
    )
    with urllib.request.urlopen(request, timeout=180) as response:
        destination.write_bytes(response.read())


def command_download(args: argparse.Namespace) -> None:
    data = request_json("GET", f"/tasks/{task_id_from_args(args)}")
    if data.get("status") != "success":
        raise RuntimeError(f"Task is not successful: {data.get('status')}")
    output = data.get("output", {})
    model_url = output.get("model_url")
    if not model_url:
        raise RuntimeError("Task output has no model_url")

    DOWNLOAD_DIR.mkdir(parents=True, exist_ok=True)
    model_path = DOWNLOAD_DIR / "ITM_WPN_GNR_0013_P1_raw.glb"
    download_url(model_url, model_path)
    preview_url = output.get("rendered_image_url")
    if preview_url:
        download_url(preview_url, DOWNLOAD_DIR / "ITM_WPN_GNR_0013_P1_preview.png")
    print(str(model_path))


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser()
    subparsers = parser.add_subparsers(dest="command", required=True)
    subparsers.add_parser("balance").set_defaults(func=command_balance)
    subparsers.add_parser("upload").set_defaults(func=command_upload)

    submit = subparsers.add_parser("submit")
    submit.add_argument("--expected-balance", required=True)
    submit.add_argument("--expected-cost", required=True)
    submit.add_argument("--confirm-consume", required=True)
    submit.set_defaults(func=command_submit)

    for name, handler in (("status", command_status), ("wait", command_wait), ("download", command_download)):
        command = subparsers.add_parser(name)
        command.add_argument("--task-id")
        if name == "wait":
            command.add_argument("--interval", type=int, default=10)
            command.add_argument("--timeout", type=int, default=900)
        command.set_defaults(func=handler)
    return parser


def main() -> None:
    args = build_parser().parse_args()
    try:
        args.func(args)
    except Exception as error:
        print(f"ERROR: {error}", file=sys.stderr)
        raise SystemExit(1) from error


if __name__ == "__main__":
    main()
