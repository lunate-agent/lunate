"""Pins the runner-capacity retry (#923).

Run 43904858046 went red on `main` without a step failing: `verify (macos-latest, 1/2)` and
`perf (macos-latest)` were cancelled before their first step because GitHub never acquired a hosted
macOS arm64 runner for them, and `ci-ok` failed because they did not succeed. The reporter now names
such a red in the run summary and defers it to `.github/workflows/ci-capacity-retry.yml`, which requests
one rerun of the run's failed jobs. No card is filed for a leg that never started (#922): the attempt the
retry budget does not cover says that the retry is spent and a rerun is the remedy.

These tests read the workflow and the two CI files as text, like `test_release_workflow.py`: there is no
YAML parser in the gate's Python, and the assertions are about wiring -- which trigger, which run, which
permission, which budget -- not about a parsed document. The wiring cannot be exercised before it is on
the default branch, so this is the only check the change itself can make.
"""

import contextlib
import importlib.util
import io
import json
import tempfile
import unittest
from importlib.machinery import SourceFileLoader
from pathlib import Path

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
WORKFLOW = REPOSITORY_ROOT / ".github" / "workflows" / "ci-capacity-retry.yml"
CI = REPOSITORY_ROOT / ".github" / "workflows" / "ci.yml"
REPORTER = REPOSITORY_ROOT / "scripts" / "nightly-issue.sh"
TRIAGE = REPOSITORY_ROOT / "scripts" / "ci-issue-triage"

RETRY_JOB = "gh api -X POST \"repos/${GITHUB_REPOSITORY}/actions/runs/${RUN_ID}/rerun-failed-jobs\""


def without_comments(text: str) -> str:
    return "\n".join(line for line in text.splitlines() if not line.lstrip().startswith("#"))


def text_of(path: Path) -> str:
    return without_comments(path.read_text(encoding="utf-8"))


def triage_module():
    loader = SourceFileLoader("ci_issue_triage", str(TRIAGE))
    spec = importlib.util.spec_from_loader(loader.name, loader)
    module = importlib.util.module_from_spec(spec)
    loader.exec_module(module)
    return module


def jobs_document(jobs: list) -> Path:
    handle = tempfile.NamedTemporaryFile("w", suffix=".json", delete=False, encoding="utf-8")
    with handle:
        json.dump({"jobs": jobs}, handle)
    return Path(handle.name)


class CapacityRetryWorkflowTests(unittest.TestCase):
    def test_the_retry_waits_for_the_run_to_complete(self) -> None:
        text = text_of(WORKFLOW)

        self.assertIn("workflow_run:", text)
        self.assertIn("workflows: [CI]", text)
        self.assertIn("types: [completed]", text)

    def test_only_a_red_run_on_the_full_matrix_events_is_retried(self) -> None:
        text = text_of(WORKFLOW)

        self.assertIn("github.event.workflow_run.conclusion == 'failure'", text)
        self.assertIn("github.event.workflow_run.event == 'push'", text)
        self.assertIn("github.event.workflow_run.event == 'schedule'", text)
        self.assertIn("github.event.workflow_run.head_branch == 'main'", text)
        # A pull request or a merge group must never reach it: neither runs macOS, and both are
        # re-triggered by a push.
        self.assertNotIn("pull_request", text)
        self.assertNotIn("merge_group", text)

    def test_the_budget_is_one_retry(self) -> None:
        text = text_of(WORKFLOW)

        self.assertIn("github.event.workflow_run.run_attempt < 2", text)

    def test_the_retry_is_the_run_it_was_triggered_by(self) -> None:
        text = text_of(WORKFLOW)

        self.assertIn(RETRY_JOB, text)
        self.assertIn("RUN_ID: ${{ github.event.workflow_run.id }}", text)
        # `rerun` (all jobs) would repeat the green Ubuntu and Windows work; only the failures are retried.
        self.assertNotIn("/rerun\"", text)

    def test_the_rerun_call_needs_and_declares_actions_write(self) -> None:
        text = text_of(WORKFLOW)

        self.assertIn("actions: write", text)
        self.assertIn("permissions:", text)

    def test_the_decision_is_the_reporters_decision(self) -> None:
        """One predicate, two callers: the workflow and the reporter cannot disagree."""
        text = text_of(WORKFLOW)

        self.assertIn("python3 scripts/ci-issue-triage --capacity-json", text)
        self.assertIn("--capacity-json", text_of(REPORTER))

    def test_the_derived_job_names_are_the_ci_workflow_s_job_names(self) -> None:
        """The predicate ignores the aggregate and the reporter by name, so the names must be real."""
        triage = triage_module()
        ci = text_of(CI)

        self.assertTrue(triage.DERIVED_JOBS, "the predicate must ignore the aggregate and the reporter")
        for name in triage.DERIVED_JOBS:
            self.assertIn(f"name: {name}\n", ci + "\n", f"{name} is not a job in ci.yml")

    def test_the_reporter_defers_below_the_budget_and_names_the_rerun_at_it(self) -> None:
        reporter = text_of(REPORTER)

        self.assertIn("RETRY_BUDGET=2", reporter)
        self.assertIn('[ "$attempt" -lt "$RETRY_BUDGET" ]', reporter)

    def test_the_summary_names_the_abandoned_legs_and_no_derived_job(self) -> None:
        """`ci-ok` and the reporter restate the others' results, so they are never an abandoned leg."""
        reporter = text_of(REPORTER)
        triage = triage_module()
        path = jobs_document(
            [
                {"id": "1", "name": "verify (macos-latest, 1/2)", "conclusion": "cancelled", "runner_id": 0, "steps": []},
                {"id": "2", "name": "perf (macos-latest)", "conclusion": "cancelled", "runner_id": 0, "steps": []},
                {"id": "3", "name": "ci-ok", "conclusion": "cancelled", "runner_id": 0, "steps": []},
                {"id": "4", "name": "red-main-issue", "conclusion": "cancelled", "runner_id": 0, "steps": []},
            ]
        )
        out = io.StringIO()
        try:
            with contextlib.redirect_stdout(out):
                triage.abandoned_jobs(path)
        finally:
            path.unlink()

        self.assertIn("--abandoned-json", reporter)
        named = [line.split("\t", 1)[1] for line in out.getvalue().splitlines()]
        self.assertEqual(named, ["verify (macos-latest, 1/2)", "perf (macos-latest)"])


if __name__ == "__main__":
    unittest.main()
