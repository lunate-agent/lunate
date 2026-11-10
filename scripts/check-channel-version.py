#!/usr/bin/env python3
"""Check what a published channel serves against the release tag.

The release smoke installs through every channel the docs name and passes what the installed
binary printed here. A stable tag must match the channel exactly: the Scoop bucket sat two
releases behind while every smoke run passed, because the PowerShell check compared an array
instead of one version string. A prerelease stays on the stable channel, so it is reported and
accepted, which is the rule the Homebrew step already carries.
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

STABLE_TAG = re.compile(r"^v(?P<version>\d+\.\d+\.\d+)$")
PRERELEASE_TAG = re.compile(r"^v(?P<version>\d+\.\d+\.\d+-[0-9A-Za-z.-]+)$")
VERSION = re.compile(
    r"(?<![\w.+-])(?P<version>\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?)"
)


def served_version(output: str) -> str | None:
    """The version the binary printed, ignoring a hint on a following line."""
    match = VERSION.search(output)
    return match.group("version") if match else None


def check(channel: str, tag: str, output: str) -> tuple[bool, str]:
    stable = STABLE_TAG.fullmatch(tag)
    prerelease = PRERELEASE_TAG.fullmatch(tag)
    if not stable and not prerelease:
        return False, (
            f"{channel}: {tag!r} is not a release tag "
            "(v<major>.<minor>.<patch>[-<prerelease>])"
        )
    observed = served_version(output)
    if observed is None:
        return False, f"{channel}: the binary printed no version: {output.strip()!r}"
    served = observed.split("+", 1)[0]
    expected = tag[1:]
    if prerelease:
        return True, (
            f"{channel} serves the stable channel; {tag} is a prerelease, "
            f"so {channel} stays at {observed}."
        )
    if served != expected:
        return False, (
            f"{channel} serves {served}, expected {expected}: the stable channel is a release "
            f"behind. Move {channel} to {expected} before the smoke can pass."
        )
    return True, f"{channel} serves {observed}, the tag."


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--channel", required=True, help="Homebrew, Scoop or winget")
    parser.add_argument(
        "--tag", required=True, help="release tag, for example v0.3.0 or v0.3.0-rc.1"
    )
    source = parser.add_mutually_exclusive_group(required=True)
    source.add_argument("--observed", help="what the installed binary printed")
    source.add_argument(
        "--observed-file", type=Path, help="file holding what the installed binary printed"
    )
    args = parser.parse_args()

    if args.observed_file is None:
        output = args.observed
    else:
        try:
            output = args.observed_file.read_text(encoding="utf-8")
        except OSError as error:
            print(
                f"{args.channel}: cannot read {args.observed_file}: {error}", file=sys.stderr
            )
            return 1

    ok, message = check(args.channel, args.tag, output)
    print(message, file=sys.stdout if ok else sys.stderr)
    return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
