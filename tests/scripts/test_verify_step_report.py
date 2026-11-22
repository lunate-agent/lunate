"""Pins the gate naming the step it failed in.

A red `ci.yml` leg on `main` is triaged from the run's check run, not from its log: the job page of a
public run answers "Sign in to view logs", while GitHub keeps every `::error::` line of the step in the
check run's annotations, where `gh api .../check-runs/<id>/annotations` returns it without an account.
The gate printed its steps but never named the one that failed, so the only annotation a red-main card
carried was "Process completed with exit code 1" and the card could not say which step to look at
(`verify (windows-latest, 1/4)` of runs 44033633019 and 44051817008, #937 and #942).

`report_failed_step` in `scripts/lib.sh` is that report; `scripts/verify.sh` hands it the step it was in
from an `EXIT` trap, because `set -e` leaves no other place that still knows it. The tests below exercise
the report through bash, as the gate calls it, and read `verify.sh` as text for the wiring.

`verify (macos-latest, 2/2)` of run 44189133429 shows what that report still lacks: its test step failed
twice and the annotation named the step, so the leg could be located but not attributed -- which test
failed was only in the job's log, and the log needs an account (#948). `report_failed_tests` names the
tests the pass printed, from the same output `run_tests` already read for the flake ledger, so the check
run carries the test as well as the step.

The same leg was red again in run 44466042157, and this time the pass named no test at all: the annotation
said `the test (de-AT culture) step failed (exit 1, 782s)` and nothing else, because the report read the
final attempt's output alone -- a first attempt that failed on a test and a retry whose session aborted
left no failing-test line in the file it read -- and because a pass whose session aborted or whose host
crashed prints a module line and a verdict instead of a failing test (`#951`). The report therefore reads
both attempts, and a pass without a test name reports its failing modules and the verdict it ended with;
`FailedPassReportTests` pins both.
"""

import os
import subprocess
import tempfile
import unittest
from pathlib import Path

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
LIB = REPOSITORY_ROOT / "scripts" / "lib.sh"
VERIFY = REPOSITORY_ROOT / "scripts" / "verify.sh"

# Calls the report the way the gate does: one step name, the exit status and the seconds it took.
CALL = 'set -euo pipefail; . "$0"; report_failed_step "$@"'

# Calls the failing-test report the way run_tests does: the pass's label, its exit status, the output of
# the final attempt and the output of the first one.
CALL_TESTS = 'set -euo pipefail; . "$0"; report_failed_tests "$@"'


def without_comments(text: str) -> str:
    return "\n".join(line for line in text.splitlines() if not line.lstrip().startswith("#"))


def function_body(text: str, name: str) -> str:
    """The lines of one shell function, from its opening line to the closing brace in column 0."""
    lines = text.splitlines()
    start = next(index for index, line in enumerate(lines) if line.startswith(f"{name}() {{"))
    end = lines.index("}", start)
    return "\n".join(lines[start : end + 1])


def report(step: str, status: str, elapsed: str, summary: str | None = None) -> subprocess.CompletedProcess:
    environment = {key: value for key, value in os.environ.items() if key != "GITHUB_STEP_SUMMARY"}
    if summary is not None:
        environment["GITHUB_STEP_SUMMARY"] = summary
    return subprocess.run(
        ["bash", "-c", CALL, str(LIB), step, status, elapsed],
        capture_output=True,
        text=True,
        env=environment,
        check=False,
    )


def report_tests(
    label: str,
    output: str,
    summary: str | None = None,
    status: str = "1",
    first: str | None = None,
) -> subprocess.CompletedProcess:
    environment = {key: value for key, value in os.environ.items() if key != "GITHUB_STEP_SUMMARY"}
    if summary is not None:
        environment["GITHUB_STEP_SUMMARY"] = summary
    arguments = [str(LIB), label, status, output]
    if first is not None:
        arguments.append(first)
    return subprocess.run(
        ["bash", "-c", CALL_TESTS, *arguments],
        capture_output=True,
        text=True,
        env=environment,
        check=False,
    )


def test_output(lines: list[str]) -> Path:
    """A file holding what one test pass printed, as `run_tests` hands it to the report."""
    handle = tempfile.NamedTemporaryFile("w", suffix=".log", delete=False, encoding="utf-8")
    handle.write("\n".join(lines) + "\n")
    handle.close()
    return Path(handle.name)


class FailedStepReportTests(unittest.TestCase):
    def test_a_failed_step_is_named_in_an_error_annotation(self) -> None:
        """The annotation is the one place a reader without an account sees what failed."""
        result = report("contract package roundtrip", "1", "42")

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn(
            "::error::verify: the contract package roundtrip step failed (exit 1, 42s)",
            result.stdout,
        )

    def test_a_successful_exit_reports_nothing(self) -> None:
        """Only a red run needs the name; a green one must not carry an error annotation."""
        result = report("contract package roundtrip", "0", "42")

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(result.stdout, "")

    def test_a_failure_before_the_first_step_reports_nothing(self) -> None:
        """A failure outside a step (argument parsing) has no step to name."""
        result = report("", "1", "0")

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(result.stdout, "")

    def test_the_run_summary_carries_the_failed_step_too(self) -> None:
        """The job summary renders on the public run page, so it carries the same sentence."""
        handle = tempfile.NamedTemporaryFile("w", suffix=".md", delete=False, encoding="utf-8")
        handle.close()
        summary = Path(handle.name)
        try:
            result = report("publish (win-x64)", "2", "7", str(summary))
            written = summary.read_text(encoding="utf-8")
        finally:
            summary.unlink()

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("::error::verify: the publish (win-x64) step failed (exit 2, 7s)", result.stdout)
        self.assertIn("failed step: publish (win-x64) (exit 2, 7s)", written)

    def test_the_summary_is_optional(self) -> None:
        """A local run has no `GITHUB_STEP_SUMMARY`, and an unset variable is not a failure."""
        result = report("build", "1", "3")

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("::error::verify: the build step failed (exit 1, 3s)", result.stdout)
        self.assertEqual(result.stderr, "")


class FailedTestReportTests(unittest.TestCase):
    def test_the_failing_test_is_named_in_an_error_annotation(self) -> None:
        """The check run's annotations are what a session without an account can read."""
        output = test_output(
            [
                "Running tests from Lunate.Tui.Tests.dll (net11.0|arm64)",
                "failed Lunate.Tui.Tests.UnixRawModeTests.Enter_restores_a_pty_slave (7ms)",
                "  posix_openpt failed",
                "  total: 2062, failed: 1",
            ]
        )
        try:
            result = report_tests("test", str(output))
        finally:
            output.unlink()

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(
            result.stdout,
            "::error::verify: test: Lunate.Tui.Tests.UnixRawModeTests.Enter_restores_a_pty_slave failed\n",
        )

    def test_the_other_failure_shapes_name_a_test_too(self) -> None:
        """The gate has read `[FAIL]` and `xunit.v3 ... failed` before; the report reads them as well."""
        output = test_output(
            [
                "  [FAIL] Lunate.Agent.Tests.FakeFailure.Case",
                "  xunit.v3.runner failed Lunate.Protocols.Tests.AcpWireTests.Times_out (1s 20ms)",
                "error: the build failed before any test ran",
            ]
        )
        try:
            result = report_tests("test", str(output))
        finally:
            output.unlink()

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("::error::verify: test: Lunate.Agent.Tests.FakeFailure.Case failed", result.stdout)
        self.assertIn(
            "::error::verify: test: Lunate.Protocols.Tests.AcpWireTests.Times_out failed", result.stdout
        )

    def test_at_most_five_tests_are_named(self) -> None:
        """A red leg that failed sixteen tests still names the step without burying it."""
        output = test_output([f"failed Lunate.Agent.Tests.Truncation.Test{n} (1ms)" for n in range(1, 8)])
        try:
            result = report_tests("test", str(output))
        finally:
            output.unlink()

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(result.stdout.count("::error::"), 5)
        self.assertIn("Truncation.Test5 failed", result.stdout)
        self.assertNotIn("Truncation.Test6", result.stdout)

    def test_a_colored_failing_test_is_named_too(self) -> None:
        """GitHub Actions colors a redirected stream, and the escaped line still names the test (#954).

        This is the shape `verify (macos-latest, 2/2)`'s de-AT pass left in run 44722124911: the runner set
        `GITHUB_ACTIONS=true`, so the failing line and its module arrived wrapped in SGR sequences and the
        report matched neither -- the check run carried only the CLI's verdict. Transcript captured from
        the same `dotnet test --test-modules` invocation with `GITHUB_ACTIONS=true`.
        """
        escape = "\x1b"
        output = test_output(
            [
                f"{escape}[31mfailed{escape}[m Lunate.Tui.Tests.UnixRawModeTests.Enter_restores_a_pty_slave "
                f"{escape}[90m(8ms){escape}[m",
                f"  {escape}[31m  posix_openpt failed",
                f"{escape}[m{escape}[m/Users/runner/work/lunate/lunate/tests/Lunate.Tui.Tests/bin/Release/"
                f"net11.0/Lunate.Tui.Tests.dll (net11.0|arm64) {escape}[31mfailed with 1 error(s){escape}[m "
                f"[{escape}[32m+2061{escape}[m/{escape}[31mx1{escape}[m/{escape}[33m?0{escape}[m] "
                f"{escape}[90m(1s 512ms){escape}[m",
                "Exit code: 2",
                f"{escape}[31mTest run summary: Failed!",
                "  total: 2062",
                f"{escape}[31m  failed: 1",
                "Test run completed with non-success exit code: 2. At least one test failed. "
                "See: https://aka.ms/testingplatform/exitcodes",
            ]
        )
        try:
            result = report_tests("test (de-AT culture)", str(output), status="2")
        finally:
            output.unlink()

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(
            result.stdout,
            "::error::verify: test (de-AT culture): "
            "Lunate.Tui.Tests.UnixRawModeTests.Enter_restores_a_pty_slave failed\n",
        )
        self.assertNotIn(escape, result.stdout)

    def test_an_output_without_a_failing_test_names_the_error_it_ended_with(self) -> None:
        """A build that failed before any test ran names no test, so the pass's own error line is named."""
        output = test_output(["error: the build failed before any test ran", "  total: 0, failed: 0"])
        try:
            result = report_tests("test", str(output))
        finally:
            output.unlink()

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(
            result.stdout,
            "::error::verify: test: the pass named no failing test (exit 1); it ended with: "
            "error: the build failed before any test ran\n",
        )

    def test_a_missing_output_still_names_the_pass_and_its_exit(self) -> None:
        """A pass whose output was never written leaves the exit status as the one fact to report."""
        result = report_tests("test", "/nonexistent/lunate-gate-output.log", status="137")

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(
            result.stdout,
            "::error::verify: test: the pass named no failing test and printed no verdict (exit 137)\n",
        )

    def test_the_run_summary_lists_the_failing_tests(self) -> None:
        """The job summary renders on the public run page, so it carries the names too."""
        output = test_output(["failed Lunate.Coding.Tests.ProcessSessionTests.Killing_the_session (30s)"])
        handle = tempfile.NamedTemporaryFile("w", suffix=".md", delete=False, encoding="utf-8")
        handle.close()
        summary = Path(handle.name)
        try:
            result = report_tests("test (de-AT culture)", str(output), str(summary))
            written = summary.read_text(encoding="utf-8")
        finally:
            output.unlink()
            summary.unlink()

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn(
            "- failing test: Lunate.Coding.Tests.ProcessSessionTests.Killing_the_session (test (de-AT culture))",
            written,
        )


class FailedPassReportTests(unittest.TestCase):
    """A red pass that named no test names its failing modules and the verdict it ended with (#951)."""

    MODULE = (
        "  /home/runner/work/lunate/lunate/tests/Lunate.Coding.Tests/bin/Release/net11.0/"
        "Lunate.Coding.Tests.dll (net11.0|arm64) failed with 1 error(s) [+2949/x0/?1] (6m 28s 855ms)"
    )
    VERDICT = (
        "Test run completed with non-success exit code: 4. The test session was aborted. "
        "See: https://aka.ms/testingplatform/exitcodes"
    )

    def test_the_failing_module_is_named_without_the_runner_path(self) -> None:
        """The module line carries the counters -- `?1` is the test that aborted -- and no path."""
        output = test_output([self.MODULE, "", "Test run summary: Failed!", "  total: 2950"])
        try:
            result = report_tests("test (de-AT culture)", str(output))
        finally:
            output.unlink()

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(
            result.stdout,
            "::error::verify: test (de-AT culture): Lunate.Coding.Tests.dll (net11.0|arm64) "
            "failed with 1 error(s) [+2949/x0/?1] (6m 28s 855ms)\n",
        )

    def test_the_verdict_is_named_without_the_exit_code_link(self) -> None:
        """The run's own sentence says why it ended; the link is the same URL on every red."""
        output = test_output([self.MODULE, self.VERDICT])
        try:
            result = report_tests("test (de-AT culture)", str(output), status="4")
        finally:
            output.unlink()

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn(
            "::error::verify: test (de-AT culture): the pass named no failing test (exit 4); "
            "it ended with: Test run completed with non-success exit code: 4. "
            "The test session was aborted.\n",
            result.stdout,
        )
        self.assertNotIn("aka.ms", result.stdout)

    def test_an_error_line_is_the_verdict_when_the_run_never_completed(self) -> None:
        """A host that crashed before the summary prints an `error:` line instead of a verdict."""
        output = test_output(["error: A fatal error occurred. The test host exited early."])
        try:
            result = report_tests("test", str(output), status="137")
        finally:
            output.unlink()

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn(
            "the pass named no failing test (exit 137); it ended with: "
            "error: A fatal error occurred. The test host exited early.",
            result.stdout,
        )

    def test_at_most_three_modules_are_named(self) -> None:
        """A pass that lost several modules still names the verdict that explains them."""
        output = test_output(
            [
                f"  /w/tests/Lunate.Shard{n}.Tests/bin/Release/net11.0/Lunate.Shard{n}.Tests.dll "
                "(net11.0|arm64) failed with 1 error(s) [+1/x0/?1] (1s)"
                for n in range(1, 6)
            ]
        )
        try:
            result = report_tests("test", str(output))
        finally:
            output.unlink()

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(result.stdout.count("failed with 1 error(s)"), 3)
        self.assertIn("Lunate.Shard3.Tests.dll", result.stdout)
        self.assertNotIn("Lunate.Shard4.Tests.dll", result.stdout)

    def test_a_pass_with_no_name_and_no_verdict_still_names_itself(self) -> None:
        """Output that holds neither leaves the exit status as the only fact, and that is reported."""
        output = test_output(["  ?", ""])
        try:
            result = report_tests("test", str(output), status="137")
        finally:
            output.unlink()

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(
            result.stdout,
            "::error::verify: test: the pass named no failing test and printed no verdict (exit 137)\n",
        )

    def test_a_pass_that_named_a_test_names_no_module_or_verdict(self) -> None:
        """The fallback never repeats what a test name already tells the reader."""
        output = test_output(
            [
                "failed Lunate.Coding.Tests.ProcessSessionTests.Killing_the_session (30s)",
                self.MODULE,
                self.VERDICT,
            ]
        )
        try:
            result = report_tests("test", str(output), status="2")
        finally:
            output.unlink()

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertNotIn("failed with 1 error(s)", result.stdout)
        self.assertNotIn("the pass named no failing test", result.stdout)

    def test_a_colored_module_line_is_named_without_its_escapes(self) -> None:
        """A red module on GitHub Actions is named too, and the annotation carries no escape codes (#954)."""
        escape = "\x1b"
        colored = (
            f"{escape}[m{escape}[m/home/runner/work/lunate/lunate/tests/Lunate.Coding.Tests/bin/Release/"
            f"net11.0/Lunate.Coding.Tests.dll (net11.0|arm64) {escape}[31mfailed with 1 error(s){escape}[m "
            f"[{escape}[32m+2949{escape}[m/{escape}[31mx1{escape}[m/{escape}[33m?0{escape}[m] "
            f"{escape}[90m(6m 28s 855ms){escape}[m"
        )
        output = test_output([colored, self.VERDICT])
        try:
            result = report_tests("test (de-AT culture)", str(output), status="2")
        finally:
            output.unlink()

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn(
            "::error::verify: test (de-AT culture): Lunate.Coding.Tests.dll (net11.0|arm64) "
            "failed with 1 error(s) [+2949/x1/?0] (6m 28s 855ms)\n",
            result.stdout,
        )
        self.assertNotIn(escape, result.stdout)

    def test_the_run_summary_carries_the_module_and_the_verdict(self) -> None:
        """The job summary renders on the public run page, so it carries the same attribution."""
        output = test_output([self.MODULE, self.VERDICT])
        handle = tempfile.NamedTemporaryFile("w", suffix=".md", delete=False, encoding="utf-8")
        handle.close()
        summary = Path(handle.name)
        try:
            result = report_tests("test (de-AT culture)", str(output), str(summary), status="4")
            written = summary.read_text(encoding="utf-8")
        finally:
            output.unlink()
            summary.unlink()

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn(
            "- failing module: Lunate.Coding.Tests.dll (net11.0|arm64) failed with 1 error(s) "
            "[+2949/x0/?1] (6m 28s 855ms) (test (de-AT culture))",
            written,
        )
        self.assertIn(
            "- failed pass: test (de-AT culture) named no failing test (exit 4): "
            "Test run completed with non-success exit code: 4. The test session was aborted",
            written,
        )


    def test_a_test_the_first_attempt_failed_is_named_when_the_retry_named_none(self) -> None:
        """The retry is a second chance, not a second report: what the first attempt failed on is kept.

        This is the shape run 44466042157's de-AT pass left behind -- one attempt that failed on a test,
        a retry whose session aborted -- and it named nothing while `run_tests` held both outputs.
        """
        final = test_output([self.MODULE, self.VERDICT])
        first = test_output(
            [
                "failed Lunate.Coding.Tests.ProcessSessionTests.Killing_the_session (30s)",
                "Test run completed with non-success exit code: 2. At least one test failed.",
            ]
        )
        try:
            result = report_tests("test (de-AT culture)", str(final), status="4", first=str(first))
        finally:
            final.unlink()
            first.unlink()

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(
            result.stdout,
            "::error::verify: test (de-AT culture): "
            "Lunate.Coding.Tests.ProcessSessionTests.Killing_the_session failed\n",
        )

    def test_a_name_both_attempts_printed_appears_once(self) -> None:
        """Two attempts that failed the same test must not double the annotation."""
        output = test_output(["failed Lunate.Agent.Tests.FakeFailure.Case (1ms)"])
        first = test_output(["failed Lunate.Agent.Tests.FakeFailure.Case (2ms)"])
        try:
            result = report_tests("test", str(output), status="2", first=str(first))
        finally:
            output.unlink()
            first.unlink()

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(
            result.stdout, "::error::verify: test: Lunate.Agent.Tests.FakeFailure.Case failed\n"
        )

    def test_the_first_attempts_module_is_named_when_the_retry_named_none(self) -> None:
        """A retry that printed nothing at all leaves the first attempt's module as the attribution."""
        final = test_output([""])
        first = test_output([self.MODULE, "  total: 0, failed: 0"])
        try:
            result = report_tests("test", str(final), status="1", first=str(first))
        finally:
            final.unlink()
            first.unlink()

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("Lunate.Coding.Tests.dll (net11.0|arm64) failed with 1 error(s)", result.stdout)

    def test_the_first_attempts_verdict_is_used_when_the_retry_printed_none(self) -> None:
        """A verdict is the one thing an aborted retry may not print, so the first attempt's stands in."""
        final = test_output([""])
        first = test_output([self.VERDICT])
        try:
            result = report_tests("test", str(final), status="4", first=str(first))
        finally:
            final.unlink()
            first.unlink()

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn(
            "the pass named no failing test (exit 4); it ended with: "
            "Test run completed with non-success exit code: 4. The test session was aborted.",
            result.stdout,
        )


class FailedPassWiringTests(unittest.TestCase):
    def test_the_pass_exit_status_reaches_the_report(self) -> None:
        """The gate's own exit is 1 whatever the pass returned; the verdict names the pass's status."""
        body = function_body(without_comments(VERIFY.read_text(encoding="utf-8")), "run_tests")

        self.assertIn('report_failed_tests "$label" "$status" "$output" "$first_output"', body)

    def test_the_flake_ledger_reads_the_pass_through_the_escape_strip(self) -> None:
        """The ledger names the same test the annotation does, so it strips the escape codes too (#954)."""
        body = function_body(without_comments(VERIFY.read_text(encoding="utf-8")), "record_flake")

        self.assertIn('strip_ansi < "$output"', body)


class FailedStepWiringTests(unittest.TestCase):
    def test_the_gate_reports_the_step_it_exited_in(self) -> None:
        """`set -e` exits on the failing command, so only the EXIT trap still knows the step.

        The trap is `verify_exit`, which already records the shard's wall time and removes the
        scratch directory: a second `trap ... EXIT` would replace it, so the report goes there.
        """
        text = without_comments(VERIFY.read_text(encoding="utf-8"))

        self.assertEqual(text.count("trap verify_exit EXIT"), 1)
        self.assertIn('report_failed_step "$STEP_NAME" "$status"', function_body(text, "verify_exit"))

    def test_the_report_runs_before_the_wall_time_is_recorded(self) -> None:
        """A red leg is a failed shard, so the wall-time record must not get in the way."""
        body = function_body(without_comments(VERIFY.read_text(encoding="utf-8")), "verify_exit")

        self.assertLess(body.index("report_failed_step"), body.index("record_wall_time"))

    def test_the_platform_profile_runs_this_test(self) -> None:
        """An unrun test is not a check: the platform job proves the report on every push."""
        text = without_comments(VERIFY.read_text(encoding="utf-8"))

        self.assertIn("tests/scripts/test_verify_step_report.py", text)

    def test_a_red_test_step_names_its_failing_tests(self) -> None:
        """`run_tests` has the pass's output, so it is where the failing test names still exist."""
        body = function_body(without_comments(VERIFY.read_text(encoding="utf-8")), "run_tests")

        self.assertIn('report_failed_tests "$label"', body)

    def test_the_names_are_reported_before_the_gate_gives_up(self) -> None:
        """A pass that failed twice exits right after; the names have to be out by then."""
        body = function_body(without_comments(VERIFY.read_text(encoding="utf-8")), "run_tests")

        self.assertLess(
            body.index("report_failed_tests"), body.index("failed twice in a row")
        )

    def test_a_flake_that_passed_on_retry_names_no_test(self) -> None:
        """A green leg carries no error annotation: the report runs only where the gate gives up."""
        text = without_comments(VERIFY.read_text(encoding="utf-8"))

        self.assertNotIn("report_failed_tests", function_body(text, "record_flake"))


if __name__ == "__main__":
    unittest.main()
