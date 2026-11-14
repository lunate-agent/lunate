"""Executing the `run: offline` and `run: help` blocks against a published binary."""

import os
import shlex
import shutil
import subprocess
import tempfile
from pathlib import Path

from .cli_options import check_cli_options
from .markers import scan_markers
from .paths import document_paths, relative_document
from .text import posix, read_text, write_text


def run_examples(root, binary):
    root = Path(root).resolve()
    problems = []
    runs = []
    for path in document_paths(root):
        relative = relative_document(root, path)
        _, found = scan_markers(root, relative, read_text(path), problems)
        runs.extend(found)
    runnable = [run for run in runs if run[0] in ("offline", "help")]
    if not runnable:
        return problems
    if not binary:
        return problems + ["docs-check: --run needs --binary <path> or LUNATE_BIN"]
    binary = Path(binary).resolve()
    if not binary.is_file():
        return problems + [f"docs-check: binary does not exist: {posix(binary)}"]
    bash = shutil.which("bash")
    if not bash:
        return problems + ["docs-check: --run needs bash on PATH"]

    help_outputs = {}
    scratch = tempfile.mkdtemp(prefix="docs-check-run.", dir=os.environ.get("TMPDIR") or None)
    try:
        bin_dir = Path(scratch) / "bin"
        bin_dir.mkdir()
        shim = bin_dir / "lunate"
        write_text(shim, f"#!/bin/sh\nexec {shlex.quote(str(binary))} \"$@\"\n")
        shim.chmod(0o755)
        for index, (kind, reason, relative, line, body) in enumerate(runnable):
            work = Path(scratch) / f"run-{index}"
            home = work / "home"
            lunate_home = work / "lunate-home"
            home.mkdir(parents=True)
            lunate_home.mkdir()
            environment = os.environ.copy()
            environment["PATH"] = str(bin_dir) + os.pathsep + environment.get("PATH", "")
            environment["HOME"] = str(home)
            environment["LUNATE_HOME"] = str(lunate_home)
            environment["XDG_CONFIG_HOME"] = str(home / ".config")
            result = subprocess.run(
                [bash, "-euo", "pipefail", "-c", body],
                cwd=work,
                env=environment,
                capture_output=True,
                text=True,
                timeout=120,
                check=False,
            )
            if result.returncode != 0:
                output = (result.stdout + result.stderr).strip()
                problems.append(
                    f"{relative}:{line}: run:{kind} exited {result.returncode}"
                    + (f": {output}" if output else "")
                )
            if kind == "help":
                help_outputs.setdefault(relative, []).append(result.stdout)
        check_cli_options(root, help_outputs, problems)
        return problems
    finally:
        shutil.rmtree(scratch, ignore_errors=True)
