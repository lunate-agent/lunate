#!/usr/bin/env python3
import argparse
import re
import sys
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[1]
STATUS = re.compile(
    r"^\s*(?:[-*]\s*)?status\s*:\s*\**\s*([A-Za-z]+)", re.IGNORECASE
)
COMPLETES = re.compile(r"^\s*(?:[-*]\s*)?completes\s*:\s*(.+)$", re.IGNORECASE)
ADR_REFERENCE = re.compile(r"ADR[- ]?(\d{4})", re.IGNORECASE)
PRIMARY_ADR = re.compile(r"^(\d{4})-(?!addendum-).+\.md$")
ALLOWED_STATUSES = {"proposed", "accepted", "deprecated", "superseded"}
HEADER_LINES = 20


def display_path(path: Path) -> str:
    return path.relative_to(REPOSITORY_ROOT).as_posix()


def header(path: Path) -> list[str]:
    return path.read_text(encoding="utf-8").splitlines()[:HEADER_LINES]


def status_of(path: Path) -> str | None:
    for line in header(path):
        match = STATUS.match(line)
        if match is not None:
            return match.group(1).lower()
    return None


def check(directory: Path) -> list[str]:
    records: dict[Path, str] = {}
    diagnostics = []
    for path in sorted(directory.glob("*.md")):
        status = status_of(path)
        if status is None:
            diagnostics.append(f"{display_path(path)}: missing `Status:` line")
            continue
        if status not in ALLOWED_STATUSES:
            allowed = "|".join(sorted(ALLOWED_STATUSES))
            diagnostics.append(
                f"{display_path(path)}: invalid status '{status}' "
                f"(expected {allowed})"
            )
            continue
        records[path] = status

    by_number: dict[int, tuple[Path, str]] = {}
    for path, status in records.items():
        match = PRIMARY_ADR.match(path.name)
        if match is not None:
            by_number[int(match.group(1))] = (path, status)

    for path, status in records.items():
        if status != "accepted":
            continue
        for line in header(path):
            match = COMPLETES.match(line)
            if match is None:
                continue
            for reference in ADR_REFERENCE.findall(match.group(1)):
                target = by_number.get(int(reference))
                if target is None or target[1] != "proposed":
                    continue
                diagnostics.append(
                    f"{display_path(path)} is accepted and completes "
                    f"ADR-{int(reference):04d}, but {display_path(target[0])} "
                    "is still proposed"
                )
    return diagnostics


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Check ADR status records for internal consistency."
    )
    parser.add_argument("--directory", type=Path, default=REPOSITORY_ROOT / "adr")
    args = parser.parse_args()

    diagnostics = check(args.directory)
    if diagnostics:
        for diagnostic in diagnostics:
            print(f"ADR status: {diagnostic}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
