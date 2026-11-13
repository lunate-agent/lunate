"""Argument parsing and the exit code for `scripts/docs-check`."""

import argparse
import os
import sys
from pathlib import Path

from .checks import check
from .constants import REPOSITORY_ROOT
from .run_examples import run_examples
from .self_test import self_test


def main(argv=None):
    parser = argparse.ArgumentParser(
        prog="docs-check",
        description="Check the documentation tree against docs/docs.json.",
    )
    parser.add_argument("--check", action="store_true", help="check the tree (the default)")
    parser.add_argument("--run", action="store_true", help="execute run:offline and run:help blocks")
    parser.add_argument("--self-test", action="store_true", help="run the checker's own tests")
    parser.add_argument("--root", default=str(REPOSITORY_ROOT), help="repository root")
    parser.add_argument("--binary", help="published lunate binary for --run")
    arguments = parser.parse_args(argv)

    if arguments.self_test:
        return self_test()

    root = Path(arguments.root).resolve()
    problems = check(root)
    if arguments.run:
        problems.extend(run_examples(root, arguments.binary or os.environ.get("LUNATE_BIN")))
    if problems:
        for problem in problems:
            print(f"docs-check: {problem}", file=sys.stderr)
        return 1
    print("docs-check: OK")
    return 0
