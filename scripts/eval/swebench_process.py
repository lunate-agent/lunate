"""Process helpers shared by the SWE-bench run orchestrator and its baseline arm."""

from __future__ import annotations

import subprocess
from datetime import datetime, timezone
from pathlib import Path


def utc_now() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def safe(value: str) -> str:
    return "".join(character if character.isalnum() or character in "_.-" else "_" for character in value)


def instance_image(instance_id: str) -> str:
    return "swebench/sweb.eval.x86_64." + instance_id.replace("__", "_1776_")


def run_command(
    command: list[str], *, cwd: Path, env: dict[str, str], log: Path, timeout: float | None = None
) -> subprocess.CompletedProcess:
    result = subprocess.run(
        command, capture_output=True, text=True, check=False, cwd=cwd, env=env, timeout=timeout
    )
    log.parent.mkdir(parents=True, exist_ok=True)
    log.write_text(result.stdout + result.stderr, encoding="utf-8", newline="\n")
    return result
