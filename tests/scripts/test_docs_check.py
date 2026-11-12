import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
CHECKER = REPOSITORY_ROOT / "scripts" / "docs-check"


class DocsCheckTests(unittest.TestCase):
    def run_checker(self, *arguments: str) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            [sys.executable, str(CHECKER), *arguments],
            capture_output=True,
            text=True,
            check=False,
        )

    def test_the_checker_self_test_passes(self) -> None:
        result = self.run_checker("--self-test")

        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_the_repository_docs_pass(self) -> None:
        result = self.run_checker("--check")

        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_a_heading_inside_a_fenced_block_is_not_an_anchor(self) -> None:
        manifest = {
            "navigation": [
                {
                    "title": "Get started",
                    "items": [
                        {"title": "Start", "path": "start.md"},
                        {"title": "Next", "path": "next.md"},
                    ],
                }
            ],
            "redirects": [],
            "proseOnly": [],
        }
        files = {
            "docs/docs.json": json.dumps(manifest),
            "docs/start.md": "# Start\n\n[next](next.md#ghost-anchor)\n\n"
            "<!-- snippet: src/example.txt -->\n",
            "docs/next.md": '# Next\n\n```bash\necho "\n## ghost-anchor\n"\n```\n\n'
            "<!-- snippet: src/example.txt -->\n",
            "src/example.txt": "example\n",
        }
        with tempfile.TemporaryDirectory(
            prefix="docs-check-anchors.", dir=os.environ.get("TMPDIR") or None
        ) as tmp:
            for relative, text in files.items():
                path = Path(tmp) / relative
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(text, encoding="utf-8", newline="")
            result = self.run_checker("--root", tmp)

        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn("missing anchor 'next.md#ghost-anchor'", result.stderr)

    def test_an_unknown_argument_is_a_usage_error(self) -> None:
        result = self.run_checker("--nonsense")

        self.assertEqual(result.returncode, 2)
        self.assertIn("usage:", result.stderr)

    def test_the_docs_and_platform_profiles_run_the_checker(self) -> None:
        verify = (REPOSITORY_ROOT / "scripts" / "verify.sh").read_text(encoding="utf-8")

        self.assertIn('docs-check" --self-test', verify)
        self.assertGreaterEqual(verify.count('docs-check" --check'), 2)

    def test_the_full_profile_runs_the_runnable_blocks(self) -> None:
        verify = (REPOSITORY_ROOT / "scripts" / "verify.sh").read_text(encoding="utf-8")

        self.assertIn('docs-check" --run', verify)
        self.assertIn("--binary", verify)

    def test_every_always_on_docs_job_runs_the_checker(self) -> None:
        for name in ("process.yml", "tree-checks.yml", "docs-lint.yml"):
            with self.subTest(workflow=name):
                text = (REPOSITORY_ROOT / ".github" / "workflows" / name).read_text(
                    encoding="utf-8"
                )
                self.assertIn("scripts/docs-check --check", text)


if __name__ == "__main__":
    unittest.main()
