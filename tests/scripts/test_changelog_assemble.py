from __future__ import annotations

import datetime
import importlib.util
import shutil
import subprocess
import sys
import tempfile
import unittest
from importlib.machinery import SourceFileLoader
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


def write(path: Path, text: str) -> None:
    """Write UTF-8 with LF on every platform, without needing Python 3.10's write_text."""
    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(text)


PROPS = """<Project>
    <PropertyGroup>
        <Version>{version}</Version>
    </PropertyGroup>
</Project>
"""


def make_tree(
    fragments: dict[str, str], changelog: str = CHANGELOG, version: str = "0.4.0"
) -> Path:
    root = Path(tempfile.mkdtemp())
    (root / "changes").mkdir()
    for name, text in fragments.items():
        write(root / "changes" / name, text)
    write(root / "CHANGELOG.md", changelog)
    write(root / "Directory.Build.props", PROPS.format(version=version))
    return root


# The shapes the assembler cannot read reliably: it would drop the first block's bullet, carry a
# bullet that no heading owns, or never look at the second unreleased section.
DOUBLE_ADDED = """# Changelog

## [Unreleased]

### Added

- First hand-written entry (#1).

### Added

- Second hand-written entry (#2).

## [0.3.0] - 2026-11-09

### Fixed

- A released entry keeps its text (#3).
"""

STRAY_ENTRY = """# Changelog

## [Unreleased]

- A stray entry (#1).

### Fixed

- A real entry (#2).

## [0.3.0] - 2026-11-09

### Fixed

- A released entry keeps its text (#3).
"""

DOUBLE_UNRELEASED = """# Changelog

## [Unreleased]

### Added

- A first-section entry (#1).

## [Unreleased]

### Fixed

- An entry in the second section (#2).

## [0.3.0] - 2026-11-09

### Fixed

- A released entry keeps its text (#3).
"""

CLEAN = """# Changelog

## [Unreleased]

### Added

- A pending addition (#1).

### Fixed

- A pending fix (#2).

## [0.3.0] - 2026-11-09

The 0.3.0 release, in the prose a released section keeps above its headings.

### Added

- A released addition (#3).

### Fixed

- A released entry keeps its text (#4).
"""


def load_assembler():
    """Load `scripts/changelog-assemble` as a module; it has no `.py` name to import."""
    loader = SourceFileLoader("changelog_assemble", str(ASSEMBLER))
    module = importlib.util.module_from_spec(importlib.util.spec_from_loader(loader.name, loader))
    loader.exec_module(module)
    return module


def released_changelog(heading: str = "## [0.4.0] - 2026-11-14") -> str:
    return (
        "# Changelog\n\n## [Unreleased]\n\n"
        f"{heading}\n\n### Fixed\n\n- A released entry (#1).\n"
    )


def body_of(text: str, heading: str) -> str:
    """Return the trimmed lines between `heading` and the next `## ` heading."""
    lines = text.splitlines()
    index = lines.index(heading)
    end = len(lines)
    for position in range(index + 1, len(lines)):
        if lines[position].startswith("## "):
            end = position
            break
    return "\n".join(lines[index + 1 : end]).strip()


def snapshot(root: Path) -> dict[str, bytes]:
    """Every file the release touches, for the refusals that must write nothing."""
    return {
        path.relative_to(root).as_posix(): path.read_bytes()
        for path in [root / "CHANGELOG.md", root / "Directory.Build.props"]
        + sorted((root / "changes").glob("*.md"))
    }


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
        for tree in self.trees:
            shutil.rmtree(tree, ignore_errors=True)

    def tree(
        self, fragments: dict[str, str], changelog: str = CHANGELOG, version: str = "0.4.0"
    ) -> Path:
        root = make_tree(fragments, changelog, version)
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

    def test_a_bullet_may_reference_several_pull_requests(self) -> None:
        root = self.tree({"799.md": fragment("### Added", "- The wizard (#681, #682).")})

        written = run(root, "--write")
        checked = run(root, "--check")

        self.assertEqual(0, written.returncode, written.stderr)
        self.assertEqual(0, checked.returncode, checked.stderr)
        self.assertIn(
            "The wizard (#681, #682).", (root / "CHANGELOG.md").read_text(encoding="utf-8")
        )

    def test_a_bullet_with_trailing_text_is_malformed(self) -> None:
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

    def test_a_repeated_heading_in_one_section_is_refused_by_name(self) -> None:
        root = self.tree({}, changelog=DOUBLE_ADDED)
        before = snapshot(root)

        checked = run(root, "--check")
        written = run(root, "--write")

        for result in (checked, written):
            self.assertEqual(1, result.returncode, result.stdout + result.stderr)
            self.assertIn("CHANGELOG.md:9:", result.stderr)
            self.assertIn("'### Added'", result.stderr)
            self.assertIn("line 5", result.stderr)
        self.assertEqual(before, snapshot(root))
        self.assertIn("First hand-written entry (#1).", before["CHANGELOG.md"].decode("utf-8"))

    def test_a_repeated_heading_in_a_released_section_is_refused_by_name(self) -> None:
        released = CLEAN.replace("### Fixed\n\n- A released entry", "### Added\n\n- A released entry")
        root = self.tree({}, changelog=released)

        result = run(root, "--check")

        self.assertEqual(1, result.returncode, result.stdout + result.stderr)
        self.assertIn("CHANGELOG.md:21:", result.stderr)
        self.assertIn("'### Added'", result.stderr)
        self.assertIn("line 17", result.stderr)
        self.assertIn("'## [0.3.0] - 2026-11-09'", result.stderr)

    def test_a_second_unreleased_section_is_refused_by_name(self) -> None:
        root = self.tree({}, changelog=DOUBLE_UNRELEASED)
        before = snapshot(root)

        checked = run(root, "--check")
        released = run(root, "--release", "0.5.0", "--date", "2026-12-01")

        for result in (checked, released):
            self.assertEqual(1, result.returncode, result.stdout + result.stderr)
            self.assertIn("CHANGELOG.md:9:", result.stderr)
            self.assertIn("'## [Unreleased]'", result.stderr)
            self.assertIn("line 3", result.stderr)
        self.assertEqual(before, snapshot(root))

    def test_text_outside_a_heading_in_the_unreleased_section_is_refused_by_name(self) -> None:
        root = self.tree({}, changelog=STRAY_ENTRY)
        before = snapshot(root)

        checked = run(root, "--check")
        written = run(root, "--write")

        for result in (checked, written):
            self.assertEqual(1, result.returncode, result.stdout + result.stderr)
            self.assertIn("CHANGELOG.md:5:", result.stderr)
            self.assertIn("outside any '###' heading", result.stderr)
            self.assertIn("'- A stray entry (#1).'", result.stderr)
        self.assertEqual(before, snapshot(root))

    def test_a_clean_changelog_passes(self) -> None:
        root = self.tree({}, changelog=CLEAN)

        checked = run(root, "--check")
        written = run(root, "--write")

        self.assertEqual(0, checked.returncode, checked.stdout + checked.stderr)
        self.assertEqual(0, written.returncode, written.stdout + written.stderr)
        self.assertEqual(CLEAN, (root / "CHANGELOG.md").read_text(encoding="utf-8"))

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

    def test_the_repository_tree_assembles(self) -> None:
        """The fragments this repository actually carries are grammatical and assemble."""
        root = self.tree({})
        shutil.copyfile(REPOSITORY_ROOT / "CHANGELOG.md", root / "CHANGELOG.md")
        fragments = sorted((REPOSITORY_ROOT / "changes").glob("*.md"))
        for path in fragments:
            shutil.copyfile(path, root / "changes" / path.name)
        self.assertTrue(fragments, "the repository has no fragments to assemble")

        written = run(root, "--write")
        checked = run(root, "--check")

        self.assertEqual(0, written.returncode, written.stderr)
        self.assertEqual(0, checked.returncode, written.stderr + checked.stderr)
        assembled = (root / "CHANGELOG.md").read_bytes()
        self.assertEqual(
            (REPOSITORY_ROOT / "CHANGELOG.md").read_text(encoding="utf-8").split("## [0.3.0]")[1],
            assembled.decode("utf-8").split("## [0.3.0]")[1],
        )
        run(root, "--write")
        self.assertEqual(assembled, (root / "CHANGELOG.md").read_bytes())

    def test_release_does_the_whole_edit(self) -> None:
        root = self.tree(
            {
                "1234.md": fragment("### Fixed", "- The fix (#1234)."),
                "1235.md": fragment("### Added", "- The feature (#1235)."),
            }
        )
        released = CHANGELOG.split("## [0.3.0]", 1)[1]

        result = run(root, "--release", "0.5.0", "--date", "2026-12-01")

        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        text = (root / "CHANGELOG.md").read_text(encoding="utf-8")
        self.assertIn("## [0.5.0] - 2026-12-01", text)
        self.assertLess(text.index("## [Unreleased]"), text.index("## [0.5.0] - 2026-12-01"))
        self.assertEqual("", body_of(text, "## [Unreleased]"))
        self.assertLess(text.index("### Added"), text.index("The feature (#1235)."))
        self.assertLess(text.index("### Fixed"), text.index("The fix (#1234)."))
        self.assertNotIn("changelog-fragments", text)
        self.assertEqual(released, text.split("## [0.3.0]", 1)[1])
        props = (root / "Directory.Build.props").read_text(encoding="utf-8")
        self.assertIn("<Version>0.5.0</Version>", props)
        self.assertFalse(list((root / "changes").glob("*.md")))
        self.assertEqual(
            {"CHANGELOG.md", "Directory.Build.props"},
            {path.name for path in root.iterdir() if path.is_file()},
        )
        self.assertEqual(0, run(root, "--check").returncode)

    def test_release_accepts_a_prerelease(self) -> None:
        root = self.tree({"1234.md": fragment("### Fixed", "- The fix (#1234).")})

        result = run(root, "--release", "0.5.0-rc.1", "--date", "2026-12-01")

        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        text = (root / "CHANGELOG.md").read_text(encoding="utf-8")
        self.assertIn("## [0.5.0-rc.1] - 2026-12-01", text)
        props = (root / "Directory.Build.props").read_text(encoding="utf-8")
        self.assertIn("<Version>0.5.0-rc.1</Version>", props)
        self.assertEqual(0, run(root, "--check").returncode)

    def test_release_follows_a_prerelease_with_its_release(self) -> None:
        root = self.tree(
            {"1234.md": fragment("### Fixed", "- The fix (#1234).")}, version="0.5.0-rc.1"
        )

        result = run(root, "--release", "0.5.0", "--date", "2026-12-01")

        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        text = (root / "CHANGELOG.md").read_text(encoding="utf-8")
        self.assertIn("## [0.5.0] - 2026-12-01", text)
        self.assertNotIn("## [0.5.0-rc.1] - 2026-12-01", text)
        props = (root / "Directory.Build.props").read_text(encoding="utf-8")
        self.assertIn("<Version>0.5.0</Version>", props)

    def test_release_defaults_to_today_in_utc(self) -> None:
        root = self.tree({"1234.md": fragment("### Fixed", "- The fix (#1234).")})
        before = datetime.datetime.now(datetime.timezone.utc).date().isoformat()

        result = run(root, "--release", "0.5.0")

        after = datetime.datetime.now(datetime.timezone.utc).date().isoformat()
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        text = (root / "CHANGELOG.md").read_text(encoding="utf-8")
        self.assertTrue(
            any(f"## [0.5.0] - {date}" in text for date in (before, after)),
            f"the promoted heading does not carry {before} or {after}",
        )

    def test_release_refuses_a_version_that_is_not_newer(self) -> None:
        for version in ("0.4.0", "0.3.9"):
            with self.subTest(version=version):
                root = self.tree({"1234.md": fragment("### Fixed", "- The fix (#1234).")})
                before = snapshot(root)

                result = run(root, "--release", version, "--date", "2026-12-01")

                self.assertEqual(1, result.returncode, result.stdout + result.stderr)
                self.assertIn(
                    f"--release {version} is not newer than the current <Version> 0.4.0",
                    result.stderr,
                )
                self.assertEqual(before, snapshot(root))

    def test_release_refuses_an_existing_release_heading(self) -> None:
        shipped = CHANGELOG.replace(
            "## [0.3.0] - 2026-11-09",
            "## [0.5.0] - 2026-11-01\n\n### Fixed\n\n- A shipped fix (#7).\n\n"
            "## [0.3.0] - 2026-11-09",
        )
        root = self.tree({"1234.md": fragment("### Fixed", "- The fix (#1234).")}, changelog=shipped)
        before = snapshot(root)

        result = run(root, "--release", "0.5.0", "--date", "2026-12-01")

        self.assertEqual(1, result.returncode, result.stdout + result.stderr)
        self.assertIn("a '## [0.5.0]' section already exists", result.stderr)
        self.assertEqual(before, snapshot(root))

    def test_release_refuses_an_invalid_fragment(self) -> None:
        root = self.tree({"1234.md": fragment("### Improved", "- The fix (#1234).")})
        before = snapshot(root)

        result = run(root, "--release", "0.5.0", "--date", "2026-12-01")

        self.assertEqual(1, result.returncode, result.stdout + result.stderr)
        self.assertIn("changes/1234.md", result.stderr)
        self.assertIn("unknown heading", result.stderr)
        self.assertEqual(before, snapshot(root))

    def test_release_refuses_a_second_run(self) -> None:
        root = self.tree({"1234.md": fragment("### Fixed", "- The fix (#1234).")})
        released = run(root, "--release", "0.5.0", "--date", "2026-12-01")
        self.assertEqual(0, released.returncode, released.stdout + released.stderr)
        before = snapshot(root)

        result = run(root, "--release", "0.5.0", "--date", "2026-12-01")

        self.assertEqual(1, result.returncode, result.stdout + result.stderr)
        self.assertIn("not newer", result.stderr)
        self.assertEqual(before, snapshot(root))
        text = (root / "CHANGELOG.md").read_text(encoding="utf-8")
        self.assertEqual(1, text.count("## [0.5.0]"))

    def test_a_release_with_no_fragments_left_still_releases(self) -> None:
        empty = (
            "# Changelog\n\n## [Unreleased]\n\n## [0.3.0] - 2026-11-09\n\n"
            "### Fixed\n\n- A released entry keeps its text (#1).\n"
        )
        root = self.tree({}, changelog=empty)

        result = run(root, "--release", "0.5.0", "--date", "2026-12-01")

        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        text = (root / "CHANGELOG.md").read_text(encoding="utf-8")
        self.assertEqual("", body_of(text, "## [0.5.0] - 2026-12-01"))
        self.assertEqual("", body_of(text, "## [Unreleased]"))
        props = (root / "Directory.Build.props").read_text(encoding="utf-8")
        self.assertIn("<Version>0.5.0</Version>", props)
        self.assertEqual(0, run(root, "--check").returncode)

    def test_release_rejects_a_date_that_is_not_iso(self) -> None:
        root = self.tree({"1234.md": fragment("### Fixed", "- The fix (#1234).")})
        before = snapshot(root)

        result = run(root, "--release", "0.5.0", "--date", "01/12/2026")

        self.assertEqual(2, result.returncode, result.stdout + result.stderr)
        self.assertEqual(before, snapshot(root))

    def test_release_rejects_a_date_spelled_without_dashes(self) -> None:
        # The first two are forms `date.fromisoformat` also accepts, so the exact spelling is checked
        # before parsing; the third is non-padded and the parser alone rejects it.
        for date in ("20261201", "2026-W49-1", "2026-1-2"):
            with self.subTest(date=date):
                root = self.tree({"1234.md": fragment("### Fixed", "- The fix (#1234).")})
                before = snapshot(root)

                result = run(root, "--release", "0.5.0", "--date", date)

                self.assertEqual(2, result.returncode, result.stdout + result.stderr)
                self.assertIn(f"--date {date}: expected YYYY-MM-DD", result.stderr)
                self.assertEqual(before, snapshot(root))

    def test_a_date_without_a_release_is_a_usage_error(self) -> None:
        root = self.tree({})

        result = run(root, "--check", "--date", "2026-12-01")

        self.assertEqual(2, result.returncode, result.stdout + result.stderr)


class ChangelogEntryCountTests(unittest.TestCase):
    """The write-time guard: the assembly never carries fewer entries than it read.

    The shape check refuses every file the guard would fire on, so a lost entry is unreachable
    through the command line; the guard is defence in depth and is exercised on its own functions,
    while the end-to-end cases above pin that no hand-written entry is dropped.
    """

    def setUp(self) -> None:
        self.assembler = load_assembler()

    def test_the_count_skips_the_generated_region(self) -> None:
        text = (
            "# Changelog\n\n## [Unreleased]\n\n### Fixed\n\n"
            "<!-- changelog-fragments:start -->\n- A generated entry (#1).\n"
            "<!-- changelog-fragments:end -->\n\n- A hand-written entry (#2).\n"
        )

        self.assertEqual(1, self.assembler.kept_entries(text))

    def test_a_released_section_does_not_add_to_the_count(self) -> None:
        self.assertEqual(2, self.assembler.kept_entries(CLEAN))
        self.assertEqual(0, self.assembler.kept_entries("# Changelog\n"))

    def test_equal_counts_are_accepted(self) -> None:
        self.assertEqual([], self.assembler.entry_count_errors(2, 2))
        self.assertEqual([], self.assembler.entry_count_errors(0, 0))

    def test_a_lost_entry_is_refused_naming_both_counts(self) -> None:
        errors = self.assembler.entry_count_errors(2, 1)

        self.assertEqual(1, len(errors))
        self.assertIn("CHANGELOG.md:", errors[0])
        self.assertIn("1 entry", errors[0])
        self.assertIn("2 entries", errors[0])

    def test_an_extra_entry_is_refused_too(self) -> None:
        errors = self.assembler.entry_count_errors(1, 2)

        self.assertEqual(1, len(errors))
        self.assertIn("2 entries", errors[0])
        self.assertIn("1 entry", errors[0])


class ChangelogTagCheckTests(unittest.TestCase):
    """`--check --tag <tag>`: the tag's own release heading and `<Version>`."""

    def setUp(self) -> None:
        self.trees: list[Path] = []

    def tearDown(self) -> None:
        for tree in self.trees:
            shutil.rmtree(tree, ignore_errors=True)

    def tree(self, changelog: str, version: str = "0.4.0") -> Path:
        root = make_tree({}, changelog, version)
        self.trees.append(root)
        return root

    def released(self, heading: str = "## [0.4.0] - 2026-11-14", version: str = "0.4.0") -> Path:
        return self.tree(released_changelog(heading), version)

    def test_a_correct_tag_passes(self) -> None:
        result = run(self.released(), "--check", "--tag", "v0.4.0")

        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertIn("v0.4.0", result.stdout)

    def test_a_prerelease_tag_passes(self) -> None:
        root = self.released("## [0.5.0-rc.1] - 2026-11-14", version="0.5.0-rc.1")

        result = run(root, "--check", "--tag", "v0.5.0-rc.1")

        self.assertEqual(0, result.returncode, result.stdout + result.stderr)

    def test_a_subheading_inside_unreleased_is_not_the_release_heading(self) -> None:
        root = self.tree(
            "# Changelog\n\n## [Unreleased]\n\n### Added\n\n- Pending (#1).\n\n"
            "## [0.4.0] - 2026-11-14\n\n### Fixed\n\n- A released entry (#2).\n"
        )

        result = run(root, "--check", "--tag", "v0.4.0")

        self.assertEqual(0, result.returncode, result.stdout + result.stderr)

    def test_a_missing_heading_fails_by_name(self) -> None:
        root = self.tree("# Changelog\n\n## [Unreleased]\n\n- Pending (#1).\n")

        result = run(root, "--check", "--tag", "v0.4.0")

        self.assertEqual(1, result.returncode)
        self.assertIn("no '## ' heading below '## [Unreleased]'", result.stderr)
        self.assertIn("expected '## [0.4.0] - YYYY-MM-DD'", result.stderr)
        self.assertIn("v0.4.0", result.stderr)

    def test_a_missing_unreleased_section_fails_by_name(self) -> None:
        root = self.tree(
            "# Changelog\n\n## [0.4.0] - 2026-11-14\n\n### Fixed\n\n- A released entry (#1).\n"
        )

        result = run(root, "--check", "--tag", "v0.4.0")

        self.assertEqual(1, result.returncode)
        self.assertIn("no '## [Unreleased]' section", result.stderr)
        self.assertIn("expected '## [0.4.0] - YYYY-MM-DD'", result.stderr)

    def test_a_heading_for_another_version_fails_by_name(self) -> None:
        result = run(self.released("## [0.3.0] - 2026-11-09"), "--check", "--tag", "v0.4.0")

        self.assertEqual(1, result.returncode)
        self.assertIn("'## [0.3.0] - 2026-11-09'", result.stderr)
        self.assertIn("expected '## [0.4.0] - YYYY-MM-DD'", result.stderr)

    def test_a_wrong_date_spelling_fails_by_name(self) -> None:
        # The first two are forms `date.fromisoformat` also accepts, so the exact spelling is checked
        # first; the last is a well-spelled date no calendar has.
        for heading in (
            "## [0.4.0] - 2026-1-2",
            "## [0.4.0] - 20261201",
            "## [0.4.0] - 2026-W49-1",
            "## [0.4.0] - 2026-02-30",
            "## [0.4.0]",
        ):
            with self.subTest(heading=heading):
                result = run(self.released(heading), "--check", "--tag", "v0.4.0")

                self.assertEqual(1, result.returncode, result.stdout + result.stderr)
                self.assertIn(heading, result.stderr)
                self.assertIn("expected '## [0.4.0] - YYYY-MM-DD'", result.stderr)

    def test_a_props_mismatch_fails_by_name(self) -> None:
        result = run(self.released(version="0.3.0"), "--check", "--tag", "v0.4.0")

        self.assertEqual(1, result.returncode)
        self.assertIn(
            "Directory.Build.props: <Version> is '0.3.0'; expected '0.4.0'", result.stderr
        )

    def test_every_failure_is_reported_in_one_run(self) -> None:
        result = run(
            self.released("## [0.3.0] - 2026-11-09", version="0.3.0"),
            "--check",
            "--tag",
            "v0.4.0",
        )

        self.assertEqual(1, result.returncode)
        self.assertIn("'## [0.3.0] - 2026-11-09'", result.stderr)
        self.assertIn("<Version> is '0.3.0'", result.stderr)

    def test_a_tag_without_its_v_is_refused_by_name(self) -> None:
        result = run(self.released(), "--check", "--tag", "0.4.0")

        self.assertEqual(1, result.returncode)
        self.assertIn("--tag '0.4.0': expected a release tag such as v0.4.0", result.stderr)

    def test_the_tag_is_a_check_only_option(self) -> None:
        for mode in (["--write"], ["--release", "0.5.0", "--date", "2026-12-01"]):
            with self.subTest(mode=mode):
                root = self.released()
                before = snapshot(root)

                result = run(root, *mode, "--tag", "v0.4.0")

                self.assertEqual(2, result.returncode, result.stdout + result.stderr)
                self.assertIn("--tag is only valid with --check", result.stderr)
                self.assertEqual(before, snapshot(root))


if __name__ == "__main__":
    unittest.main()
