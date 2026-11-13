"""Pins the release-side integration of the changelog-fragment contract (#794).

The guard and the assembler prove the rule inside a pull request. The release pull request itself is
proven at the tag: the Release workflow refuses a tag whose commit is not on `main`, requires the
commit to be a merged release pull request, and reads the published release back after attaching its
assets. The lane's own release and merge execution is outside this repository and tracked in
lunate-lane#21; the repository-side checks fail a tag where either action did not happen.

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
PULL_STEP = "- name: The tag is a merged release pull request"
RELEASE_STEP = "- name: The release exists with its assets"


def without_comments(text: str) -> str:
    return "\n".join(line for line in text.splitlines() if not line.lstrip().startswith("#"))


def step_block(text: str, name: str = STEP) -> str:
    lines = text.splitlines()
    for index, line in enumerate(lines):
        if line.strip() == name:
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

    def test_the_tag_must_come_from_a_merged_release_pull_request(self) -> None:
        text = without_comments(WORKFLOW.read_text(encoding="utf-8"))
        block = step_block(text, PULL_STEP)

        self.assertIn("--pulls-file associated-pulls.json", block)
        self.assertIn("repos/${GH_REPO}/commits/${sha}/pulls", block)
        self.assertIn("if: startsWith(github.ref, 'refs/tags/')", block)
        self.assertLess(text.index(PULL_STEP), text.index("- name: Changelog is assembled from the fragments"))

    def test_the_published_release_is_read_back(self) -> None:
        block = step_block(without_comments(WORKFLOW.read_text(encoding="utf-8")), RELEASE_STEP)

        self.assertIn('gh release view "${GITHUB_REF_NAME}" --json tagName,isDraft,assets', block)
        self.assertIn("--release-file release.json", block)
        self.assertNotIn("continue-on-error", block)

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
        self.assertIn("lunate-lane#21", text)
        self.assertIn("scripts/release-contract", text)
        self.assertIn("git merge-base --is-ancestor <tag> origin/main", text)
        self.assertIn("gh release view", text)


if __name__ == "__main__":
    unittest.main()
