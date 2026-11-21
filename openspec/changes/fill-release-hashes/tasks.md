## 1. Fixtures

- [x] 1.1 Commit 0.4.0's real `SHA256SUMS` under `tests/fixtures/releases/v0.4.0/`.
- [x] 1.2 Commit the 0.3.0 manifests under `tests/fixtures/releases/v0.4.0/before/` and the #868 result
      (with the #880 locale correction) under `tests/fixtures/releases/v0.4.0/expected/`.

## 2. The script

- [x] 2.1 Write `scripts/fill-release-hashes` with `v<version> --sums <file> [--root DIR]`, reusing
      `published_release.parse_sums`, `STABLE_TAG` and `tag_version`.
- [x] 2.2 Rewrite the Homebrew URL/sha256 pairs and the Scoop version/url/hash with shape-anchored
      edits, and the three winget `PackageVersion` lines plus `InstallerUrl`/`InstallerSha256`.
- [x] 2.3 Collect every refusal before writing, write nothing on a refusal, skip unchanged files and
      keep the second run byte-identical.

## 3. Tests

- [x] 3.1 Add `tests/scripts/test_fill_release_hashes.py`: the 0.4.0 golden comparison, the channel
      guard passing, the docs-sync README update, every refusal and the idempotent second run.
- [x] 3.2 Wire the test into `scripts/verify.sh`'s platform channel-manifest step.
- [x] 3.3 Refuse a duplicated Scoop JSON member and pin the refusal with a regression test that
      proves the manifest bytes are untouched.

## 4. Docs and close-out

- [x] 4.1 Update `docs/dev/distribution.md` step 4 to name the command and the `docs-sync --write`
      follow-up.
- [x] 4.2 Write the changelog fragment for the pull request.
- [ ] 4.3 Run the fast gate, `openspec validate fill-release-hashes --type change --strict`, then
      archive the change as the last commit.
