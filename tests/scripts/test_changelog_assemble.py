import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
ASSEMBLER = REPOSITORY_ROOT / "scripts" / "changelog-assemble"

CHANGELOG = """# Changelog

## [Unreleased]

### Fixed

- **Settings survive a second process.** The stores merge instead of replacing (#584).

## [0.3.0] - 2026-11-09

### Fixed

- A released entry keeps its text (#1).
"""


def fragment(heading: str, *bullets: str) -> str:
    return "\n".join([heading, "", *bullets]) + "\n"


def make_tree(fragments: dict[str, str], changelog: str = CHANGELOG) -> Path:
    root = Path(tempfile.mkdtemp())
    (root / "changes").mkdir()
    for name, text in fragments.items():
        (root / "changes" / name).write_text(text, encoding="utf-8", newline="\n")
    (root / "CHANGELOG.md").write_text(changelog, encoding="utf-8", newline="\n")
    return root


def run(root: Path, *arguments: str) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [sys.executable, str(ASSEMBLER), "--root", str(root), *arguments],
        capture_output=True,
        text=True,
        check=False,
    )


class ChangelogAssembleTests(unittest.TestCase):
    def setUp(self) -> None:
        self.trees: list[Path] = []

    def tearDown(self) -> None:
        import shutil

        for tree in self.trees:
            shutil.rmtree(tree, ignore_errors=True)

    def tree(self, fragments: dict[str, str], changelog: str = CHANGELOG) -> Path:
        root = make_tree(fragments, changelog)
        self.trees.append(root)
        return root

    def test_a_missing_fragment_fails_the_check(self) -> None:
        root = self.tree({"1234.md": fragment("### Fixed", "- The fix (#1234).")})

        result = run(root, "--check")

        self.assertEqual(1, result.returncode, result.stdout + result.stderr)
        self.assertIn("changes/1234.md is missing from CHANGELOG.md", result.stderr)

    def test_write_makes_the_check_pass(self) -> None:
        root = self.tree({"1234.md": fragment("### Fixed", "- The fix (#1234).")})

        written = run(root, "--write")
        checked = run(root, "--check")

        self.assertEqual(0, written.returncode, written.stderr)
        self.assertEqual(0, checked.returncode, checked.stderr)
        self.assertIn("The fix (#1234).", (root / "CHANGELOG.md").read_text(encoding="utf-8"))

    def test_write_is_idempotent(self) -> None:
        root = self.tree({"1234.md": fragment("### Fixed", "- The fix (#1234).")})
        run(root, "--write")
        first = (root / "CHANGELOG.md").read_bytes()

        run(root, "--write")
        second = (root / "CHANGELOG.md").read_bytes()

        self.assertEqual(first, second)

    def test_entries_follow_number_order_under_their_heading(self) -> None:
        root = self.tree(
            {
                "1200.md": fragment("### Fixed", "- Later fix (#1200)."),
                "1199.md": fragment("### Fixed", "- Earlier fix (#1199)."),
                "1201.md": fragment("### Added", "- New thing (#1201)."),
            }
        )

        run(root, "--write")
        text = (root / "CHANGELOG.md").read_text(encoding="utf-8")

        self.assertLess(text.index("### Added"), text.index("### Fixed"))
        self.assertLess(text.index("Earlier fix (#1199)"), text.index("Later fix (#1200)"))
        self.assertLess(text.index("### Added"), text.index("New thing (#1201)"))
        self.assertLess(text.index("### Fixed"), text.index("Earlier fix (#1199)"))

    def test_the_entry_written_before_fragments_keeps_its_place_and_text(self) -> None:
        root = self.tree({"1200.md": fragment("### Fixed", "- Later fix (#1200).")})

        run(root, "--write")
        text = (root / "CHANGELOG.md").read_text(encoding="utf-8")

        legacy = "- **Settings survive a second process.** The stores merge instead of replacing (#584)."
        self.assertIn(legacy, text)
        self.assertLess(text.index("<!-- changelog-fragments:end -->"), text.index(legacy))

    def test_released_history_is_not_rewritten(self) -> None:
        root = self.tree({"1200.md": fragment("### Fixed", "- Later fix (#1200).")})
        before = CHANGELOG.split("## [0.3.0]", 1)[1]

        run(root, "--write")
        after = (root / "CHANGELOG.md").read_text(encoding="utf-8").split("## [0.3.0]", 1)[1]

        self.assertEqual(before, after)

    def test_a_fragment_without_a_heading_is_malformed(self) -> None:
        root = self.tree({"1234.md": "- The fix (#1234).\n"})

        result = run(root, "--check")

        self.assertEqual(1, result.returncode)
        self.assertIn("unknown heading", result.stderr)
        self.assertIn("changes/1234.md", result.stderr)

    def test_a_bullet_may_reference_the_issue_number(self) -> None:
        root = self.tree({"801.md": fragment("### Added", "- The checker (#749).")})

        written = run(root, "--write")
        checked = run(root, "--check")

        self.assertEqual(0, written.returncode, written.stderr)
        self.assertEqual(0, checked.returncode, checked.stderr)
        self.assertIn("The checker (#749).", (root / "CHANGELOG.md").read_text(encoding="utf-8"))

    def test_a_bullet_whose_reference_is_not_at_the_end_is_malformed(self) -> None:
        root = self.tree({"1234.md": fragment("### Fixed", "- The fix (#1234) trailing.")})

        result = run(root, "--check")

        self.assertEqual(1, result.returncode)
        self.assertIn("does not end with a reference", result.stderr)

    def test_a_bullet_without_any_reference_is_malformed(self) -> None:
        root = self.tree({"1234.md": fragment("### Fixed", "- The fix.")})

        result = run(root, "--check")

        self.assertEqual(1, result.returncode)
        self.assertIn("does not end with a reference", result.stderr)

    def test_two_fragments_for_one_number_are_reported(self) -> None:
        root = self.tree(
            {
                "1234.md": fragment("### Fixed", "- The fix (#1234)."),
                "01234.md": fragment("### Added", "- Another (#1234)."),
            }
        )

        result = run(root, "--check")

        self.assertEqual(1, result.returncode)
        self.assertIn("two fragments for pull request 1234", result.stderr)
        self.assertIn("changes/1234.md", result.stderr)
        self.assertIn("changes/01234.md", result.stderr)

    def test_an_unknown_heading_is_reported(self) -> None:
        root = self.tree({"1234.md": fragment("### Improved", "- The fix (#1234).")})

        result = run(root, "--check")

        self.assertEqual(1, result.returncode)
        self.assertIn("unknown heading '### Improved'", result.stderr)
        self.assertIn("changes/1234.md", result.stderr)

    def test_write_refuses_a_malformed_tree(self) -> None:
        root = self.tree({"1234.md": fragment("### Improved", "- The fix (#1234).")})
        before = (root / "CHANGELOG.md").read_text(encoding="utf-8")

        result = run(root, "--write")

        self.assertEqual(1, result.returncode)
        self.assertEqual(before, (root / "CHANGELOG.md").read_text(encoding="utf-8"))

    def test_no_fragments_and_no_markers_pass(self) -> None:
        root = self.tree({})

        self.assertEqual(0, run(root, "--check").returncode)

    def test_a_promoted_release_with_no_fragments_passes(self) -> None:
        promoted = CHANGELOG.replace("## [Unreleased]", "## [0.4.0] - 2026-11-12")
        root = self.tree({}, promoted)

        self.assertEqual(0, run(root, "--check").returncode)

    def test_fragments_without_an_unreleased_section_fail(self) -> None:
        promoted = CHANGELOG.replace("## [Unreleased]", "## [0.4.0] - 2026-11-12")
        root = self.tree({"1234.md": fragment("### Fixed", "- The fix (#1234).")}, promoted)

        result = run(root, "--check")

        self.assertEqual(1, result.returncode)
        self.assertIn("no '## [Unreleased]' section", result.stderr)

    def test_a_consumed_fragment_disappears_from_the_section(self) -> None:
        root = self.tree({"1234.md": fragment("### Fixed", "- The fix (#1234).")})
        run(root, "--write")

        (root / "changes" / "1234.md").unlink()
        consumed = run(root, "--write")
        checked = run(root, "--check")

        self.assertEqual(0, consumed.returncode, consumed.stderr)
        self.assertEqual(0, checked.returncode, checked.stderr)
        text = (root / "CHANGELOG.md").read_text(encoding="utf-8")
        self.assertNotIn("The fix (#1234).", text)
        self.assertNotIn("<!-- changelog-fragments:start -->", text)


if __name__ == "__main__":
    unittest.main()
