"""Pins the release-side half of the changelog-fragment contract (#794).

The guard and the assembler prove the rule inside a pull request. The release pull request itself is
proven only at the tag: the Release workflow refuses a tag whose commit is not on `main`, so the
release pull request that assembled the changelog must have been merged. The lane's own release and
merge execution is outside this repository, so `docs/distribution.md` states that contract and the
command that verifies a release against it.

These tests read `.github/workflows/release.yml` and the runbook as text. There is no YAML parser in
the gate's Python, and the assertions are about wiring -- which event, which command, whether a
failure can reach the check -- not about a parsed document.
"""

import unittest
from pathlib import Path

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
WORKFLOW = REPOSITORY_ROOT / ".github" / "workflows" / "release.yml"
RUNBOOK = REPOSITORY_ROOT / "docs" / "distribution.md"
STEP = "- name: The release pull request is merged into main"


def without_comments(text: str) -> str:
    return "\n".join(line for line in text.splitlines() if not line.lstrip().startswith("#"))


def step_block(text: str) -> str:
    lines = text.splitlines()
    for index, line in enumerate(lines):
        if line.strip() == STEP:
            block = [line]
            for following in lines[index + 1 :]:
                if following.startswith("      - name:"):
                    break
                block.append(following)
            return "\n".join(block)
    return ""


class ReleaseWorkflowTests(unittest.TestCase):
    def test_a_tag_must_be_on_main(self) -> None:
        block = step_block(without_comments(WORKFLOW.read_text(encoding="utf-8")))

        self.assertIn("git merge-base --is-ancestor HEAD origin/main", block)

    def test_the_guard_runs_on_tags_before_the_changelog_check(self) -> None:
        text = without_comments(WORKFLOW.read_text(encoding="utf-8"))
        block = step_block(text)

        self.assertIn("if: startsWith(github.ref, 'refs/tags/')", block)
        self.assertLess(text.index(STEP), text.index("- name: Changelog is assembled from the fragments"))

    def test_the_failure_names_the_release_pull_request_and_cannot_be_swallowed(self) -> None:
        block = step_block(without_comments(WORKFLOW.read_text(encoding="utf-8")))

        self.assertIn("Merge the release pull request before tagging", block)
        self.assertNotIn("continue-on-error", block)
        self.assertNotIn("|| true", block)

    def test_the_lane_contract_and_its_verification_are_documented(self) -> None:
        text = RUNBOOK.read_text(encoding="utf-8")

        self.assertIn("release/<version>", text)
        self.assertIn("lane releases it and merges it", text)
        self.assertIn("git merge-base --is-ancestor <tag> origin/main", text)
        self.assertIn("gh release view", text)


if __name__ == "__main__":
    unittest.main()
