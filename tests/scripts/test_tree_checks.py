"""Pins the wiring of the main-push tree checks (#740).

The cheap repository-wide checks -- ADR numbering, the spec-archive rule, the generated docs regions
and the docs lint -- ran only where a pull request selected the docs profile, plus the always-on
`Process` job's region check. A docs-only push to main skips `ci.yml` entirely (its `paths-ignore`)
and `verify-docs` is only selected for a *docs pull request*, so a defect in one of these checks
stayed invisible on main until the first pull request that touched documentation: `#738`
(`check-adr-numbers.py` failing on main) survived exactly that way.

These tests read `.github/workflows/tree-checks.yml` as text. There is no YAML parser in the gate's
Python, and the assertions are about wiring -- which event, which commands, whether a failure can
reach the check -- not about a parsed document.
"""

import re
import unittest
from pathlib import Path

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
WORKFLOW = REPOSITORY_ROOT / ".github" / "workflows" / "tree-checks.yml"

# The four checks the card names, by the command line that runs each one.
CHECKS = {
    "ADR numbering": "scripts/check-adr-numbers.py",
    "spec archive": "scripts/check-spec-archive.py",
    "generated docs regions": "scripts/docs-sync --check",
    "docs lint": "markdownlint-cli2",
}


def workflow_text() -> str:
    return WORKFLOW.read_text(encoding="utf-8")


def without_comments(text: str) -> str:
    return "\n".join(line for line in text.splitlines() if not line.lstrip().startswith("#"))


def event_block(text: str) -> str:
    """The `on:` block of a workflow, without its comments."""
    lines = []
    inside = False
    for line in without_comments(text).splitlines():
        if line == "on:":
            inside = True
            continue
        if inside and line and not line.startswith(" "):
            break
        if inside:
            lines.append(line)
    return "\n".join(lines)


class TreeChecksWorkflowTests(unittest.TestCase):
    def test_every_push_to_main_runs_the_checks(self) -> None:
        events = event_block(workflow_text())

        self.assertIn("push:", events)
        self.assertIn("branches: [main]", events)

    def test_no_path_filter_can_skip_a_push_to_main(self) -> None:
        # The whole point: a docs-only push is exactly the push that used to run nothing.
        events = event_block(workflow_text())

        self.assertNotIn("paths", events)

    def test_the_checks_do_not_become_a_pull_request_trigger(self) -> None:
        # A pull request's behaviour stays as it is (verify-docs or the Process job); the new
        # trigger is the push to main only.
        text = without_comments(workflow_text())

        self.assertNotIn("pull_request", text)

    def test_all_four_cheap_checks_run(self) -> None:
        text = without_comments(workflow_text())

        for name, command in CHECKS.items():
            with self.subTest(check=name):
                self.assertIn(command, text)

    def test_a_failing_check_fails_the_job(self) -> None:
        # A check that cannot fail is the defect being fixed, so no step may swallow its exit code.
        text = without_comments(workflow_text())

        self.assertNotIn("continue-on-error", text)
        self.assertNotIn("|| true", text)

    def test_the_job_stays_cheap(self) -> None:
        # Minutes, not the verify suite: no .NET, no build, no test, no full profile.
        text = without_comments(workflow_text())

        for expensive in ("verify.sh", "setup-dotnet", "dotnet build", "dotnet test", "dotnet publish"):
            with self.subTest(expensive=expensive):
                self.assertNotIn(expensive, text)

    def test_the_checkout_carries_the_pushed_range_and_the_tags(self) -> None:
        # check-spec-archive.py diffs the pushed range against github.event.before, which a shallow
        # clone lacks; docs-sync's version region reads the release tags, which it does not fetch.
        text = without_comments(workflow_text())

        self.assertIn("fetch-depth: 0", text)
        self.assertIn("fetch-tags: true", text)
        self.assertIn("github.event.before", text)

    def test_a_main_push_is_never_cancelled(self) -> None:
        # A newer push must not cancel the verdict an older one is still producing (#705).
        text = without_comments(workflow_text())

        self.assertRegex(text, re.compile(r"^concurrency:", re.MULTILINE))
        self.assertIn("group: tree-checks-${{ github.ref }}", text)
        self.assertIn("cancel-in-progress: false", text)

    def test_the_job_is_read_only(self) -> None:
        text = without_comments(workflow_text())

        self.assertIn("permissions:", text)
        self.assertIn("contents: read", text)


if __name__ == "__main__":
    unittest.main()
