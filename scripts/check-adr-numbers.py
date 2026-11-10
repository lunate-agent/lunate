#!/usr/bin/env python3
import argparse
import re
import sys
from collections import defaultdict
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[1]
# Any numbered ADR file occupies its number, addenda included: an addendum is a
# decision record, so a number only used by addenda is not a gap.
NUMBERED_ADR = re.compile(r"^(\d{4})-.+\.md$")
# Duplicates are checked on primaries only: several addenda sharing their parent
# ADR's number is the documented convention (ADR-0017 has several).
PRIMARY_ADR = re.compile(r"^(\d{4})-(?!addendum-).+\.md$")


def display_path(path: Path) -> str:
    return path.relative_to(REPOSITORY_ROOT).as_posix()


def check(directory: Path) -> list[str]:
    primaries: dict[int, list[Path]] = defaultdict(list)
    occupied: dict[int, list[Path]] = defaultdict(list)
    for path in sorted(directory.iterdir()):
        match = NUMBERED_ADR.match(path.name)
        if not path.is_file() or match is None:
            continue
        number = int(match.group(1))
        occupied[number].append(path)
        if PRIMARY_ADR.match(path.name) is not None:
            primaries[number].append(path)

    diagnostics = []
    for number, paths in sorted(primaries.items()):
        if len(paths) > 1:
            names = ", ".join(display_path(path) for path in paths)
            diagnostics.append(f"duplicate ADR-{number:04d}: {names}")

    ordered_numbers = sorted(occupied)
    if ordered_numbers and ordered_numbers[0] > 1:
        first_number = ordered_numbers[0]
        first_path = display_path(occupied[first_number][0])
        diagnostics.append(f"missing ADR-0001–{first_number - 1:04d} before {first_path}")
    for previous, current in zip(ordered_numbers, ordered_numbers[1:]):
        if current > previous + 1:
            previous_path = display_path(occupied[previous][-1])
            current_path = display_path(occupied[current][0])
            missing = ", ".join(
                f"ADR-{number:04d}" for number in range(previous + 1, current)
            )
            diagnostics.append(
                f"missing {missing} between {previous_path} and {current_path}"
            )

    if diagnostics:
        diagnostics.append(f"next free number: {max(occupied) + 1:04d}")
    return diagnostics


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Check ADR numbering: every number occupied, primaries unique."
    )
    parser.add_argument("--directory", type=Path, default=REPOSITORY_ROOT / "adr")
    args = parser.parse_args()

    diagnostics = check(args.directory)
    if diagnostics:
        for diagnostic in diagnostics:
            print(f"ADR numbering: {diagnostic}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
