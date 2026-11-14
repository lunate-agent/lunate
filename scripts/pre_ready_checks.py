#!/usr/bin/env python3
"""The archive, changelog-fragment and spec checks, with no IO.

`scripts/pre-ready.py` loads the pull request, its comments, its linked issues and the git data and
calls these functions. The acceptance check lives in `pre_ready_acceptance.py`. Keep both modules
free of `git`, `gh` and the filesystem: the executable owns the inputs and the tests own the
fixtures.
"""

from __future__ import annotations

import re
from dataclasses import dataclass

CHANGES = "openspec/changes/"
ARCHIVE = "openspec/changes/archive/"
SPECS = "openspec/specs/"
CHANGELOG = "CHANGELOG.md"
FRAGMENTS_README = "changes/README.md"
RELEASE_BRANCH = "release/"
RELEASE_TITLE = "chore(release)"

CHECK_LABELS = {
    1: "the archive is the last commit",
    2: "a changelog fragment exists",
    3: "no hand-edited spec",
    4: "every acceptance check is answered by number",
}

ARCHIVE_DIR_NAME = re.compile(r"\d{4}-\d{2}-\d{2}-")


@dataclass
class Change:
    """One `git diff --name-status -M` line: a status and its path or paths."""

    status: str
    paths: list[str]


@dataclass
class CheckResult:
    ok: bool
    detail: str


def parse_name_status(text: str) -> list[Change]:
    changes: list[Change] = []
    for line in text.splitlines():
        if not line.strip():
            continue
        fields = line.split("\t")
        changes.append(Change(fields[0], [field for field in fields[1:] if field]))
    return changes


def changed_paths(changes: list[Change]) -> list[str]:
    return [path for change in changes for path in change.paths]


def added_paths(change: Change) -> list[str]:
    if change.status.startswith("A"):
        return change.paths[:1]
    if change.status.startswith("R"):
        return change.paths[1:2]
    return []


def is_unarchived_change(path: str) -> bool:
    if not path.startswith(CHANGES) or path.startswith(ARCHIVE):
        return False
    name = path[len(CHANGES) :].split("/", 1)[0]
    return bool(name) and name not in ("README.md", "archive")


def change_name(path: str) -> str:
    return path[len(CHANGES) :].split("/", 1)[0]


def archive_directory(path: str) -> str | None:
    if not path.startswith(ARCHIVE):
        return None
    rest = path[len(ARCHIVE) :]
    return rest.split("/", 1)[0] if "/" in rest else None


def names_archive_of(directory: str, name: str) -> bool:
    prefix = ARCHIVE_DIR_NAME.match(directory)
    return bool(prefix) and directory == f"{prefix.group(0)}{name}"


def check_archive(
    changed: list[Change], head_changes: list[Change], head_tree: list[str]
) -> CheckResult:
    names = sorted({change_name(path) for path in changed_paths(changed) if is_unarchived_change(path)})
    if not names:
        return CheckResult(True, "no openspec/changes/<name>/ path is changed")

    archive_directories = {
        directory
        for directory in (archive_directory(path) for path in head_tree)
        if directory is not None
    }
    problems: list[str] = []
    for name in names:
        prefix = f"{CHANGES}{name}/"
        archived = any(names_archive_of(directory, name) for directory in archive_directories)
        still_there = any(path.startswith(prefix) for path in head_tree)
        if not archived:
            problems.append(
                f"openspec/changes/{name}/ is not archived; run `openspec archive {name} --yes` "
                f"in the final commit so it lands under openspec/changes/archive/"
                f"<YYYY-MM-DD>-{name}/"
            )
            continue
        if still_there:
            problems.append(
                f"openspec/changes/{name}/ still exists at HEAD; finish `openspec archive "
                f"{name} --yes` so the directory moves under openspec/changes/archive/"
            )
            continue
        deleted_in_head = any(
            change.status.startswith(("D", "R"))
            and bool(change.paths)
            and change.paths[0].startswith(prefix)
            for change in head_changes
        )
        archived_in_head = any(
            names_archive_of(directory, name)
            for change in head_changes
            for path in added_paths(change)
            for directory in [archive_directory(path)]
            if directory is not None
        )
        if not (deleted_in_head and archived_in_head):
            problems.append(
                f"the archive of {name} is not the final commit; run `openspec archive {name} "
                f"--yes` as the last commit, after the review and before the merge"
            )
    if problems:
        return CheckResult(False, " ".join(problems))
    return CheckResult(True, f"archived in the final commit: {', '.join(names)}")


def is_release(branch: str, title: str) -> bool:
    return branch.startswith(RELEASE_BRANCH) or title.startswith(RELEASE_TITLE)


def check_fragment(
    changed: list[Change],
    head_tree: list[str],
    base_tree: list[str],
    pull_request: int,
    branch: str,
    title: str,
) -> CheckResult:
    if FRAGMENTS_README not in base_tree:
        return CheckResult(True, "inert: changes/README.md is not on the base branch")
    if is_release(branch, title):
        return CheckResult(
            True, "release pull request: the fragment and changelog rules do not apply"
        )

    paths = changed_paths(changed)
    fragment = f"changes/{pull_request}.md"
    problems: list[str] = []
    if CHANGELOG in paths:
        problems.append(
            f"{CHANGELOG} was edited; only the release pull request assembles the changelog. "
            f"Write the release-note line in {fragment} instead"
        )
    if fragment not in paths or fragment not in head_tree:
        problems.append(
            f"{fragment} is missing; every pull request writes its release-note line there "
            "(### Added, ### Changed or ### Fixed) and never edits CHANGELOG.md"
        )
    if problems:
        return CheckResult(False, " ".join(problems))
    return CheckResult(True, f"{fragment} is present and CHANGELOG.md is unchanged")


def check_specs(changed: list[Change]) -> CheckResult:
    paths = changed_paths(changed)
    specs = [path for path in paths if path.startswith(SPECS)]
    if not specs:
        return CheckResult(True, "no openspec/specs/ path is changed")
    if any(path.startswith(ARCHIVE) for path in paths):
        return CheckResult(True, "every spec change rides with an archive")
    joined = ", ".join(specs[:3]) + ("…" if len(specs) > 3 else "")
    return CheckResult(
        False,
        f"{joined}: spec edited by hand; specs are synced by `openspec archive <change>`. "
        "Archive the change in this pull request so the delta reaches openspec/specs/ through "
        "openspec/changes/archive/<YYYY-MM-DD>-<change>/",
    )
