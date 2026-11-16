"""Pins the runner-capacity retry (#923).

Run 43904858046 went red on `main` without a step failing: `verify (macos-latest, 1/2)` and
`perf (macos-latest)` were cancelled before their first step because GitHub never acquired a hosted
macOS arm64 runner for them, and `ci-ok` failed because they did not succeed. The reporter now defers
such a red to `.github/workflows/ci-capacity-retry.yml`, which requests one rerun of the run's failed
jobs, and only the attempt the retry budget does not cover files a card.

These tests read the workflow and the two CI files as text, like `test_release_workflow.py`: there is no
YAML parser in the gate's Python, and the assertions are about wiring -- which trigger, which run, which
permission, which budget -- not about a parsed document. The wiring cannot be exercised before it is on
the default branch, so this is the only check the change itself can make.
"""

import importlib.util
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
        loader = SourceFileLoader("ci_issue_triage", str(TRIAGE))
        spec = importlib.util.spec_from_loader(loader.name, loader)
        triage = importlib.util.module_from_spec(spec)
        loader.exec_module(triage)
        ci = text_of(CI)

        self.assertTrue(triage.DERIVED_JOBS, "the predicate must ignore the aggregate and the reporter")
        for name in triage.DERIVED_JOBS:
            self.assertIn(f"name: {name}\n", ci + "\n", f"{name} is not a job in ci.yml")

    def test_the_reporter_defers_at_the_first_attempt_and_files_at_the_budget(self) -> None:
        reporter = text_of(REPORTER)

        self.assertIn("RETRY_BUDGET=2", reporter)
        self.assertIn('[ "$attempt" -lt "$RETRY_BUDGET" ]', reporter)


if __name__ == "__main__":
    unittest.main()
