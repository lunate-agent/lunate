import subprocess
import sys
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
CHECKER = REPOSITORY_ROOT / "scripts" / "check-adr-status.py"
FIXTURES = REPOSITORY_ROOT / "tests" / "fixtures" / "adr-status"


class AdrStatusTests(unittest.TestCase):
    def run_checker(self, fixture: str) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            [
                sys.executable,
                str(CHECKER),
                "--directory",
                str(FIXTURES / fixture / "adr"),
            ],
            capture_output=True,
            text=True,
            check=False,
        )

    def test_an_accepted_adr_completing_a_proposed_adr_is_reported(self):
        result = self.run_checker("completed-proposed")

        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        output = result.stdout + result.stderr
        self.assertIn(
            "tests/fixtures/adr-status/completed-proposed/adr/0003-completer.md "
            "is accepted and completes ADR-0002",
            output,
        )
        self.assertIn(
            "tests/fixtures/adr-status/completed-proposed/adr/0002-second-half.md "
            "is still proposed",
            output,
        )

    def test_an_accepted_adr_completing_an_accepted_adr_passes(self):
        result = self.run_checker("completed-accepted")

        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_a_completer_that_is_not_accepted_does_not_imply_acceptance(self):
        result = self.run_checker("proposed-completer")

        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_a_missing_status_line_is_reported(self):
        result = self.run_checker("missing-status")

        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn(
            "tests/fixtures/adr-status/missing-status/adr/0001-no-status.md: "
            "missing `Status:` line",
            result.stdout + result.stderr,
        )

    def test_an_unknown_status_is_reported(self):
        result = self.run_checker("invalid-status")

        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn(
            "tests/fixtures/adr-status/invalid-status/adr/0001-pending.md: "
            "invalid status 'pending'",
            result.stdout + result.stderr,
        )

    def test_a_bold_status_line_is_read(self):
        result = self.run_checker("bold-status")

        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_an_addendum_does_not_redefine_the_primary_record(self):
        result = self.run_checker("addendum-is-not-the-record")

        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)


if __name__ == "__main__":
    unittest.main()
