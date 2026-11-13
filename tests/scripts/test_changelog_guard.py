from __future__ import annotations

import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
GUARD = REPOSITORY_ROOT / "scripts" / "changelog-guard"


def run(paths: list[str], number: int = 1234, branch: str = "wt/issue-794", title: str = "feat: x"):
    with tempfile.NamedTemporaryFile("w", suffix=".txt", delete=False, encoding="utf-8") as handle:
        handle.write("\n".join(paths) + "\n")
        path = handle.name
    try:
        return subprocess.run(
            [
                sys.executable,
                str(GUARD),
                "--paths-file",
                path,
                "--pull-request",
                str(number),
                "--branch",
                branch,
                "--title",
                title,
            ],
            capture_output=True,
            text=True,
            check=False,
        )
    finally:
        Path(path).unlink(missing_ok=True)


class ChangelogGuardTests(unittest.TestCase):
    def test_a_fragment_only_pull_request_passes(self) -> None:
        result = run(["A\tchanges/1234.md"])

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn("the changelog rule holds", result.stdout)

    def test_a_changelog_edit_names_the_fragment_path(self) -> None:
        result = run(["A\tchanges/1234.md", "M\tCHANGELOG.md"])

        self.assertEqual(1, result.returncode)
        self.assertIn("CHANGELOG.md was edited", result.stderr)
        self.assertIn("changes/1234.md", result.stderr)

    def test_a_release_branch_may_edit(self) -> None:
        result = run(["M\tCHANGELOG.md"], branch="release/0.4.0")

        self.assertEqual(0, result.returncode, result.stderr)

    def test_a_release_title_may_edit(self) -> None:
        result = run(["M\tCHANGELOG.md"], title="chore(release): 0.4.0")

        self.assertEqual(0, result.returncode, result.stderr)

    def test_a_rename_out_of_the_changelog_is_caught(self) -> None:
        result = run(["R100\tCHANGELOG.md\told-changelog.md"])

        self.assertEqual(1, result.returncode)
        self.assertIn("changes/1234.md", result.stderr)

    def test_unrelated_prose_passes(self) -> None:
        result = run(["M\tdocs/guide.md"])

        self.assertEqual(0, result.returncode, result.stderr)

    def test_the_decision_self_test_passes(self) -> None:
        result = subprocess.run(
            [sys.executable, str(GUARD), "--self-test"],
            capture_output=True,
            text=True,
            check=False,
        )

        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertIn("self-test passed", result.stdout)


if __name__ == "__main__":
    unittest.main()
