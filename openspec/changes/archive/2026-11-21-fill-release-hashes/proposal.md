## Why

After every tag the Homebrew formula, the Scoop manifest and the three winget manifests are filled by
hand from the release's `SHA256SUMS` (`docs/dev/distribution.md` step 4; #868 did it for 0.4.0). The
lane will open that post-tag pull request itself (lunate-lane#21), so the edit has to be a command
that reads the release's checksums and rewrites the manifests deterministically instead of a runbook
step every operator re-does from memory.

## What Changes

- A committed script `scripts/fill-release-hashes` with the usage
  `fill-release-hashes v<version> --sums <SHA256SUMS file>` rewrites, against the repository root:
  - `packaging/homebrew/lunate.rb`: the macOS arm64 and Linux x64 release URLs and their two
    `sha256` values;
  - `packaging/scoop/lunate.json`: `version`, the win-x64 `url` and its `hash`;
  - `packaging/winget/wdaniel1993.lunate.yaml`,
    `packaging/winget/wdaniel1993.lunate.installer.yaml` and
    `packaging/winget/wdaniel1993.lunate.locale.en-US.yaml`: `PackageVersion`, and in the installer
    manifest `InstallerUrl` and the uppercase `InstallerSha256`.
- The command reads only the `--sums` file it is given: no network, no git, no release lookup. It
  refuses, naming the missing asset or the manifest whose shape moved and writing nothing at all,
  when `SHA256SUMS` lacks `lunate-osx-arm64.tar.gz`, `lunate-linux-x64.tar.gz` or
  `lunate-win-x64.zip`, or when a manifest no longer carries the lines it edits.
- A second run with the same input leaves every byte unchanged.
- `docs/dev/distribution.md` step 4 names the command instead of the manual grep-and-edit list, and
  says that `scripts/docs-sync --write` updates the README release line afterwards.
- Tests run the command on 0.4.0's real `SHA256SUMS` (committed as a fixture) against the 0.3.0
  manifests and compare the result with what #868 committed (plus the locale correction from #880),
  prove the result passes `scripts/check-channel-manifests.py`, prove `scripts/docs-sync --write`
  updates the README afterwards, and cover every refusal and idempotency.
- No new dependency, no public API, no runtime behaviour: this is a release-process script.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `distribution`: the channel artifacts are filled from a release's `SHA256SUMS` by one committed
  command, what it rewrites, what it refuses and the runbook step that names it.

## Impact

`scripts/fill-release-hashes` (new), `tests/scripts/test_fill_release_hashes.py` (new),
`tests/fixtures/releases/v0.4.0/` (the real `SHA256SUMS` and the before/expected manifests),
`scripts/verify.sh` (the gate runs the new test in the platform checks) and
`docs/dev/distribution.md`. Nothing under `src/` or `tests/` (C#) is touched.
