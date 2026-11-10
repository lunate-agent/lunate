"""Pins the one definition of the newest published stable release (#723).

`scripts/docs-sync` and `scripts/check-channel-manifests.py` both ask
`scripts/published_release.py`. The release exists exactly when its `SHA256SUMS` asset resolves, so
a stable tag whose release workflow failed before attaching assets is skipped, and the previous
published release stays current until the release actually exists.
"""

import sys
import unittest
from pathlib import Path

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPOSITORY_ROOT / "scripts"))

import published_release  # noqa: E402  (the scripts directory is on sys.path above)

SUMS = {"lunate-osx-arm64.tar.gz": "a" * 64}


def missing(tag: str) -> dict[str, str]:
    raise published_release.ReleaseMissing(f"{tag} has no SHA256SUMS asset")


class PublishedReleaseTests(unittest.TestCase):
    def test_skips_a_stable_tag_whose_release_assets_are_missing(self) -> None:
        calls: list[str] = []

        def fetch(tag: str) -> dict[str, str]:
            calls.append(tag)
            if tag == "v0.3.0":
                return missing(tag)
            return SUMS

        current = published_release.newest_published_tag(["v0.3.0", "v0.2.1"], fetch)

        self.assertEqual(current, "v0.2.1")
        self.assertEqual(calls, ["v0.3.0", "v0.2.1"])

    def test_moves_to_the_new_release_once_its_asset_appears(self) -> None:
        published = {"v0.2.1"}

        def fetch(tag: str) -> dict[str, str]:
            if tag not in published:
                return missing(tag)
            return SUMS

        tags = ["v0.3.0", "v0.2.1"]
        self.assertEqual(
            published_release.newest_published_tag(tags, fetch), "v0.2.1"
        )

        published.add("v0.3.0")
        self.assertEqual(
            published_release.newest_published_tag(tags, fetch), "v0.3.0"
        )

    def test_no_published_release_is_a_clear_failure(self) -> None:
        with self.assertRaises(published_release.ReleaseError) as raised:
            published_release.newest_published_tag(["v0.3.0"], missing)

        self.assertIn("no published stable release", str(raised.exception))

    def test_an_unreachable_host_is_not_an_unpublished_release(self) -> None:
        def unavailable(tag: str) -> dict[str, str]:
            raise published_release.ReleaseUnavailable(f"cannot fetch {tag}")

        with self.assertRaises(published_release.ReleaseUnavailable):
            published_release.newest_published_tag(["v0.3.0", "v0.2.1"], unavailable)

    def test_prereleases_and_unrelated_tags_are_not_releases(self) -> None:
        tags = published_release.parse_stable_tags(
            ["v0.3.0-rc.3", "v0.3.0", "backup/local-637", "v0.2.1"]
        )

        self.assertEqual(tags, ["v0.3.0", "v0.2.1"])

    def test_the_readme_form_drops_the_tag_prefix(self) -> None:
        self.assertEqual(published_release.tag_version("v0.3.0"), "0.3.0")
        self.assertEqual(published_release.tag_version("0.3.0"), "0.3.0")
        with self.assertRaises(published_release.ReleaseError):
            published_release.tag_version("v0.3.0-rc.1")


if __name__ == "__main__":
    unittest.main()
