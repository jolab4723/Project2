from __future__ import annotations

import argparse
import hashlib
import json
import os
import sys
import time
import urllib.error
import urllib.request
import uuid
from datetime import datetime, timezone
from decimal import Decimal
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
BASE_URL = "https://api.tripo3d.ai/v2/openapi"
ITEM_ID = "item.weapon.rifle.novalance"
INPUT_DIR = ROOT / "Prepared" / "TripoInput"
TRIPO_DIR = ROOT / "Tripo"
UPLOAD_MANIFEST = TRIPO_DIR / "h3_upload_manifest.json"
TASK_MANIFEST = TRIPO_DIR / "h3_task.json"
RESULT_MANIFEST = TRIPO_DIR / "h3_task_result.json"
DOWNLOAD_DIR = TRIPO_DIR / "Downloaded"
VIEWS = ("front", "left", "back", "right")
MODEL_VERSION = "v3.1-20260211"
GEOMETRY_QUALITY = "standard"
EXPECTED_COST = Decimal("30")


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def api_key() -> str:
    value = os.environ.get("TRIPO_API_KEY", "").strip()
    if not value:
        raise RuntimeError("TRIPO_API_KEY is not set")
    return value


def decode_response(response) -> dict:
    result = json.loads(response.read().decode("utf-8"))
    if result.get("code") != 0:
        raise RuntimeError(json.dumps(result, ensure_ascii=False))
    return result["data"]


def request_json(method: str, path: str, payload: dict | None = None) -> dict:
    body = None if payload is None else json.dumps(payload).encode("utf-8")
    request = urllib.request.Request(
        f"{BASE_URL}{path}",
        data=body,
        method=method,
        headers={
            "Authorization": f"Bearer {api_key()}",
            "Content-Type": "application/json",
            "User-Agent": f"Project2-{ITEM_ID}-H3/1.0",
        },
    )
    try:
        with urllib.request.urlopen(request, timeout=90) as response:
            return decode_response(response)
    except urllib.error.HTTPError as error:
        details = error.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"Tripo HTTP {error.code}: {details}") from error


def query_balance() -> dict:
    data = request_json("GET", "/user/balance")
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
            if image.format != "PNG" or image.size != (2048, 1024) or image.mode != "RGBA":
                raise ValueError(
                    f"Invalid {view}: format={image.format}, size={image.size}, mode={image.mode}"
                )
            if image.getchannel("A").getextrema() != (0, 255):
                raise ValueError(f"{view}.png lacks genuine transparent and opaque pixels")
        records[view] = {
            "path": str(path.relative_to(ROOT)),
            "size_bytes": path.stat().st_size,
            "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
        }
    return records


def upload_file(path: Path) -> str:
    boundary = f"----Project2{uuid.uuid4().hex}"
    content = path.read_bytes()
    body = b"".join(
        [
            f"--{boundary}\r\n".encode(),
            b'Content-Disposition: form-data; name="file"; filename="',
            path.name.encode("utf-8"),
            b'"\r\nContent-Type: image/png\r\n\r\n',
            content,
            b"\r\n",
            f"--{boundary}--\r\n".encode(),
        ]
    )
    request = urllib.request.Request(
        f"{BASE_URL}/upload/sts",
        data=body,
        method="POST",
        headers={
            "Authorization": f"Bearer {api_key()}",
            "Content-Type": f"multipart/form-data; boundary={boundary}",
            "User-Agent": f"Project2-{ITEM_ID}-H3/1.0",
        },
    )
    try:
        with urllib.request.urlopen(request, timeout=180) as response:
            data = decode_response(response)
    except urllib.error.HTTPError as error:
        details = error.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"Upload HTTP {error.code}: {details}") from error
    return data["image_token"]


def command_balance(_: argparse.Namespace) -> None:
    print(json.dumps(query_balance(), ensure_ascii=False, indent=2))


def command_validate(_: argparse.Namespace) -> None:
    print(json.dumps(validate_inputs(), ensure_ascii=False, indent=2))


def command_upload(_: argparse.Namespace) -> None:
    inputs = validate_inputs()
    views = {}
    for view in VIEWS:
        token = upload_file(ROOT / inputs[view]["path"])
        views[view] = {**inputs[view], "file_token": token}
        print(f"uploaded {view}", flush=True)
    manifest = {
        "item_id": ITEM_ID,
        "type": "multiview_to_model",
        "model_version": MODEL_VERSION,
        "geometry_quality": GEOMETRY_QUALITY,
        "uploaded_at": utc_now(),
        "view_order": list(VIEWS),
        "views": views,
    }
    TRIPO_DIR.mkdir(parents=True, exist_ok=True)
    UPLOAD_MANIFEST.write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8"
    )
    print(UPLOAD_MANIFEST)


def load_upload_manifest() -> dict:
    if not UPLOAD_MANIFEST.is_file():
        raise FileNotFoundError(UPLOAD_MANIFEST)
    manifest = json.loads(UPLOAD_MANIFEST.read_text(encoding="utf-8"))
    current = validate_inputs()
    for view in VIEWS:
        if manifest["views"][view]["sha256"] != current[view]["sha256"]:
            raise RuntimeError(f"{view}.png changed after upload; upload again")
    return manifest


def command_submit(args: argparse.Namespace) -> None:
    if args.confirm != "API_SUBMIT_GO":
        raise RuntimeError("Refusing submission without exact --confirm API_SUBMIT_GO")
    if Decimal(args.expected_cost) != EXPECTED_COST:
        raise RuntimeError(f"Expected-cost guard must be {EXPECTED_COST}")
    balance = query_balance()
    if (
        Decimal(balance["balance"]) != Decimal(args.expected_balance)
        or Decimal(balance["frozen"]) != 0
    ):
        raise RuntimeError(
            "Balance guard failed: "
            f"expected={args.expected_balance}/0 "
            f"actual={balance['balance']}/{balance['frozen']}"
        )
    if Decimal(balance["balance"]) < EXPECTED_COST:
        raise RuntimeError("Insufficient balance")
    upload = load_upload_manifest()
    payload = {
        "type": "multiview_to_model",
        "files": [
            {"type": "png", "file_token": upload["views"][view]["file_token"]}
            for view in VIEWS
        ],
        "model_version": MODEL_VERSION,
        "geometry_quality": GEOMETRY_QUALITY,
        "texture": True,
        "pbr": True,
        "export_uv": True,
    }
    data = request_json("POST", "/task", payload)
    task = {
        "item_id": ITEM_ID,
        "task_id": data["task_id"],
        "submitted_at": utc_now(),
        "balance_before": balance,
        "expected_cost": str(EXPECTED_COST),
        "request": payload,
    }
    TASK_MANIFEST.write_text(
        json.dumps(task, ensure_ascii=False, indent=2), encoding="utf-8"
    )
    print(json.dumps({"task_id": data["task_id"]}, indent=2))


def task_id(args: argparse.Namespace) -> str:
    if args.task_id:
        return args.task_id
    if not TASK_MANIFEST.is_file():
        raise FileNotFoundError(TASK_MANIFEST)
    return json.loads(TASK_MANIFEST.read_text(encoding="utf-8"))["task_id"]


def command_status(args: argparse.Namespace) -> None:
    print(
        json.dumps(
            request_json("GET", f"/task/{task_id(args)}"),
            ensure_ascii=False,
            indent=2,
        )
    )


def command_wait(args: argparse.Namespace) -> None:
    ident = task_id(args)
    deadline = time.monotonic() + args.timeout
    last_state = None
    while True:
        data = request_json("GET", f"/task/{ident}")
        state = (data.get("status"), data.get("progress"))
        if state != last_state:
            print(f"{state[0]} {state[1]}%", flush=True)
            last_state = state
        if data.get("status") in {"success", "failed", "cancelled"}:
            RESULT_MANIFEST.write_text(
                json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8"
            )
            if data.get("status") != "success":
                raise RuntimeError(f"Task ended {data.get('status')}")
            return
        if time.monotonic() >= deadline:
            raise TimeoutError("Task wait timed out")
        time.sleep(args.interval)


def download_url(url: str, path: Path) -> None:
    request = urllib.request.Request(
        url, headers={"User-Agent": f"Project2-{ITEM_ID}-H3/1.0"}
    )
    with urllib.request.urlopen(request, timeout=240) as response:
        path.write_bytes(response.read())


def command_download(args: argparse.Namespace) -> None:
    data = request_json("GET", f"/task/{task_id(args)}")
    if data.get("status") != "success":
        raise RuntimeError(f"Task is not successful: {data.get('status')}")
    output = data.get("output", {})
    model_url = (
        output.get("pbr_model")
        or output.get("model")
        or output.get("model_url")
    )
    if isinstance(model_url, dict):
        model_url = model_url.get("url")
    if isinstance(model_url, list):
        model_url = model_url[0]
    if not model_url:
        raise RuntimeError("No model URL in task output")
    DOWNLOAD_DIR.mkdir(parents=True, exist_ok=True)
    target = DOWNLOAD_DIR / f"{ITEM_ID}_raw.glb"
    download_url(model_url, target)
    preview_url = output.get("rendered_image") or output.get("rendered_image_url")
    if preview_url:
        download_url(preview_url, DOWNLOAD_DIR / f"{ITEM_ID}_preview.png")
    print(target)


def parser() -> argparse.ArgumentParser:
    result = argparse.ArgumentParser()
    subs = result.add_subparsers(dest="command", required=True)
    subs.add_parser("balance").set_defaults(func=command_balance)
    subs.add_parser("validate").set_defaults(func=command_validate)
    subs.add_parser("upload").set_defaults(func=command_upload)

    submit = subs.add_parser("submit")
    submit.add_argument("--expected-balance", required=True)
    submit.add_argument("--expected-cost", required=True)
    submit.add_argument("--confirm", required=True)
    submit.set_defaults(func=command_submit)

    for name, handler in (
        ("status", command_status),
        ("wait", command_wait),
        ("download", command_download),
    ):
        command = subs.add_parser(name)
        command.add_argument("--task-id")
        if name == "wait":
            command.add_argument("--interval", type=int, default=10)
            command.add_argument("--timeout", type=int, default=1200)
        command.set_defaults(func=handler)
    return result


def main() -> None:
    args = parser().parse_args()
    try:
        args.func(args)
    except Exception as error:
        print(f"ERROR: {error}", file=sys.stderr)
        raise SystemExit(1) from error


if __name__ == "__main__":
    main()
