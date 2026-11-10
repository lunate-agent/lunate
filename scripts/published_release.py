#!/usr/bin/env python3
"""The one definition of the newest published stable release.

A stable tag is cut before the release workflow attaches its assets, so for the minutes between
the tag push and the finished release the newest stable tag has no release. The release exists
exactly when its `SHA256SUMS` asset resolves, so that file is the proof: a tag whose `SHA256SUMS`
is missing is a tag, not a release.

`scripts/docs-sync` and `scripts/check-channel-manifests.py` both ask this module instead of each
carrying its own notion of "current", so a failed release cannot make the README advertise a
release that does not exist or the manifest guard demand one.

An unreachable release host is not an unpublished release: only a 404/410 steps past a tag. Any
other failure is raised, because reading an outage as "the older release is still current" would
silently pass with a stale README or stale manifests.
"""

from __future__ import annotations

import re
import subprocess
import sys
import urllib.error
import urllib.request
from collections.abc import Callable, Iterable
from pathlib import Path

STABLE_TAG = re.compile(r"^v?(\d+)\.(\d+)\.(\d+)$")
SHA256 = re.compile(r"^[0-9a-fA-F]{64}$")
SUMS_URL = "https://github.com/lunate-agent/lunate/releases/download/{tag}/SHA256SUMS"


class ReleaseError(ValueError):
    """No stable tag, no published release, or unreadable release assets."""


class ReleaseMissing(ReleaseError):
    """The tag exists but its release has no SHA256SUMS asset."""


class ReleaseUnavailable(ReleaseError):
    """The release could not be checked (network, server or malformed assets)."""


def parse_stable_tags(lines: Iterable[str]) -> list[str]:
    """The stable tags from `git tag --sort=-v:refname` output, newest first.

    Prerelease tags (`v0.3.0-rc.1`) and unrelated tags are skipped, so an rc can never advertise
    a stable version.
    """
    tags = [line.strip() for line in lines if STABLE_TAG.fullmatch(line.strip())]
    if not tags:
        raise ReleaseError("no stable release tag (v<major>.<minor>.<patch>)")
    return tags


def stable_tags(root: Path) -> list[str]:
    """The repository's stable tags, newest first by version."""
    try:
        result = subprocess.run(
            ["git", "-C", str(root), "tag", "--list", "--sort=-v:refname"],
            check=True,
            capture_output=True,
            text=True,
        )
    except FileNotFoundError as error:
        raise ReleaseUnavailable(
            "git is required to read the release tags for the version region"
        ) from error
    except subprocess.CalledProcessError as error:
        raise ReleaseUnavailable(
            f"git tag failed: {error.stderr.strip() or f'no repository under {root}'}"
        ) from error
    return parse_stable_tags(result.stdout.splitlines())


def tag_version(tag: str) -> str:
    """The `X.Y.Z` version a stable tag names, without its `v` prefix."""
    match = STABLE_TAG.fullmatch(tag)
    if not match:
        raise ReleaseError(
            f"{tag!r} is not a stable release tag (v<major>.<minor>.<patch>)"
        )
    return ".".join(match.groups())


def parse_sums(text: str) -> dict[str, str]:
    """The asset name → SHA-256 map a release's SHA256SUMS carries."""
    sums: dict[str, str] = {}
    for number, raw in enumerate(text.splitlines(), start=1):
        line = raw.strip()
        if not line or line.startswith("#"):
            continue
        parts = line.split()
        if len(parts) != 2 or not SHA256.fullmatch(parts[0]):
            raise ReleaseError(f"SHA256SUMS line {number} is malformed: {raw!r}")
        digest, name = parts[0].lower(), parts[1]
        if name in sums:
            raise ReleaseError(f"SHA256SUMS names {name!r} twice")
        sums[name] = digest
    if not sums:
        raise ReleaseError("SHA256SUMS is empty")
    return sums


def fetch_sums(tag: str) -> dict[str, str]:
    """Download and parse the tag's SHA256SUMS; a missing asset is a missing release."""
    url = SUMS_URL.format(tag=tag)
    try:
        with urllib.request.urlopen(url, timeout=30) as response:
            body = response.read().decode("utf-8")
    except urllib.error.HTTPError as error:
        if error.code in (404, 410):
            raise ReleaseMissing(
                f"{tag} has no SHA256SUMS asset at {url} (HTTP {error.code})"
            ) from error
        raise ReleaseUnavailable(f"cannot fetch {url}: HTTP {error.code}") from error
    except (OSError, urllib.error.URLError) as error:
        raise ReleaseUnavailable(f"cannot fetch {url}: {error}") from error
    return parse_sums(body)


def newest_published_tag(
    tags: Iterable[str], fetch: Callable[[str], dict[str, str]] | None = None
) -> str:
    """The newest stable tag whose release assets exist.

    A tag whose SHA256SUMS is absent is skipped; the first tag that resolves is the current
    release. A tag whose release cannot be checked is an error, not a reason to fall back.
    """
    fetch = fetch or fetch_sums
    tags = list(tags)
    missing: ReleaseMissing | None = None
    for tag in tags:
        try:
            fetch(tag)
            return tag
        except ReleaseMissing as error:
            missing = error
    raise ReleaseError(
        f"no published stable release among {', '.join(tags)}: {missing}"
    )


def published_release(
    root: Path, fetch: Callable[[str], dict[str, str]] | None = None
) -> str:
    """The newest published stable release tag in the repository."""
    return newest_published_tag(stable_tags(root), fetch)


def published_version(
    root: Path, fetch: Callable[[str], dict[str, str]] | None = None
) -> str:
    """The newest published stable release as `X.Y.Z`, the form the README carries."""
    return tag_version(published_release(root, fetch))


if __name__ == "__main__":
    try:
        print(published_version(Path(sys.argv[1] if len(sys.argv) > 1 else ".")))
    except ReleaseError as error:
        print(f"published release: FAIL: {error}", file=sys.stderr)
        raise SystemExit(1)
