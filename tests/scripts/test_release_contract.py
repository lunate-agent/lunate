from __future__ import annotations

import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
CONTRACT = REPOSITORY_ROOT / "scripts" / "release-contract"


def write_json(value: object) -> str:
    with tempfile.NamedTemporaryFile("w", suffix=".json", delete=False, encoding="utf-8") as handle:
        json.dump(value, handle)
        return handle.name


def run(*arguments: str) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [sys.executable, str(CONTRACT), *arguments],
        capture_output=True,
        text=True,
        check=False,
    )


def run_pulls(pulls: list[dict], tag: str = "v0.4.0") -> subprocess.CompletedProcess[str]:
    path = write_json(pulls)
    try:
        return run("--tag", tag, "--pulls-file", path)
    finally:
        Path(path).unlink(missing_ok=True)


def run_release(release: object, tag: str = "v0.4.0") -> subprocess.CompletedProcess[str]:
    path = write_json(release)
    try:
        return run("--tag", tag, "--release-file", path)
    finally:
        Path(path).unlink(missing_ok=True)


def pull(branch: str, title: str, *, merged: bool = True, number: int = 813) -> dict:
    return {
        "number": number,
        "title": title,
        "merged_at": "2026-11-13T00:00:00Z" if merged else None,
        "head": {"ref": branch},
    }


def release(*assets: str, draft: bool = False, tag: str = "v0.4.0") -> dict:
    return {
        "tagName": tag,
        "isDraft": draft,
        "assets": [{"name": name} for name in assets],
    }


class ReleaseContractTests(unittest.TestCase):
    def test_a_merged_release_branch_passes(self) -> None:
        result = run_pulls([pull("release/0.4.0", "Release 0.4.0")])

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn("merged release pull request", result.stdout)

    def test_a_merged_release_title_passes(self) -> None:
        result = run_pulls([pull("wt/issue-820", "chore(release): 0.4.0")])

        self.assertEqual(0, result.returncode, result.stderr)

    def test_an_unmerged_release_pull_request_fails(self) -> None:
        result = run_pulls([pull("release/0.4.0", "Release 0.4.0", merged=False)])

        self.assertEqual(1, result.returncode)
        self.assertIn("is not merged", result.stderr)
        self.assertIn("merge the release pull request before tagging", result.stderr)

    def test_a_merged_pull_request_without_the_release_signal_fails(self) -> None:
        result = run_pulls([pull("wt/issue-820", "Fix the loop")])

        self.assertEqual(1, result.returncode)
        self.assertIn("is not a release pull request", result.stderr)
        self.assertIn("release/<version>", result.stderr)

    def test_no_associated_pull_request_fails(self) -> None:
        result = run_pulls([])

        self.assertEqual(1, result.returncode)
        self.assertIn("has no associated pull request", result.stderr)

    def test_a_release_with_its_checksums_passes(self) -> None:
        result = run_release(release("SHA256SUMS", "lunate-osx-arm64.tar.gz"))

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn("published release", result.stdout)

    def test_a_draft_release_fails(self) -> None:
        result = run_release(release("SHA256SUMS", draft=True))

        self.assertEqual(1, result.returncode)
        self.assertIn("is still a draft", result.stderr)

    def test_a_release_without_sha256sums_fails(self) -> None:
        result = run_release(release("lunate-osx-arm64.tar.gz"))

        self.assertEqual(1, result.returncode)
        self.assertIn("has no SHA256SUMS asset", result.stderr)

    def test_the_self_test_passes(self) -> None:
        result = run("--self-test")

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn("self-test passed", result.stdout)


if __name__ == "__main__":
    unittest.main()
