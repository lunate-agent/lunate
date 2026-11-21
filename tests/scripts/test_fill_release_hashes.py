from __future__ import annotations

import importlib.machinery
import importlib.util
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
SCRIPT = REPOSITORY_ROOT / "scripts" / "fill-release-hashes"
CHECKER = REPOSITORY_ROOT / "scripts" / "check-channel-manifests.py"
DOCS_SYNC = REPOSITORY_ROOT / "scripts" / "docs-sync"
FIXTURE = REPOSITORY_ROOT / "tests" / "fixtures" / "releases" / "v0.4.0"
SUMS = FIXTURE / "SHA256SUMS"
RUNBOOK = REPOSITORY_ROOT / "docs" / "dev" / "distribution.md"

HOME_BREW = "packaging/homebrew/lunate.rb"
SCOOP = "packaging/scoop/lunate.json"
WINGET_VERSION = "packaging/winget/wdaniel1993.lunate.yaml"
WINGET_INSTALLER = "packaging/winget/wdaniel1993.lunate.installer.yaml"
WINGET_LOCALE = "packaging/winget/wdaniel1993.lunate.locale.en-US.yaml"
MANIFESTS = (HOME_BREW, SCOOP, WINGET_VERSION, WINGET_INSTALLER, WINGET_LOCALE)
ASSETS = ("lunate-osx-arm64.tar.gz", "lunate-linux-x64.tar.gz", "lunate-win-x64.zip")

sys.path.insert(0, str(REPOSITORY_ROOT / "scripts"))
import published_release  # noqa: E402


def write(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(text)


def run(root: Path, *arguments: str) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [sys.executable, str(SCRIPT), "--root", str(root), *arguments],
        capture_output=True,
        text=True,
        check=False,
    )


def snapshot(root: Path) -> dict[str, bytes]:
    return {relative: (root / relative).read_bytes() for relative in MANIFESTS}


def sums_without(asset: str) -> str:
    lines = [
        line
        for line in SUMS.read_text(encoding="utf-8").splitlines()
        if line.split()[-1] != asset
    ]
    return "\n".join(lines) + "\n"


def load_docs_sync():
    loader = importlib.machinery.SourceFileLoader("docs_sync_fill_release_hashes", str(DOCS_SYNC))
    spec = importlib.util.spec_from_loader(loader.name, loader)
    module = importlib.util.module_from_spec(spec)
    loader.exec_module(module)
    return module


class FillReleaseHashesTests(unittest.TestCase):
    def setUp(self) -> None:
        self.trees: list[Path] = []

    def tearDown(self) -> None:
        for tree in self.trees:
            shutil.rmtree(tree, ignore_errors=True)

    def tree(self, source: str = "before") -> Path:
        root = Path(tempfile.mkdtemp(prefix="fill-release-hashes-"))
        self.trees.append(root)
        shutil.copytree(FIXTURE / source, root, dirs_exist_ok=True)
        return root

    def sums(self, text: str) -> Path:
        path = Path(tempfile.mkdtemp(prefix="fill-release-sums-"))
        self.trees.append(path)
        write(path / "SHA256SUMS", text)
        return path / "SHA256SUMS"

    def test_the_0_4_0_sums_reproduce_the_868_commit(self) -> None:
        root = self.tree()

        result = run(root, "v0.4.0", "--sums", str(SUMS))

        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        for relative in MANIFESTS:
            self.assertEqual(
                (FIXTURE / "expected" / relative).read_bytes(),
                (root / relative).read_bytes(),
                relative,
            )

    def test_the_filled_manifests_pass_the_channel_guard(self) -> None:
        root = self.tree()
        run(root, "v0.4.0", "--sums", str(SUMS))

        guard = subprocess.run(
            [
                sys.executable,
                str(CHECKER),
                "--root",
                str(root),
                "--tag",
                "v0.4.0",
                "--sums",
                str(SUMS),
            ],
            capture_output=True,
            text=True,
            check=False,
        )

        self.assertEqual(0, guard.returncode, guard.stdout + guard.stderr)
        self.assertIn("channel manifests: OK (v0.4.0)", guard.stdout)

    def test_docs_sync_updates_the_readme_after_the_fill(self) -> None:
        root = self.tree()
        self.prepare_docs(root)
        run(root, "v0.4.0", "--sums", str(SUMS))

        docs_sync = load_docs_sync()

        def fetch(tag: str) -> dict[str, str]:
            if tag != "v0.4.0":
                raise published_release.ReleaseMissing(f"{tag} has no release asset")
            return published_release.parse_sums(SUMS.read_text(encoding="utf-8"))

        status = docs_sync.run(root, True, fetch)

        self.assertEqual(0, status)
        readme = (root / "README.md").read_text(encoding="utf-8")
        self.assertIn(
            "The current stable release is [**0.4.0**]"
            "(https://github.com/lunate-agent/lunate/releases/tag/v0.4.0).",
            readme,
        )
        self.assertNotIn("[**0.3.0**]", readme)

    def test_a_second_run_changes_nothing(self) -> None:
        root = self.tree()
        run(root, "v0.4.0", "--sums", str(SUMS))
        first = snapshot(root)

        second = run(root, "v0.4.0", "--sums", str(SUMS))

        self.assertEqual(0, second.returncode, second.stderr)
        self.assertIn("unchanged", second.stdout)
        self.assertEqual(first, snapshot(root))

    def test_a_missing_asset_is_refused_by_name_and_writes_nothing(self) -> None:
        for asset in ASSETS:
            with self.subTest(asset=asset):
                root = self.tree()
                before = snapshot(root)
                sums = self.sums(sums_without(asset))

                result = run(root, "v0.4.0", "--sums", str(sums))

                self.assertEqual(1, result.returncode, result.stdout + result.stderr)
                self.assertIn(asset, result.stderr)
                self.assertEqual(before, snapshot(root))

    def test_a_moved_manifest_shape_is_refused_by_name_and_writes_nothing(self) -> None:
        cases = (
            (HOME_BREW, 'sha256 "', 'sha "'),
            (SCOOP, '"hash"', '"digest"'),
            (WINGET_INSTALLER, "InstallerSha256:", "InstallerHash:"),
            (WINGET_VERSION, "PackageVersion:", "PackageRelease:"),
            (WINGET_LOCALE, "PackageVersion:", "PackageRelease:"),
        )
        for relative, old, new in cases:
            with self.subTest(manifest=relative):
                root = self.tree()
                text = (root / relative).read_text(encoding="utf-8")
                write(root / relative, text.replace(old, new))
                before = snapshot(root)

                result = run(root, "v0.4.0", "--sums", str(SUMS))

                self.assertEqual(1, result.returncode, result.stdout + result.stderr)
                self.assertIn(relative, result.stderr)
                self.assertEqual(before, snapshot(root))

    def test_a_duplicated_line_is_refused(self) -> None:
        root = self.tree()
        path = root / WINGET_INSTALLER
        write(path, path.read_text(encoding="utf-8") + "PackageVersion: 9.9.9\n")
        before = snapshot(root)

        result = run(root, "v0.4.0", "--sums", str(SUMS))

        self.assertEqual(1, result.returncode, result.stdout + result.stderr)
        self.assertIn(WINGET_INSTALLER, result.stderr)
        self.assertIn("found 2", result.stderr)
        self.assertEqual(before, snapshot(root))

    def test_a_duplicated_scoop_key_is_refused(self) -> None:
        root = self.tree()
        path = root / SCOOP
        original = path.read_text(encoding="utf-8")
        needle = '            "hash": "'
        duplicate = '            "hash": "' + "0" * 64 + '",\n'
        write(path, original.replace(needle, duplicate + needle, 1))
        before = snapshot(root)

        result = run(root, "v0.4.0", "--sums", str(SUMS))

        self.assertEqual(1, result.returncode, result.stdout + result.stderr)
        self.assertIn(SCOOP, result.stderr)
        self.assertIn("duplicate", result.stderr)
        self.assertIn('"hash"', result.stderr)
        self.assertEqual(before, snapshot(root))

    def test_a_malformed_checksum_file_is_refused(self) -> None:
        root = self.tree()
        before = snapshot(root)
        sums = self.sums(
            "not-a-sha256  lunate-osx-arm64.tar.gz\n"
            "d0ad6cf1fc2d324a21fa2c3f4979dbb0c6fac9ec2be714a2b998c5132fd7feed  "
            "lunate-linux-x64.tar.gz\n"
        )

        result = run(root, "v0.4.0", "--sums", str(sums))

        self.assertEqual(1, result.returncode, result.stdout + result.stderr)
        self.assertIn("line 1", result.stderr)
        self.assertEqual(before, snapshot(root))

    def test_an_unreadable_checksum_file_is_refused(self) -> None:
        root = self.tree()
        before = snapshot(root)

        result = run(root, "v0.4.0", "--sums", str(root / "SHA256SUMS"))

        self.assertEqual(1, result.returncode, result.stdout + result.stderr)
        self.assertIn("cannot read --sums", result.stderr)
        self.assertEqual(before, snapshot(root))

    def test_only_a_stable_tag_is_filled(self) -> None:
        for tag in ("0.4.0", "v0.4", "v0.4.0-rc.1"):
            with self.subTest(tag=tag):
                root = self.tree()
                before = snapshot(root)

                result = run(root, tag, "--sums", str(SUMS))

                self.assertEqual(2, result.returncode, result.stdout + result.stderr)
                self.assertIn("stable release tag", result.stderr)
                self.assertEqual(before, snapshot(root))

    def test_a_local_checksum_file_is_the_only_input(self) -> None:
        root = self.tree()

        result = run(root, "v9.9.9", "--sums", str(SUMS))

        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertIn("/download/v9.9.9/lunate-osx-arm64.tar.gz", (root / HOME_BREW).read_text())

    def test_the_runbook_names_the_command(self) -> None:
        text = RUNBOOK.read_text(encoding="utf-8")
        step = text.split("### 4. Fill the channel manifests", 1)[1].split("### 5.", 1)[0]

        self.assertIn("scripts/fill-release-hashes v<version> --sums", step)
        self.assertIn("scripts/docs-sync --write", step)

    def prepare_docs(self, root: Path) -> None:
        """The pages docs-sync needs, with a README that still carries 0.3.0."""
        for relative in (
            "src/Lunate.Tui/keys.json",
            "src/Lunate.Coding/settings-schema.json",
            "docs/keybindings.md",
            "docs/settings.md",
        ):
            write(root / relative, (REPOSITORY_ROOT / relative).read_text(encoding="utf-8"))
        write(
            root / "README.md",
            "# Lunate\n\n<!-- docs-sync: version -->\n"
            "The current stable release is [**0.3.0**]"
            "(https://github.com/lunate-agent/lunate/releases/tag/v0.3.0).\n"
            "<!-- /docs-sync: version -->\n",
        )
        # docs-sync derives the release line from the tree's own tags.
        subprocess.run(["git", "init", "-q"], cwd=root, check=True)
        subprocess.run(
            [
                "git",
                "-c",
                "user.name=fill-test",
                "-c",
                "user.email=fill-test@example.com",
                "commit",
                "--allow-empty",
                "-q",
                "-m",
                "scratch",
            ],
            cwd=root,
            check=True,
        )
        subprocess.run(["git", "tag", "v0.4.0"], cwd=root, check=True)


if __name__ == "__main__":
    unittest.main()
