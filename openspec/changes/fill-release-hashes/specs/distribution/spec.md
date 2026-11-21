## ADDED Requirements

### Requirement: Channel manifests are filled from the release checksums

The repository SHALL provide a committed script, `scripts/fill-release-hashes`, that rewrites the
channel manifests for one stable release from that release's checksum file:

```text
scripts/fill-release-hashes v<version> --sums <SHA256SUMS file>
```

It SHALL use only the Python 3 standard library and SHALL read only the file named by `--sums`; it
SHALL NOT contact the release host, read the repository's tags or consult any other source. It SHALL
rewrite, under the repository root and for the tag it is given:

- `packaging/homebrew/lunate.rb`: the `lunate-osx-arm64.tar.gz` and `lunate-linux-x64.tar.gz` release
  URLs and the `sha256` value that follows each of them;
- `packaging/scoop/lunate.json`: `version`, the `lunate-win-x64.zip` `url` and the `hash`;
- `packaging/winget/wdaniel1993.lunate.yaml`,
  `packaging/winget/wdaniel1993.lunate.installer.yaml` and
  `packaging/winget/wdaniel1993.lunate.locale.en-US.yaml`: `PackageVersion`, and in the installer
  manifest `InstallerUrl` and `InstallerSha256` in uppercase hexadecimal.

It SHALL refuse, naming the missing asset or the manifest and the line it expected, and SHALL write
nothing at all, when `SHA256SUMS` does not carry `lunate-osx-arm64.tar.gz`,
`lunate-linux-x64.tar.gz` or `lunate-win-x64.zip`, when the checksum file is malformed, or when a
manifest no longer has the shape the command edits (a missing, duplicated or renamed URL, `sha256`,
`version`, `hash`, `PackageVersion`, `InstallerUrl` or `InstallerSha256` line). It SHALL refuse a
tag that is not a stable `v<major>.<minor>.<patch>` tag, because the channel guard and the README
release line are defined on stable releases only.

A second run with the same tag and checksum file SHALL leave every byte unchanged. The tests SHALL
run the command on 0.4.0's real `SHA256SUMS` against the 0.3.0 manifests and SHALL compare the result
with what #868 committed, and SHALL prove that the result passes `scripts/check-channel-manifests.py`
and that `scripts/docs-sync --write` then updates the README release line. The release runbook
(`docs/dev/distribution.md`, step 4) SHALL name the command instead of the manual list.

#### Scenario: The 0.4.0 checksums reproduce the committed manifests

- **GIVEN** the 0.3.0 channel manifests and 0.4.0's real `SHA256SUMS`
- **WHEN** `scripts/fill-release-hashes v0.4.0 --sums SHA256SUMS` runs
- **THEN** the Homebrew, Scoop and winget manifests are byte-identical to the ones #868 committed, and `scripts/check-channel-manifests.py --tag v0.4.0` passes against the same checksums

#### Scenario: The README release line follows through docs-sync

- **GIVEN** a filled tree whose README still carries the previous release
- **WHEN** `scripts/docs-sync --write` runs against the published 0.4.0 release
- **THEN** the README's version region names 0.4.0

#### Scenario: A missing asset is refused by name

- **GIVEN** a `SHA256SUMS` file without `lunate-linux-x64.tar.gz`
- **WHEN** the command runs on an otherwise valid tree
- **THEN** it fails naming `lunate-linux-x64.tar.gz`, and every manifest is byte-identical to before

#### Scenario: A manifest whose shape moved is refused by name

- **GIVEN** a winget installer manifest whose `InstallerSha256` line was renamed
- **WHEN** the command runs with complete checksums
- **THEN** it fails naming the manifest and the line it expected, and writes nothing

#### Scenario: A second run changes nothing

- **GIVEN** a tree the command already filled
- **WHEN** it runs again with the same tag and checksums
- **THEN** every manifest's bytes are unchanged

## MODIFIED Requirements

### Requirement: Channel artifacts

The repository SHALL carry the distribution-channel artifacts next to the code: a Homebrew formula (`packaging/homebrew/lunate.rb`) with macOS arm64 and Linux x64 release URLs, a Scoop manifest (`packaging/scoop/lunate.json`) with `checkver`/`autoupdate`, and winget manifests (`packaging/winget/`) for a portable x64 install. Each artifact SHALL name the release asset filenames and a release version, with its SHA-256 values written by `scripts/fill-release-hashes` from that release's `SHA256SUMS` and not edited by hand. `docs/dev/distribution.md` SHALL document the live-channel runbook (tap and bucket repositories, winget submission including the pre-release name check, NuGet push, first-release checklist) and SHALL name the command in step 4.

#### Scenario: Packaging files carry the current version

- **GIVEN** the packaging files and `Directory.Build.props`
- **WHEN** the verify gate runs
- **THEN** every packaging file exists and contains the current version string

#### Scenario: The checksums are the only source of a channel hash

- **GIVEN** a release whose `SHA256SUMS` carries the three archives
- **WHEN** `scripts/fill-release-hashes` has run for its tag
- **THEN** every channel hash in the artifacts equals the checksum file's value for that asset
