"""Pins where the docs-tree guard runs (#929).

`scripts/no-docs-references.sh` is the guard the docs profile leans on: it keeps the docs tree prose
that no build input and no test touches, which is why the cheap profile can skip the build and the
suite. Its own `--self-test` pins the shapes it accepts; these tests pin the step lists that run it.

The docs profile alone is not enough. `scripts/ci-plan.sh` sends every non-prose change to the full
gate, so a code pull request never runs that profile, and #910's `TargetFrameworkTests` line reached
`main` with every gate green. The guard therefore rides the platform checks (`verify.sh --profile full`
and `--profile platform`, which is the CI `platform` job) and the tree-check command list (every push
to `main` and the merge queue).

The assertions read the scripts and the workflow as text: there is no shell parser in the gate's
Python, and the contract is which block runs which command, not a parsed program.
"""

import re
import unittest
from pathlib import Path

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
WORKFLOWS = REPOSITORY_ROOT / ".github" / "workflows"
VERIFY = REPOSITORY_ROOT / "scripts" / "verify.sh"
TREE_CHECKS = REPOSITORY_ROOT / "scripts" / "tree-checks.sh"
CI_WORKFLOW = WORKFLOWS / "ci.yml"

GUARD = "no-docs-references.sh"


def shell_function(text: str, name: str) -> str:
    """One top-level `name() {` … `}` block of a shell script."""
    lines = text.splitlines()
    start = next(
        (index for index, line in enumerate(lines) if line.startswith(f"{name}() {{")), None
    )
    if start is None:
        return ""
    end = next(
        (index for index in range(start + 1, len(lines)) if lines[index] == "}"),
        None,
    )
    if end is None:
        return ""
    return "\n".join(lines[start : end + 1])


def job_block(text: str, job: str) -> str:
    """One job's block of a workflow, from its two-space key to the next two-space key."""
    lines = []
    inside = False
    for line in text.splitlines():
        if line.startswith("#"):
            continue
        if line == f"  {job}:":
            inside = True
            continue
        if inside and re.fullmatch(r"  [A-Za-z0-9_-]+:", line):
            break
        if inside:
            lines.append(line)
    return "\n".join(lines)


class VerifyProfileTests(unittest.TestCase):
    def test_the_docs_profile_runs_the_guard(self) -> None:
        block = shell_function(VERIFY.read_text(encoding="utf-8"), "docs_profile")

        self.assertIn(GUARD, block)

    def test_the_platform_checks_run_the_guard(self) -> None:
        # The platform job is the only job a code pull request runs these docs checks in.
        block = shell_function(VERIFY.read_text(encoding="utf-8"), "platform_checks")

        self.assertIn(GUARD, block)

    def test_the_platform_profile_runs_the_platform_checks(self) -> None:
        block = shell_function(VERIFY.read_text(encoding="utf-8"), "platform_profile")

        self.assertIn("platform_checks", block)


class TreeChecksTests(unittest.TestCase):
    def test_the_tree_checks_run_the_guard(self) -> None:
        script = TREE_CHECKS.read_text(encoding="utf-8")

        self.assertIn(GUARD, script)

    def test_the_tree_checks_name_the_guard(self) -> None:
        # A red run names the rule that broke instead of "the tree checks".
        script = TREE_CHECKS.read_text(encoding="utf-8")

        self.assertIn("docs are not build inputs", script)


class CiTests(unittest.TestCase):
    def test_the_platform_job_runs_the_profile_that_carries_the_guard(self) -> None:
        block = job_block(CI_WORKFLOW.read_text(encoding="utf-8"), "platform")

        self.assertIn("scripts/verify.sh --profile platform", block)

    def test_the_docs_job_runs_the_profile_that_carries_the_guard(self) -> None:
        block = job_block(CI_WORKFLOW.read_text(encoding="utf-8"), "verify-docs")

        self.assertIn("scripts/verify.sh --profile docs", block)

    def test_the_guard_self_test_runs_before_the_plan_decides(self) -> None:
        text = CI_WORKFLOW.read_text(encoding="utf-8")

        self.assertIn(f"bash scripts/{GUARD} --self-test", text)


if __name__ == "__main__":
    unittest.main()
