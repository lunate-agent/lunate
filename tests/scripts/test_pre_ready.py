from __future__ import annotations

import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
SCRIPT = REPOSITORY_ROOT / "scripts" / "pre-ready.py"
SPEC_ARCHIVE = REPOSITORY_ROOT / "scripts" / "check-spec-archive.py"

ARCHIVE = "openspec/changes/archive/2026-11-14-pre-ready-gate/"
CHANGE = "openspec/changes/pre-ready-gate/"


def acceptance(count: int = 4) -> str:
    lines = ["## Acceptance", ""]
    lines.extend(f"- {index}. acceptance check {index}" for index in range(1, count + 1))
    return "\n".join(lines) + "\n"


def answers(count: int = 4, evidence: bool = True) -> str:
    lines = []
    for index in range(1, count + 1):
        proof = " `scripts/pre-ready.py:42`" if evidence else ""
        lines.append(f"{index}. **met** — done{proof}")
    return "\n".join(lines) + "\n"


def green_changed() -> str:
    return "\n".join(
        [
            f"D\t{CHANGE}proposal.md",
            f"A\t{ARCHIVE}proposal.md",
            "M\topenspec/specs/ci-pipeline/spec.md",
            "A\tchanges/847.md",
            "M\tCONTRIBUTING.md",
        ]
    ) + "\n"


def green_head_changes() -> str:
    return "\n".join(
        [
            f"D\t{CHANGE}proposal.md",
            f"A\t{ARCHIVE}proposal.md",
            "M\topenspec/specs/ci-pipeline/spec.md",
        ]
    ) + "\n"


def green_head_tree() -> list[str]:
    return [
        f"{ARCHIVE}proposal.md",
        "openspec/specs/ci-pipeline/spec.md",
        "changes/847.md",
        "CONTRIBUTING.md",
    ]


def green_base_tree() -> list[str]:
    return ["changes/README.md", "CHANGELOG.md", "CONTRIBUTING.md"]


class PreReadyTests(unittest.TestCase):
    def run_script(
        self,
        *,
        changed: str | None = None,
        head_changes: str | None = None,
        head_tree: list[str] | None = None,
        base_tree: list[str] | None = None,
        body: str | None = None,
        comments: list[str] | None = None,
        issues: list[dict] | None = None,
        title: str = "pre-ready as a required CI job",
        branch: str = "wt/issue-805",
        number: int = 847,
        extra: list[str] | None = None,
    ) -> subprocess.CompletedProcess[str]:
        changed = green_changed() if changed is None else changed
        head_changes = green_head_changes() if head_changes is None else head_changes
        head_tree = green_head_tree() if head_tree is None else head_tree
        base_tree = green_base_tree() if base_tree is None else base_tree
        body = f"Closes #805\n\n{answers()}" if body is None else body
        issues = [{"number": 805, "title": "issue", "body": acceptance()}] if issues is None else issues
        comments = comments or []

        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "changed.txt").write_text(changed, encoding="utf-8")
            (root / "head.txt").write_text(head_changes, encoding="utf-8")
            (root / "head-tree.txt").write_text("\n".join(head_tree) + "\n", encoding="utf-8")
            (root / "base-tree.txt").write_text("\n".join(base_tree) + "\n", encoding="utf-8")
            (root / "pr.json").write_text(
                json.dumps(
                    {
                        "number": number,
                        "title": title,
                        "body": body,
                        "head": {"ref": branch},
                    }
                ),
                encoding="utf-8",
            )
            (root / "comments.json").write_text(
                json.dumps([{"body": comment, "created_at": f"2026-11-14T0{i}:00:00Z"} for i, comment in enumerate(comments)]),
                encoding="utf-8",
            )
            issues_dir = root / "issues"
            issues_dir.mkdir()
            for issue in issues:
                (issues_dir / f"{issue['number']}.json").write_text(
                    json.dumps(issue), encoding="utf-8"
                )
            return subprocess.run(
                [
                    sys.executable,
                    str(SCRIPT),
                    "--paths-file",
                    str(root / "changed.txt"),
                    "--head-paths-file",
                    str(root / "head.txt"),
                    "--head-tree-file",
                    str(root / "head-tree.txt"),
                    "--base-tree-file",
                    str(root / "base-tree.txt"),
                    "--pr-json",
                    str(root / "pr.json"),
                    "--comments-json",
                    str(root / "comments.json"),
                    "--issues-dir",
                    str(issues_dir),
                    *(extra or []),
                ],
                capture_output=True,
                text=True,
                check=False,
            )

    def test_a_satisfied_pull_request_passes(self) -> None:
        result = self.run_script()

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn("pre-ready: all four checks passed", result.stdout)
        for index in range(1, 5):
            self.assertIn(f"pre-ready: check {index} (", result.stdout)

    def test_an_unarchived_change_fails(self) -> None:
        result = self.run_script(
            changed=f"A\t{CHANGE}proposal.md\nA\tchanges/847.md\n",
            head_changes=f"A\t{CHANGE}proposal.md\n",
            head_tree=[f"{CHANGE}proposal.md", "changes/847.md"],
        )

        self.assertEqual(1, result.returncode)
        self.assertIn("pre-ready: check 1 (the archive is the last commit): FAILED", result.stderr)
        self.assertIn("openspec/changes/pre-ready-gate/ is not archived", result.stderr)
        self.assertIn("openspec archive pre-ready-gate --yes", result.stderr)

    def test_an_archive_that_is_not_the_last_commit_fails(self) -> None:
        result = self.run_script(
            head_changes="M\tdocs/dev/README.md\n",
        )

        self.assertEqual(1, result.returncode)
        self.assertIn("the archive of pre-ready-gate is not the final commit", result.stderr)

    def test_a_rename_from_the_change_into_the_archive_passes(self) -> None:
        result = self.run_script(
            changed=(
                f"R100\t{CHANGE}proposal.md\t{ARCHIVE}proposal.md\n"
                "M\topenspec/specs/ci-pipeline/spec.md\nA\tchanges/847.md\n"
            ),
            head_changes=(
                f"R100\t{CHANGE}proposal.md\t{ARCHIVE}proposal.md\n"
                "M\topenspec/specs/ci-pipeline/spec.md\n"
            ),
        )

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn("check 1 (the archive is the last commit): ok", result.stdout)

    def test_a_missing_fragment_fails(self) -> None:
        result = self.run_script(changed=green_changed().replace("A\tchanges/847.md\n", ""))

        self.assertEqual(1, result.returncode)
        self.assertIn("pre-ready: check 2 (a changelog fragment exists): FAILED", result.stderr)
        self.assertIn("changes/847.md is missing", result.stderr)

    def test_editing_the_changelog_directly_fails(self) -> None:
        result = self.run_script(changed=green_changed() + "M\tCHANGELOG.md\n")

        self.assertEqual(1, result.returncode)
        self.assertIn("CHANGELOG.md was edited", result.stderr)
        self.assertIn("Write the release-note line in changes/847.md", result.stderr)

    def test_a_hand_edited_spec_fails(self) -> None:
        result = self.run_script(
            changed="M\topenspec/specs/ci-pipeline/spec.md\nA\tchanges/847.md\n",
            head_changes="M\topenspec/specs/ci-pipeline/spec.md\n",
            head_tree=["openspec/specs/ci-pipeline/spec.md", "changes/847.md"],
        )

        self.assertEqual(1, result.returncode)
        self.assertIn("pre-ready: check 3 (no hand-edited spec): FAILED", result.stderr)
        self.assertIn("spec edited by hand", result.stderr)

    def test_an_unanswered_acceptance_check_fails(self) -> None:
        result = self.run_script(body=f"Closes #805\n\n{answers(3)}")

        self.assertEqual(1, result.returncode)
        self.assertIn("pre-ready: check 4 (every acceptance check is answered by number): FAILED", result.stderr)
        self.assertIn("acceptance check 4 of #805 is unanswered", result.stderr)

    def test_a_not_met_acceptance_check_fails(self) -> None:
        body = answers().replace("2. **met**", "2. **not met**")
        result = self.run_script(body=f"Closes #805\n\n{body}")

        self.assertEqual(1, result.returncode)
        self.assertIn("acceptance check 2 of #805 is not met", result.stderr)

    def test_a_met_answer_without_evidence_fails(self) -> None:
        result = self.run_script(body=f"Closes #805\n\n{answers(evidence=False)}")

        self.assertEqual(1, result.returncode)
        self.assertIn("acceptance check 1 of #805 says met but carries no evidence", result.stderr)

    def test_answers_in_a_comment_pass(self) -> None:
        result = self.run_script(body="Closes #805\n", comments=[answers()])

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn("check 4 (every acceptance check is answered by number): ok", result.stdout)

    def test_check_prefixed_and_table_answers_are_read(self) -> None:
        prefixed = "\n".join(
            f"Check {index}: **met** — `scripts/pre-ready.py:{index}`"
            for index in range(1, 5)
        )
        result = self.run_script(body="Closes #805\n", comments=[prefixed])
        self.assertEqual(0, result.returncode, result.stderr)

        table = "\n".join(
            ["| # | Status | Evidence |", "| --- | --- | --- |"]
            + [f"| {index} | met | `scripts/pre-ready.py:{index}` |" for index in range(1, 5)]
        )
        result = self.run_script(body="Closes #805\n", comments=[table])
        self.assertEqual(0, result.returncode, result.stderr)

    def test_an_issue_linked_from_the_title_is_read(self) -> None:
        result = self.run_script(title="pre-ready as a required CI job (#805)", body=answers())

        self.assertEqual(0, result.returncode, result.stderr)

    def test_no_linked_issue_fails(self) -> None:
        result = self.run_script(body=answers())

        self.assertEqual(1, result.returncode)
        self.assertIn("no issue is linked", result.stderr)

    def test_the_release_pull_request_needs_no_fragment(self) -> None:
        result = self.run_script(
            branch="release/0.4.0",
            changed=f"D\t{CHANGE}proposal.md\nA\t{ARCHIVE}proposal.md\nM\tCHANGELOG.md\n",
        )

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn("release pull request", result.stdout)

    def test_the_fragment_rule_is_inert_before_the_directory_lands(self) -> None:
        result = self.run_script(
            base_tree=["CHANGELOG.md"],
            changed=f"D\t{CHANGE}proposal.md\nA\t{ARCHIVE}proposal.md\n",
        )

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn("inert: changes/README.md is not on the base branch", result.stdout)

    def test_the_spec_archive_rule_agrees_with_check_spec_archive(self) -> None:
        cases = {
            "spec only": ("M\topenspec/specs/ci-pipeline/spec.md\n", "openspec/specs/ci-pipeline/spec.md\n"),
            "spec with archive": (
                "M\topenspec/specs/ci-pipeline/spec.md\nA\topenspec/changes/archive/2026-11-14-x/proposal.md\n",
                "openspec/specs/ci-pipeline/spec.md\nopenspec/changes/archive/2026-11-14-x/proposal.md\n",
            ),
        }
        for name, (changed, reference_paths) in cases.items():
            with self.subTest(name):
                with tempfile.NamedTemporaryFile(
                    "w", suffix=".txt", delete=False, encoding="utf-8"
                ) as handle:
                    handle.write(reference_paths)
                    paths = handle.name
                try:
                    reference = subprocess.run(
                        [sys.executable, str(SPEC_ARCHIVE), "--paths-file", paths],
                        capture_output=True,
                        text=True,
                        check=False,
                    )
                finally:
                    Path(paths).unlink(missing_ok=True)
                result = self.run_script(changed=changed)
                self.assertEqual(reference.returncode == 0, "check 3 (no hand-edited spec): ok" in result.stdout)


if __name__ == "__main__":
    unittest.main()
