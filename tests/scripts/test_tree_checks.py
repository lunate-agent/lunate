"""Pins the wiring of the cheap tree checks (#740, #854).

The checks -- ADR numbering, the spec-archive rule, the generated docs regions, the docs
link/navigation check and the docs lint -- need no build and no test. They run from two triggers:

- a push to `main`, because a docs-only push skips `ci.yml` entirely (its `paths-ignore`) and
  `verify-docs` is selected only for a *docs pull request*, so a defect in one of them stayed
  invisible on main until the first pull request that touched documentation. That is how `#738`
  (`check-adr-numbers.py` failing on main) survived; and
- the merge queue, because the batch is the first tree on which two pull requests meet. #840 took a
  free ADR-0036 and #843's full-profile gate never asked about ADR numbers, so the batch that
  combined them carried a duplicate and nothing in the queue ran the check that catches it (#854).

`scripts/tree-checks.sh` holds the one command list both triggers call, so they cannot drift.

These tests read the files as text. There is no YAML parser in the gate's Python, and the assertions
are about wiring -- which event, which base, which commands, whether a failure can reach the check --
not about a parsed document.
"""

import re
import unittest
from pathlib import Path

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
WORKFLOWS = REPOSITORY_ROOT / ".github" / "workflows"
PUSH_WORKFLOW = WORKFLOWS / "tree-checks.yml"
CI_WORKFLOW = WORKFLOWS / "ci.yml"
SCRIPT = REPOSITORY_ROOT / "scripts" / "tree-checks.sh"

# The checks the script runs, by the command line that runs each one.
CHECKS = {
    "ADR numbering": "scripts/check-adr-numbers.py",
    "spec changes are archived": "scripts/check-spec-archive.py",
    "generated docs regions": "scripts/docs-sync --check",
    "docs links and navigation": "scripts/docs-check --check",
    "docs lint": "markdownlint-cli2",
}

# Commands that would stop the job being cheap: minutes, not seconds.
EXPENSIVE = ("verify.sh", "setup-dotnet", "dotnet build", "dotnet test", "dotnet publish")


def text_of(path: Path) -> str:
    return path.read_text(encoding="utf-8")


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


def job_block(text: str, job: str) -> str:
    """One job's block, from its two-space key to the next two-space key."""
    lines = []
    inside = False
    for line in without_comments(text).splitlines():
        if line == f"  {job}:":
            inside = True
            continue
        if inside and re.fullmatch(r"  [A-Za-z0-9_-]+:", line):
            break
        if inside:
            lines.append(line)
    return "\n".join(lines)


class PushToMainTests(unittest.TestCase):
    def test_every_push_to_main_runs_the_checks(self) -> None:
        events = event_block(text_of(PUSH_WORKFLOW))

        self.assertIn("push:", events)
        self.assertIn("branches: [main]", events)
        self.assertIn("scripts/tree-checks.sh", without_comments(text_of(PUSH_WORKFLOW)))

    def test_no_path_filter_can_skip_a_push_to_main(self) -> None:
        # The whole point: a docs-only push is exactly the push that used to run nothing.
        events = event_block(text_of(PUSH_WORKFLOW))

        self.assertNotIn("paths", events)

    def test_the_checks_do_not_become_a_pull_request_trigger(self) -> None:
        # A pull request's behaviour stays as it is (verify-docs or the Process job); the queue is
        # the other trigger, and it lives in ci.yml.
        text = without_comments(text_of(PUSH_WORKFLOW))

        self.assertNotIn("pull_request", text)

    def test_a_failing_check_fails_the_job(self) -> None:
        # A check that cannot fail is the defect being fixed, so no step may swallow its exit code.
        text = without_comments(text_of(PUSH_WORKFLOW))

        self.assertNotIn("continue-on-error", text)
        self.assertNotIn("|| true", text)

    def test_the_job_stays_cheap(self) -> None:
        # Seconds, not the verify suite: no .NET, no build, no test, no full profile.
        text = without_comments(text_of(PUSH_WORKFLOW))

        for expensive in EXPENSIVE:
            with self.subTest(expensive=expensive):
                self.assertNotIn(expensive, text)

    def test_the_checkout_carries_the_pushed_range_and_the_tags(self) -> None:
        # check-spec-archive.py diffs the pushed range against github.event.before, which a shallow
        # clone lacks; docs-sync's version region reads the release tags, which it does not fetch.
        text = without_comments(text_of(PUSH_WORKFLOW))

        self.assertIn("fetch-depth: 0", text)
        self.assertIn("fetch-tags: true", text)
        self.assertIn("github.event.before", text)

    def test_a_main_push_is_never_cancelled(self) -> None:
        # A newer push must not cancel the verdict an older one is still producing (#705).
        text = without_comments(text_of(PUSH_WORKFLOW))

        self.assertRegex(text, re.compile(r"^concurrency:", re.MULTILINE))
        self.assertIn("group: tree-checks-${{ github.ref }}", text)
        self.assertIn("cancel-in-progress: false", text)

    def test_the_job_is_read_only(self) -> None:
        text = without_comments(text_of(PUSH_WORKFLOW))

        self.assertIn("permissions:", text)
        self.assertIn("contents: read", text)


class MergeQueueTests(unittest.TestCase):
    """The queue's job, which ci-ok requires: a batch with a duplicate ADR number must be red."""

    def merge_group_job(self) -> str:
        block = job_block(text_of(CI_WORKFLOW), "tree-checks")
        self.assertTrue(block, "ci.yml has no `tree-checks` job")
        return block

    def test_the_merge_queue_runs_the_tree_checks(self) -> None:
        block = self.merge_group_job()

        self.assertIn("if: github.event_name == 'merge_group'", block)

    def test_the_merge_queue_job_does_not_run_elsewhere(self) -> None:
        # A pull request already runs the checks its profile carries, and a push to main has
        # tree-checks.yml (a docs-only push skips ci.yml entirely).
        block = self.merge_group_job()

        self.assertNotIn("pull_request", block)

    def test_the_merge_queue_runs_the_shared_script(self) -> None:
        # One command list: the push to main and the queue cannot drift.
        block = self.merge_group_job()

        self.assertIn("scripts/tree-checks.sh", block)

    def test_the_merge_queue_judges_the_batch_against_its_base(self) -> None:
        # The batch would land on merge_group.base_sha, so that is the range it must be judged on.
        block = self.merge_group_job()

        self.assertIn("github.event.merge_group.base_sha", block)

    def test_the_checkout_carries_the_batch_and_the_tags(self) -> None:
        block = self.merge_group_job()

        self.assertIn("fetch-depth: 0", block)
        self.assertIn("fetch-tags: true", block)

    def test_a_failing_check_fails_the_queue(self) -> None:
        block = self.merge_group_job()

        self.assertNotIn("continue-on-error", block)
        self.assertNotIn("|| true", block)

    def test_the_queue_job_stays_cheap(self) -> None:
        block = self.merge_group_job()

        for expensive in EXPENSIVE:
            with self.subTest(expensive=expensive):
                self.assertNotIn(expensive, block)


class TreeChecksScriptTests(unittest.TestCase):
    def test_all_cheap_checks_run(self) -> None:
        text = text_of(SCRIPT)

        for name, command in CHECKS.items():
            with self.subTest(check=name):
                self.assertIn(command, text)

    def test_the_script_stops_at_the_first_failure(self) -> None:
        text = text_of(SCRIPT)

        self.assertIn("set -euo pipefail", text)
        self.assertNotIn("|| true", text)
        self.assertNotIn("continue-on-error", text)

    def test_the_script_takes_the_base_for_the_spec_archive_rule(self) -> None:
        text = text_of(SCRIPT)

        self.assertIn("--base", text)
        self.assertIn("check-spec-archive.py", text)

    def test_the_script_names_every_check(self) -> None:
        # A red run names the rule that broke instead of "the tree checks".
        text = text_of(SCRIPT)

        for name in CHECKS:
            with self.subTest(check=name):
                self.assertIn(name, text)


if __name__ == "__main__":
    unittest.main()
