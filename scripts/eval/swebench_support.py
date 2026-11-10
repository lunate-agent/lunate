"""Shared constants, preflight checks and dataset records for the SWE-bench adapter.

The entry point is ``scripts/eval/swebench.py``; this module holds the pieces that decide whether
a run may start and which instance records it will use. It never starts a container.
"""

from __future__ import annotations

import argparse
import json
import os
import subprocess
from datetime import datetime, timezone
from pathlib import Path
from typing import NoReturn

DEFAULT_DATASET = "princeton-nlp/SWE-bench_Verified"
PROVIDER_ENV = (
    "ANTHROPIC_API_KEY",
    "OPENAI_API_KEY",
    "OPENROUTER_API_KEY",
    "GOOGLE_API_KEY",
    "GEMINI_API_KEY",
    "DEEPSEEK_API_KEY",
    "MISTRAL_API_KEY",
    "GROQ_API_KEY",
    "XAI_API_KEY",
    "AZURE_OPENAI_API_KEY",
)
PUBLISH_COMMAND = (
    "dotnet publish src/Lunate.Coding -c Release -r linux-x64 --self-contained true "
    "-p:PublishSingleFile=true -o artifacts/linux-x64"
)


class AdapterError(Exception):
    """A run-ending problem the operator can act on."""


def fail(message: str) -> NoReturn:
    raise AdapterError(message)


def utc_now() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def run_command(command: list[str], **kwargs) -> subprocess.CompletedProcess:
    try:
        return subprocess.run(command, capture_output=True, text=True, check=False, **kwargs)
    except FileNotFoundError:
        fail(f"command not found: {command[0]}")


def check_usage(args: argparse.Namespace) -> None:
    if not args.instance:
        fail("usage: --instance <id> or --dry-run <id> is required")
    if args.dry_run and len(args.instance) != 1:
        fail("usage: --dry-run runs exactly one --instance")
    if not args.model:
        fail("usage: --model <exact-model-id> is required")
    if not args.lunate_bin:
        fail("usage: --lunate-bin <linux-x64 executable> is required")


def load_dataset(args: argparse.Namespace) -> list[dict]:
    if args.dataset_file:
        path = Path(args.dataset_file)
        if not path.is_file():
            fail(f"dataset file '{path}' does not exist")
        text = path.read_text(encoding="utf-8")
        if text.lstrip().startswith("["):
            records = json.loads(text)
        else:
            records = [json.loads(line) for line in text.splitlines() if line.strip()]
    else:
        snippet = (
            "import json, sys\n"
            "from datasets import load_dataset\n"
            "dataset = load_dataset(sys.argv[1], split=sys.argv[2])\n"
            "print(json.dumps([dict(record) for record in dataset]))\n"
        )
        result = subprocess.run(
            [args.eval_python, "-c", snippet, args.dataset_name, args.split],
            capture_output=True,
            text=True,
            check=False,
        )
        if result.returncode != 0:
            fail(
                f"could not load dataset '{args.dataset_name}' with {args.eval_python}: "
                f"{result.stderr.strip() or 'no output'}"
            )
        records = json.loads(result.stdout)
    if not isinstance(records, list):
        fail("dataset must be a JSON array or JSONL records")
    return records


def select_instances(records: list[dict], ids: list[str]) -> list[dict]:
    by_id = {record.get("instance_id"): record for record in records}
    missing = [instance_id for instance_id in ids if instance_id not in by_id]
    if missing:
        fail("instance id(s) not in the dataset: " + ", ".join(missing))
    selected = [by_id[instance_id] for instance_id in ids]
    for instance in selected:
        if not instance.get("problem_statement"):
            fail(f"instance '{instance.get('instance_id')}' has no problem_statement")
    return selected


def instance_image(instance: dict) -> str:
    image = instance.get("image") or instance.get("docker_image")
    if image:
        return str(image)
    return "swebench/sweb.eval.x86_64." + str(instance["instance_id"]).replace("__", "_1776_")


def check_docker(args: argparse.Namespace) -> None:
    result = run_command([args.docker, "version", "--format", "{{.Server.Version}}"], timeout=60)
    if result.returncode != 0:
        detail = result.stderr.strip() or result.stdout.strip() or "no output"
        fail(f"Docker daemon is not reachable with '{args.docker}': {detail}")


def check_binary(path_text: str) -> Path:
    path = Path(path_text).resolve()
    if not path.is_file():
        fail(f"Lunate binary '{path}' does not exist")
    with path.open("rb") as handle:
        magic = handle.read(4)
    if magic != b"\x7fELF":
        fail(
            f"Lunate binary '{path}' is not a linux-x64 ELF executable; publish one with: "
            + PUBLISH_COMMAND
        )
    if not os.access(path, os.X_OK):
        fail(f"Lunate binary '{path}' is not executable; chmod +x it before the run")
    return path


def forwarded_environment(names: list[str]) -> dict[str, str]:
    selected = names or [name for name in PROVIDER_ENV if os.environ.get(name)]
    values: dict[str, str] = {}
    for name in selected:
        value = os.environ.get(name)
        if not value:
            fail(
                f"credential environment variable {name} is not set; set it on the host "
                "(or pass a different --env NAME)"
            )
        values[name] = value
    if not values:
        fail(
            "no provider credential is set; set one of "
            + ", ".join(PROVIDER_ENV)
            + " (or pass --env NAME)"
        )
    return values


def check_official_harness(args: argparse.Namespace, evaluate: str) -> None:
    if evaluate == "never":
        return
    result = run_command([args.eval_python, "-c", "import swebench"], timeout=60)
    if result.returncode != 0:
        fail(
            f"the official swebench package is not importable with '{args.eval_python}'; "
            "install it with: python3 -m pip install swebench"
        )
