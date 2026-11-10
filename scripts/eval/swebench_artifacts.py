"""Artifact scanning for the SWE-bench adapter: secret redaction and ``run.json``."""

from __future__ import annotations

import json
from pathlib import Path


def looks_text(path: Path) -> bool:
    try:
        with path.open("rb") as handle:
            chunk = handle.read(8192)
    except OSError:
        return False
    return b"\x00" not in chunk


def redact_tree(run_root: Path, forwarded: dict[str, str]) -> int:
    redacted = 0
    for path in run_root.rglob("*"):
        if not path.is_file() or not looks_text(path):
            continue
        data = path.read_bytes()
        updated = data
        for name, value in forwarded.items():
            if len(value) < 8:
                continue
            updated = updated.replace(value.encode("utf-8"), f"[REDACTED:{name}]".encode("utf-8"))
        if updated != data:
            path.write_bytes(updated)
            redacted += 1
    return redacted


def write_run_json(run_root: Path, payload: dict) -> None:
    (run_root / "run.json").write_text(
        json.dumps(payload, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
        newline="\n",
    )
