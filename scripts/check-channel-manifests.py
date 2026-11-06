#!/usr/bin/env python3
"""Check the channel manifests against the newest published stable release.

The manifests carry the archives' SHA-256 values, so they cannot be updated until the
release assets exist. A release pull request therefore keeps the manifests one release
behind; after the tag, the channel pull request moves them to the new release and this
check proves the values against that release's SHA256SUMS.
"""

from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
import tempfile
import urllib.error
import urllib.request
from pathlib import Path

STABLE_TAG = re.compile(r"^v(\d+)\.(\d+)\.(\d+)$")
SHA256 = re.compile(r"^[0-9a-fA-F]{64}$")
DOWNLOAD = re.compile(r"/download/(v[^/]+)/([^/]+)$")
SUMS_URL = "https://github.com/wdaniel1993/lunate/releases/download/{tag}/SHA256SUMS"


def _read(path: Path) -> str:
    try:
        return path.read_text(encoding="utf-8")
    except OSError as error:
        raise ValueError(f"cannot read {path}: {error}") from error


def latest_stable_tag(root: Path) -> str:
    """The newest non-prerelease tag, sorted by version (not by tag string)."""
    try:
        result = subprocess.run(
            ["git", "-C", str(root), "tag", "--list", "v*", "--sort=-v:refname"],
            check=True,
            capture_output=True,
            text=True,
        )
    except (OSError, subprocess.CalledProcessError) as error:
        raise ValueError(f"cannot list tags in {root}: {error}") from error
    for tag in result.stdout.splitlines():
        if STABLE_TAG.fullmatch(tag):
            return tag
    raise ValueError(f"no stable release tag (v<major>.<minor>.<patch>) in {root}")


def parse_sums(text: str) -> dict[str, str]:
    sums: dict[str, str] = {}
    for number, raw in enumerate(text.splitlines(), start=1):
        line = raw.strip()
        if not line or line.startswith("#"):
            continue
        parts = line.split()
        if len(parts) != 2 or not SHA256.fullmatch(parts[0]):
            raise ValueError(f"SHA256SUMS line {number} is malformed: {raw!r}")
        digest, name = parts[0].lower(), parts[1]
        if name in sums:
            raise ValueError(f"SHA256SUMS names {name!r} twice")
        sums[name] = digest
    if not sums:
        raise ValueError("SHA256SUMS is empty")
    return sums


def fetch_sums(tag: str) -> dict[str, str]:
    url = SUMS_URL.format(tag=tag)
    try:
        with urllib.request.urlopen(url, timeout=30) as response:
            body = response.read().decode("utf-8")
    except (OSError, urllib.error.URLError) as error:
        raise ValueError(f"cannot fetch {url}: {error}") from error
    return parse_sums(body)


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
    url "https://github.com/wdaniel1993/lunate/releases/download/v{version}/lunate-osx-arm64.tar.gz"
    sha256 "{digest}"
  end

  on_linux do
    url "https://github.com/wdaniel1993/lunate/releases/download/v{version}/lunate-linux-x64.tar.gz"
    sha256 "{digest}"
  end
end
""",
        "packaging/scoop/lunate.json": json.dumps(
            {
                "version": version,
                "architecture": {
                    "64bit": {
                        "url": f"https://github.com/wdaniel1993/lunate/releases/download/v{version}/lunate-win-x64.zip",
                        "hash": win,
                    }
                },
            }
        ),
        "packaging/winget/wdaniel1993.lunate.installer.yaml": f"""PackageIdentifier: wdaniel1993.lunate
PackageVersion: {version}
Installers:
  - Architecture: x64
    InstallerUrl: https://github.com/wdaniel1993/lunate/releases/download/v{version}/lunate-win-x64.zip
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

        # Wrong hash: the same channel PR must be rejected.
        _write_sums(root, "0.3.0", "c" * 64)
        errors = check(root, "v0.3.0", parse_sums((root / "SHA256SUMS").read_text()))
        assert errors and any("SHA256SUMS says" in error for error in errors), errors
    print("channel manifest guard: self-test OK (release, channel, mismatch)")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path("."))
    parser.add_argument("--tag", help="stable release tag; defaults to the newest stable tag")
    parser.add_argument("--sums", type=Path, help="local SHA256SUMS fixture (tests/offline)")
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()

    if args.self_test:
        self_test()
        return 0

    try:
        tag = args.tag or latest_stable_tag(args.root)
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
