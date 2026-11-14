#!/usr/bin/env python3
"""Fail a pull request that misses one of the four mechanical pre-ready checks.

The lane used to run `~/.hermes/scripts/lunate-pr-ready.py`, outside the repository and invisible
on the pull request. This is the committed replacement: it reports the findings that have cost this
week's reviews before a reviewer is spent, and it is advisory until a maintainer adds `pre-ready`
to the ruleset's required checks.

The four checks:

1. The archive is the last commit. A pull request that adds or modifies
   `openspec/changes/<name>/` must end with `openspec archive <name> --yes`: the change directory is
   gone at HEAD, the HEAD commit deletes or renames out of it, and the HEAD commit adds or renames
   into `openspec/changes/archive/<YYYY-MM-DD>-<name>/`.
2. A changelog fragment exists. A non-release pull request adds `changes/<pr-number>.md` and never
   edits `CHANGELOG.md`. The release pull request is exempt, and the rule is inert until
   `changes/README.md` is on the base branch.
3. No hand-edited spec. A change under `openspec/specs/` must ride with an archive under
   `openspec/changes/archive/`, the rule `scripts/check-spec-archive.py` enforces on `main`.
4. Every acceptance check is answered by number. Each list item under the linked issue's
   `Acceptance` heading needs an answer in the pull request body or a comment: its number, `met`,
   and a `file:line` or a test name.

Usage:
    pre-ready.py --list-linked-issues --pr-json pr.json
    pre-ready.py --base origin/main --pr-json pr.json --comments-json comments.json \
        --issues-dir issues/
    pre-ready.py --paths-file changed.txt --head-paths-file head.txt \
        --head-tree-file head-tree.txt --base-tree-file base-tree.txt \
        --pr-json pr.json --comments-json comments.json --issues-dir issues/

When a git input file is omitted the script runs the corresponding `git` command in this
repository. The script itself never reads the network: the workflow writes the pull-request inputs
from `gh api` and the tests pass fixtures in the same shape.
"""

from __future__ import annotations

import argparse
import json
import subprocess
import sys
from pathlib import Path

from pre_ready_acceptance import check_acceptance, linked_issues
from pre_ready_checks import (
    CHECK_LABELS,
    check_archive,
    check_fragment,
    check_specs,
    parse_name_status,
)

REPOSITORY_ROOT = Path(__file__).resolve().parents[1]


class PreReadyError(Exception):
    """An input could not be read; this is not one of the four findings."""


def git(*arguments: str) -> str:
    result = subprocess.run(
        ["git", *arguments], cwd=REPOSITORY_ROOT, capture_output=True, text=True, check=False
    )
    if result.returncode != 0:
        raise PreReadyError(f"`git {' '.join(arguments)}` failed: {result.stderr.strip()}")
    return result.stdout


def read_text(path: str | None, fallback: tuple[str, ...], what: str) -> str:
    if path:
        try:
            return Path(path).read_text(encoding="utf-8")
        except OSError as error:
            raise PreReadyError(f"could not read {what} from {path}: {error}") from error
    return git(*fallback)


def read_lines(path: str | None, fallback: tuple[str, ...], what: str) -> list[str]:
    return [line.strip() for line in read_text(path, fallback, what).splitlines()]


def load_json(path: str, what: str):
    try:
        return json.loads(Path(path).read_text(encoding="utf-8"))
    except (OSError, ValueError) as error:
        raise PreReadyError(f"could not read {what} from {path}: {error}") from error


def read_comments(path: str | None) -> list[dict]:
    if not path:
        return []
    data = load_json(path, "the pull request comments")

    def flatten(value):
        if isinstance(value, list):
            for item in value:
                yield from flatten(item)
        elif isinstance(value, dict):
            yield value

    comments = [item for item in flatten(data) if isinstance(item, dict)]
    return sorted(comments, key=lambda comment: comment.get("created_at") or "")


def read_issues(directory: str | None) -> dict[int, dict]:
    issues: dict[int, dict] = {}
    if not directory:
        return issues
    for path in sorted(Path(directory).glob("*.json")):
        issue = load_json(str(path), f"linked issue {path.name}")
        if not isinstance(issue, dict):
            raise PreReadyError(f"linked issue {path.name} is not a JSON object")
        number = issue.get("number")
        if number is None:
            try:
                number = int(path.stem)
            except ValueError as error:
                raise PreReadyError(
                    f"linked issue {path.name} has no number and its name is not one"
                ) from error
        issues[int(number)] = issue
    return issues


def run(arguments: argparse.Namespace) -> int:
    pull_request = load_json(arguments.pr_json, "the pull request")
    if not isinstance(pull_request, dict):
        raise PreReadyError("the pull request JSON is not an object")
    title = pull_request.get("title") or ""
    body = pull_request.get("body") or ""

    if arguments.list_linked_issues:
        for number in linked_issues(title, body):
            print(number)
        return 0

    number = pull_request.get("number") or arguments.pull_request
    if number is None:
        raise PreReadyError("the pull request JSON carries no number; pass --pull-request")
    number = int(number)
    branch = (pull_request.get("head") or {}).get("ref") or arguments.branch or ""
    base = arguments.base or "origin/main"

    changed = parse_name_status(
        read_text(
            arguments.paths_file,
            ("diff", "--name-status", "-M", f"{base}...HEAD"),
            "changed paths",
        )
    )
    head_changes = parse_name_status(
        read_text(
            arguments.head_paths_file,
            ("show", "--name-status", "-M", "--format=", "HEAD"),
            "the final commit's changes",
        )
    )
    head_tree = read_lines(
        arguments.head_tree_file, ("ls-tree", "-r", "--name-only", "HEAD"), "the HEAD tree"
    )
    base_tree = read_lines(
        arguments.base_tree_file, ("ls-tree", "-r", "--name-only", base), "the base tree"
    )

    results = [
        check_archive(changed, head_changes, head_tree),
        check_fragment(changed, head_tree, base_tree, number, branch, title),
        check_specs(changed),
        check_acceptance(
            title, body, read_comments(arguments.comments_json), read_issues(arguments.issues_dir)
        ),
    ]
    failed = 0
    for index, result in enumerate(results, start=1):
        stream = sys.stdout if result.ok else sys.stderr
        state = "ok" if result.ok else "FAILED"
        print(
            f"pre-ready: check {index} ({CHECK_LABELS[index]}): {state} — {result.detail}",
            file=stream,
            flush=True,
        )
        if not result.ok:
            failed += 1
    if failed:
        print(
            f"pre-ready: {failed} of {len(results)} checks failed; the findings are above",
            file=sys.stderr,
        )
        return 1
    print("pre-ready: all four checks passed")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Run the four advisory pre-ready pull-request checks."
    )
    parser.add_argument(
        "--base", help="git revision of the base (default: origin/main)"
    )
    parser.add_argument("--pull-request", type=int, help="pull request number (else from pr.json)")
    parser.add_argument("--branch", default="", help="head branch (else from pr.json)")
    parser.add_argument("--paths-file", help="git diff --name-status output for base...HEAD")
    parser.add_argument("--head-paths-file", help="git show --name-status output for HEAD")
    parser.add_argument("--head-tree-file", help="git ls-tree -r --name-only HEAD output")
    parser.add_argument("--base-tree-file", help="git ls-tree -r --name-only <base> output")
    parser.add_argument("--pr-json", required=True, help="the pull request JSON")
    parser.add_argument("--comments-json", help="the pull request's issue comments JSON")
    parser.add_argument("--issues-dir", help="a directory of linked-issue JSON files")
    parser.add_argument(
        "--list-linked-issues",
        action="store_true",
        help="print the issue numbers the pull request links and exit",
    )
    arguments = parser.parse_args()
    try:
        return run(arguments)
    except PreReadyError as error:
        print(f"pre-ready: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
