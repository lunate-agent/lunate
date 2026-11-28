"""Pins that every gate test path records the project wall times the partition reads.

`scripts/verify.sh` records one per-project record per shard and culture pass (#789), and the `ci-pipeline`
spec requires every gate run that ran test projects to leave that record. Two paths exited before the
recorder did: a pass that failed twice in a row (the flake budget is spent, then the leg exits), and the
optional pooled runner (`LUNATE_VERIFY_JOBS`), which writes one log per project and never calls the
recorder at all. Both still ran projects, so both still have to record them.

These tests run the gate's own `run_tests` and pooled recorder out of `scripts/verify.sh`, with a fake
`shard-weights.sh` and fake test command, so the wiring is exercised rather than only grepped.
"""

import os
import subprocess
import tempfile
import textwrap
import unittest
from pathlib import Path

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
VERIFY = REPOSITORY_ROOT / "scripts" / "verify.sh"


def function_body(text: str, name: str) -> str:
    """The lines of one shell function, from its opening line to the closing brace in column 0."""
    lines = text.splitlines()
    start = next(index for index, line in enumerate(lines) if line.startswith(f"{name}() {{"))
    end = lines.index("}", start)
    return "\n".join(lines[start : end + 1])


class VerifyProjectRecordingTests(unittest.TestCase):
    def setUp(self) -> None:
        self.verify_text = VERIFY.read_text(encoding="utf-8")
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.record_log = self.root / "records.log"
        self.script_dir = self.root / "scripts"
        self.script_dir.mkdir()
        self.verify_tmp_root = self.root / "verify-tmp"
        self.verify_tmp_root.mkdir()
        (self.script_dir / "shard-weights.sh").write_text(
            textwrap.dedent(
                """\
                #!/usr/bin/env bash
                set -euo pipefail
                printf 'args:%s\\n' "$*" >> "$RECORD_LOG"
                cat >> "$RECORD_LOG"
                """
            ),
            encoding="utf-8",
        )

    def tearDown(self) -> None:
        self.temp.cleanup()

    def run_harness(self, body: str, fake_test: str) -> subprocess.CompletedProcess[str]:
        """Runs extracted gate functions with a fake test command and a fake recorder."""
        fake = self.root / "fake-test"
        fake.write_text(fake_test, encoding="utf-8")
        fake.chmod(0o755)
        functions = "\n\n".join(
            function_body(self.verify_text, name)
            for name in ("record_project_times", "record_project_times_if_configured", "run_tests")
        )
        harness = self.root / "harness.sh"
        harness.write_text(
            textwrap.dedent(
                f"""\
                set -euo pipefail
                export RID=osx-arm64 SHARD_I=1 SHARD_N=2
                export VERIFY_TIMING_PASS=default
                export RECORD_LOG={str(self.record_log)!r}
                SCRIPT_DIR={str(self.script_dir)!r}
                VERIFY_TMP_ROOT={str(self.verify_tmp_root)!r}
                report_failed_tests() {{ :; }}
                record_flake() {{ :; }}
                {functions}
                {body}
                """
            ),
            encoding="utf-8",
        )
        return subprocess.run(
            ["bash", str(harness), str(fake)],
            capture_output=True,
            text=True,
            env={**os.environ, "RECORD_LOG": str(self.record_log)},
            check=False,
        )

    def test_a_pass_that_fails_twice_still_records_its_projects(self) -> None:
        """The retry budget is spent, then the leg exits; the projects it ran must still be recorded."""
        result = self.run_harness(
            'run_tests "test" "$1"',
            textwrap.dedent(
                """\
                #!/usr/bin/env bash
                echo "tests/Lunate.Ai.Tests/bin/Release/net11.0/Lunate.Ai.Tests.dll (net11.0) failed (1s 000ms)"
                echo "total: 7"
                exit 1
                """
            ),
        )

        self.assertNotEqual(result.returncode, 0, "run_tests must fail after the retry budget")
        recorded = self.record_log.read_text(encoding="utf-8")
        self.assertIn("--pass default", recorded)
        self.assertIn("Lunate.Ai.Tests.dll", recorded)

    def test_a_pass_that_discovers_zero_tests_still_records_its_projects(self) -> None:
        """A discovery regression exits through another branch; it still ran the projects it reports."""
        result = self.run_harness(
            'run_tests "test" "$1"',
            textwrap.dedent(
                """\
                #!/usr/bin/env bash
                echo "tests/Lunate.Ai.Tests/bin/Release/net11.0/Lunate.Ai.Tests.dll (net11.0) passed (1s 000ms)"
                exit 0
                """
            ),
        )

        self.assertNotEqual(result.returncode, 0, "a pass with no total must fail the gate")
        recorded = self.record_log.read_text(encoding="utf-8")
        self.assertIn("--pass default", recorded)
        self.assertIn("Lunate.Ai.Tests.dll", recorded)

    def test_the_pooled_runner_records_each_pass_from_its_project_logs(self) -> None:
        """The pooled path runs one project per log, so the recorder reads their concatenation."""
        logs = self.root / "pooled"
        logs.mkdir()
        (logs / "0.log").write_text("first project log\n", encoding="utf-8")
        (logs / "1.log").write_text("second project log\n", encoding="utf-8")
        functions = "\n\n".join(
            function_body(self.verify_text, name)
            for name in ("record_project_times", "record_pooled_project_times")
        )
        harness = self.root / "pooled-harness.sh"
        harness.write_text(
            textwrap.dedent(
                f"""\
                set -euo pipefail
                export RID=osx-arm64 SHARD_I=1 SHARD_N=2
                export RECORD_LOG={str(self.record_log)!r}
                SCRIPT_DIR={str(self.script_dir)!r}
                VERIFY_TMP_ROOT={str(self.verify_tmp_root)!r}
                verify_test_queue() {{ printf '%s\\n' '0:serial:first' '1:parallel:second'; }}
                verify_shard_queue() {{ cat; }}
                {functions}
                record_pooled_project_times {str(logs)!r} serial-first default
                """
            ),
            encoding="utf-8",
        )
        result = subprocess.run(
            ["bash", str(harness)],
            capture_output=True,
            text=True,
            env={**os.environ, "RECORD_LOG": str(self.record_log)},
            check=False,
        )

        self.assertEqual(result.returncode, 0, result.stderr)
        recorded = self.record_log.read_text(encoding="utf-8")
        self.assertIn("--pass default", recorded)
        self.assertIn("first project log\nsecond project log", recorded)

    def test_both_pooled_passes_reach_the_recorder(self) -> None:
        """Each culture pass is recorded under its own name, so a refresh keeps both."""
        phase = function_body(self.verify_text, "verify_test_phase")
        self.assertIn(
            'record_pooled_project_times "$default_dir" serial-first default',
            phase,
        )
        self.assertIn(
            'record_pooled_project_times "$culture_dir" serial-last de-at',
            phase,
        )


if __name__ == "__main__":
    unittest.main()
