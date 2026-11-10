import subprocess
import sys
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
CHECKER = REPOSITORY_ROOT / "scripts" / "check-channel-version.py"


class ChannelVersionTests(unittest.TestCase):
    def run_checker(
        self, channel: str, tag: str, observed: str
    ) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            [
                sys.executable,
                str(CHECKER),
                "--channel",
                channel,
                "--tag",
                tag,
                "--observed",
                observed,
            ],
            capture_output=True,
            text=True,
            check=False,
        )

    def test_a_stable_channel_a_release_behind_fails_naming_the_channel_and_version(self):
        # The Scoop bucket served 0.1.0 while the tag was 0.3.0. The binary's first-run hint on
        # the second line is what defeated the old PowerShell check, so it stays in the fixture.
        result = self.run_checker(
            "Scoop", "v0.3.0", "0.1.0+92fe1e5\nhome D:/a/_temp/lunate-home (LUNATE_HOME)"
        )

        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        output = result.stdout + result.stderr
        self.assertIn("Scoop serves 0.1.0, expected 0.3.0", output)
        self.assertIn("release behind", output)

    def test_a_stable_channel_at_the_tag_passes(self):
        result = self.run_checker(
            "Homebrew", "v0.3.0", "0.3.0+a8af348\nhome D:/a/_temp/lunate-home (LUNATE_HOME)"
        )

        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn("Homebrew serves 0.3.0+a8af348, the tag", result.stdout)

    def test_a_prerelease_leaves_the_stable_channel_and_is_reported(self):
        result = self.run_checker("Scoop", "v0.3.0-rc.2", "0.2.1+abc123")

        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn("stable channel", result.stdout)
        self.assertIn("stays at 0.2.1", result.stdout)

    def test_a_prerelease_does_not_satisfy_a_stable_tag(self):
        result = self.run_checker("winget", "v0.3.0", "0.3.0-rc.1+abc123")

        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn("winget serves 0.3.0-rc.1, expected 0.3.0", result.stderr)

    def test_output_without_a_version_fails_loudly(self):
        result = self.run_checker("winget", "v0.3.0", "no version here")

        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn("winget", result.stderr)
        self.assertIn("printed no version", result.stderr)

    def test_an_invalid_tag_is_rejected(self):
        result = self.run_checker("Homebrew", "0.3.0", "0.3.0")

        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn("is not a release tag", result.stderr)


if __name__ == "__main__":
    unittest.main()
