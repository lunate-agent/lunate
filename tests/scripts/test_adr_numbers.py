import subprocess
import sys
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
CHECKER = REPOSITORY_ROOT / "scripts" / "check-adr-numbers.py"
FIXTURES = REPOSITORY_ROOT / "tests" / "fixtures" / "adr-numbering"


class AdrNumberingTests(unittest.TestCase):
    def run_checker(self, directory: Path) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            [sys.executable, str(CHECKER), "--directory", str(directory)],
            capture_output=True,
            text=True,
            check=False,
        )

    def run_fixture(self, fixture: str) -> subprocess.CompletedProcess[str]:
        return self.run_checker(FIXTURES / fixture / "adr")

    def test_duplicate_numbers_report_both_files_and_next_free_number(self):
        result = self.run_fixture("duplicate")

        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        output = result.stdout + result.stderr
        self.assertIn("tests/fixtures/adr-numbering/duplicate/adr/0001-first.md", output)
        self.assertIn("tests/fixtures/adr-numbering/duplicate/adr/0001-second.md", output)
        self.assertIn("next free number: 0003", output)

    def test_a_duplicate_at_the_numbers_that_broke_main(self):
        """0034 was taken twice on main: the posix descriptor ADR and the pass-through ADR.

        The fixture keeps those numbers rather than a toy 0001 pair, so the case that actually
        reddened main has a test of its own.
        """
        result = self.run_fixture("duplicate-real-numbers")

        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        output = result.stdout + result.stderr
        self.assertIn("duplicate ADR-0034", output)
        self.assertIn("0034-first.md", output)
        self.assertIn("0034-second.md", output)
        self.assertIn("next free number: 0036", output)

    def test_gap_reports_bracketing_files_missing_number_and_next_free_number(self):
        result = self.run_fixture("gap")

        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        output = result.stdout + result.stderr
        self.assertIn("tests/fixtures/adr-numbering/gap/adr/0001-first.md", output)
        self.assertIn("tests/fixtures/adr-numbering/gap/adr/0003-third.md", output)
        self.assertIn("missing ADR-0002", output)
        self.assertIn("next free number: 0004", output)

    def test_clean_consecutive_numbers_pass(self):
        result = self.run_fixture("clean")

        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_numbers_occupied_only_by_addenda_are_not_gaps(self):
        result = self.run_fixture("addenda-filled")

        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_addenda_elsewhere_do_not_fill_a_genuine_gap(self):
        result = self.run_fixture("addenda-do-not-fill-gaps")

        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        output = result.stdout + result.stderr
        self.assertIn(
            "tests/fixtures/adr-numbering/addenda-do-not-fill-gaps/adr/0001-first.md",
            output,
        )
        self.assertIn(
            "tests/fixtures/adr-numbering/addenda-do-not-fill-gaps/adr/0003-third.md",
            output,
        )
        self.assertIn("missing ADR-0002", output)
        self.assertIn("next free number: 0005", output)

    def test_repository_adr_tree_passes(self):
        result = self.run_checker(REPOSITORY_ROOT / "adr")

        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)


if __name__ == "__main__":
    unittest.main()
