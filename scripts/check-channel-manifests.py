#!/usr/bin/env python3
"""Check the channel manifests against the newest published stable release.

The manifests carry the archives' SHA-256 values, so they cannot be updated until the
release assets exist. A release pull request therefore keeps the manifests one release
behind; after the tag, the channel pull request moves them to the new release and this
check proves the values against that release's SHA256SUMS.

"The newest published stable release" is defined once, in `scripts/published_release.py`, and
shared with `scripts/docs-sync`: a stable tag whose release has not attached its `SHA256SUMS`
is skipped, because a tag with no release is not a release.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
import tempfile
from pathlib import Path

from published_release import (
    ReleaseError,
    ReleaseMissing,
    ReleaseUnavailable,
    STABLE_TAG,
    fetch_sums,
    newest_published_tag,
    parse_sums,
    stable_tags,
)

DOWNLOAD = re.compile(r"/download/(v[^/]+)/([^/]+)$")


def _read(path: Path) -> str:
    try:
        return path.read_text(encoding="utf-8")
    except OSError as error:
        raise ValueError(f"cannot read {path}: {error}") from error


def _asset_version(url: str, manifest: str) -> str:
    match = DOWNLOAD.search(url)
    if not match:
        raise ValueError(f"{manifest}: URL does not name a release asset: {url}")
    return match.group(1)


def _check_asset(
    manifest: str,
    url: str,
    digest: str,
    tag: str,
    sums: dict[str, str],
    errors: list[str],
) -> None:
    try:
        version = _asset_version(url, manifest)
    except ValueError as error:
        errors.append(str(error))
        return
    if version != tag:
        errors.append(f"{manifest}: asset points at {version}, expected {tag}")
        return
    name = DOWNLOAD.search(url).group(2)  # type: ignore[union-attr]
    expected = sums.get(name)
    if expected is None:
        errors.append(f"{manifest}: {name} is not in SHA256SUMS for {tag}")
    elif digest.lower() != expected:
        errors.append(
            f"{manifest}: {name} sha256 is {digest.lower()}, SHA256SUMS says {expected}"
        )


def check_homebrew(root: Path, tag: str, sums: dict[str, str], errors: list[str]) -> None:
    manifest = "packaging/homebrew/lunate.rb"
    text = _read(root / manifest)
    urls = re.findall(r'^\s*url\s+"([^"]+)"\s*$', text, re.MULTILINE)
    digests = re.findall(r'^\s*sha256\s+"([0-9a-fA-F]{64})"\s*$', text, re.MULTILINE)
    if not urls:
        errors.append(f"{manifest}: no release url")
        return
    if len(urls) != len(digests):
        errors.append(f"{manifest}: {len(urls)} urls but {len(digests)} sha256 values")
        return
    for url, digest in zip(urls, digests):
        _check_asset(manifest, url, digest, tag, sums, errors)


def check_scoop(root: Path, tag: str, sums: dict[str, str], errors: list[str]) -> None:
    manifest = "packaging/scoop/lunate.json"
    try:
        data = json.loads(_read(root / manifest))
    except json.JSONDecodeError as error:
        errors.append(f"{manifest}: invalid JSON: {error}")
        return
    version = data.get("version")
    if version != tag.removeprefix("v"):
        errors.append(f"{manifest}: version {version!r}, expected {tag.removeprefix('v')}")
    architecture = data.get("architecture")
    if not isinstance(architecture, dict) or not architecture:
        errors.append(f"{manifest}: no architecture entries")
        return
    for name, entry in architecture.items():
        if not isinstance(entry, dict):
            errors.append(f"{manifest}: architecture {name} is not an object")
            continue
        url, digest = entry.get("url"), entry.get("hash")
        if not isinstance(url, str) or not isinstance(digest, str):
            errors.append(f"{manifest}: architecture {name} needs url and hash")
            continue
        _check_asset(f"{manifest} ({name})", url, digest, tag, sums, errors)


def check_winget(root: Path, tag: str, sums: dict[str, str], errors: list[str]) -> None:
    manifest = "packaging/winget/wdaniel1993.lunate.installer.yaml"
    text = _read(root / manifest)
    version = re.search(r"^PackageVersion:\s*(\S+)\s*$", text, re.MULTILINE)
    if not version:
        errors.append(f"{manifest}: no PackageVersion")
    elif version.group(1) != tag.removeprefix("v"):
        errors.append(f"{manifest}: version {version.group(1)!r}, expected {tag.removeprefix('v')}")
    urls = re.findall(r"^\s*InstallerUrl:\s*(\S+)\s*$", text, re.MULTILINE)
    digests = re.findall(r"^\s*InstallerSha256:\s*([0-9a-fA-F]{64})\s*$", text, re.MULTILINE)
    if not urls:
        errors.append(f"{manifest}: no InstallerUrl")
        return
    if len(urls) != len(digests):
        errors.append(f"{manifest}: {len(urls)} installer URLs but {len(digests)} hashes")
        return
    for url, digest in zip(urls, digests):
        _check_asset(manifest, url, digest, tag, sums, errors)


def check(root: Path, tag: str, sums: dict[str, str]) -> list[str]:
    errors: list[str] = []
    check_homebrew(root, tag, sums, errors)
    check_scoop(root, tag, sums, errors)
    check_winget(root, tag, sums, errors)
    return errors


def _manifest(version: str, digest: str) -> dict[str, str]:
    win = "a" * 64 if digest == "a" * 64 else digest
    return {
        "packaging/homebrew/lunate.rb": f"""class Lunate < Formula
  on_macos do
    url "https://github.com/lunate-agent/lunate/releases/download/v{version}/lunate-osx-arm64.tar.gz"
    sha256 "{digest}"
  end

  on_linux do
    url "https://github.com/lunate-agent/lunate/releases/download/v{version}/lunate-linux-x64.tar.gz"
    sha256 "{digest}"
  end
end
""",
        "packaging/scoop/lunate.json": json.dumps(
            {
                "version": version,
                "architecture": {
                    "64bit": {
                        "url": f"https://github.com/lunate-agent/lunate/releases/download/v{version}/lunate-win-x64.zip",
                        "hash": win,
                    }
                },
            }
        ),
        "packaging/winget/wdaniel1993.lunate.installer.yaml": f"""PackageIdentifier: wdaniel1993.lunate
PackageVersion: {version}
Installers:
  - Architecture: x64
    InstallerUrl: https://github.com/lunate-agent/lunate/releases/download/v{version}/lunate-win-x64.zip
    InstallerSha256: {win.upper()}
ManifestType: installer
""",
    }


def _write_manifests(root: Path, version: str, digest: str) -> None:
    for relative, content in _manifest(version, digest).items():
        path = root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding="utf-8")


def _write_sums(root: Path, version: str, digest: str) -> Path:
    path = root / "SHA256SUMS"
    path.write_text(
        f"{digest}  lunate-osx-arm64.tar.gz\n"
        f"{digest}  lunate-linux-x64.tar.gz\n"
        f"{'a' * 64 if digest == 'a' * 64 else digest}  lunate-win-x64.zip\n",
        encoding="utf-8",
    )
    return path


def self_test() -> None:
    with tempfile.TemporaryDirectory(prefix="lunate-manifest-guard-") as temp:
        root = Path(temp)
        # Release PR: manifests still point at the previous stable release.
        _write_manifests(root, "0.2.1", "a" * 64)
        _write_sums(root, "0.2.1", "a" * 64)
        assert not check(root, "v0.2.1", parse_sums((root / "SHA256SUMS").read_text()))

        # Channel PR: manifests point at the new stable release and match its sums.
        _write_manifests(root, "0.3.0", "b" * 64)
        _write_sums(root, "0.3.0", "b" * 64)
        assert not check(root, "v0.3.0", parse_sums((root / "SHA256SUMS").read_text()))

        # A tag exists but its release is not published yet: the guard steps past it to the previous
        # published release, which is what the manifests still name during a release run.
        def fetch_only_v021(tag: str) -> dict[str, str]:
            if tag != "v0.2.1":
                raise ReleaseMissing(f"{tag} has no release asset")
            return {"lunate-osx-arm64.tar.gz": "a" * 64}

        assert newest_published_tag(["v0.3.0", "v0.2.1"], fetch_only_v021) == "v0.2.1"

        # And when the new release does exist, it is the one the manifests are held to.
        assert newest_published_tag(["v0.3.0", "v0.2.1"], lambda tag: {"x": "y"}) == "v0.3.0"

        # Nothing published at all is a failure, not a silent pass.
        try:
            newest_published_tag(["v0.3.0"], fetch_only_v021)
            raise AssertionError("an unpublished tag was accepted")
        except ReleaseError:
            pass

        # An unreachable host is not an unpublished release: stepping back would silently pass
        # against stale manifests.
        def fetch_unavailable(tag: str) -> dict[str, str]:
            raise ReleaseUnavailable(f"cannot fetch {tag}")

        try:
            newest_published_tag(["v0.3.0"], fetch_unavailable)
            raise AssertionError("an unreachable release was skipped")
        except ReleaseUnavailable:
            pass

        # Wrong hash: the same channel PR must be rejected.
        _write_sums(root, "0.3.0", "c" * 64)
        errors = check(root, "v0.3.0", parse_sums((root / "SHA256SUMS").read_text()))
        assert errors and any("SHA256SUMS says" in error for error in errors), errors
    print("channel manifest guard: self-test OK (release, channel, mismatch)")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path("."))
    parser.add_argument(
        "--tag", help="stable release tag; defaults to the newest published stable release"
    )
    parser.add_argument("--sums", type=Path, help="local SHA256SUMS fixture (tests/offline)")
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()

    if args.self_test:
        self_test()
        return 0

    try:
        tag = args.tag or newest_published_tag(stable_tags(args.root), fetch_sums)
        if not STABLE_TAG.fullmatch(tag):
            raise ValueError(f"{tag!r} is not a stable release tag")
        sums = parse_sums(args.sums.read_text()) if args.sums else fetch_sums(tag)
        errors = check(args.root, tag, sums)
    except (ValueError, OSError) as error:
        print(f"channel manifests: FAIL: {error}", file=sys.stderr)
        return 1
    if errors:
        for error in errors:
            print(f"channel manifests: FAIL: {error}", file=sys.stderr)
        return 1
    print(f"channel manifests: OK ({tag})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
