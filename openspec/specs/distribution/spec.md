# distribution Specification

## Purpose

How Lunate reaches users: one-command installs (`install.sh` / `install.ps1`) that fetch checksum-verified release archives, the Homebrew/Scoop/winget artifacts and the `dotnet tool` package, and the runbook that drives the live distribution channels.

## Requirements

### Requirement: Install script (sh)

The repository SHALL provide `install.sh` for macOS and Linux: it detects the platform (`Darwin`+`arm64` → `osx-arm64`, `Linux`+`x86_64` → `linux-x64`), downloads the matching release archive and `SHA256SUMS` (latest, or the tag given via `--version` / `LUNATE_INSTALL_VERSION`), verifies the archive's SHA-256 before extracting, installs the `lunate` binary into `$HOME/.local/bin` (or `--prefix` / `LUNATE_INSTALL_PREFIX`), and prints the installed version plus a PATH hint when the directory is not on `PATH`. `LUNATE_INSTALL_BASE_URL` SHALL override the releases base (`https://github.com/wdaniel1993/lunate/releases` by default) and the script SHALL compose `latest/download/<asset>` or `download/<tag>/<asset>` from it (the test seam; `file://` URLs SHALL work). Unsupported platforms SHALL be refused with a message pointing Windows users at `install.ps1`, and the script SHALL never require `sudo` or leave a partial install behind.

#### Scenario: A supported platform installs from a release

- **GIVEN** a release containing the platform's archive and `SHA256SUMS`
- **WHEN** `install.sh` runs against it
- **THEN** the checksum is verified, `lunate` is installed into the target directory, is executable, and the installed version is printed

#### Scenario: A checksum mismatch aborts

- **GIVEN** an archive whose bytes do not match `SHA256SUMS`
- **WHEN** `install.sh` runs
- **THEN** it exits non-zero with a clear error and installs nothing

#### Scenario: An unsupported platform is refused

- **GIVEN** a platform outside the map (for example macOS on x86_64, or a non-Unix shell)
- **WHEN** `install.sh` runs
- **THEN** it exits non-zero naming the platform, and Windows is pointed at `install.ps1`

#### Scenario: Version and prefix flags are honoured

- **GIVEN** `--version` and `--prefix` arguments
- **WHEN** `install.sh` runs
- **THEN** the download composes that tag into the URL and the binary lands in that directory

### Requirement: Install script (PowerShell)

The repository SHALL provide `install.ps1` for Windows: on x86_64 it downloads `lunate-win-x64.zip` and `SHA256SUMS` (same seams as `install.sh`, settable via environment variables so `irm | iex` works without parameters), verifies the SHA-256 with `Get-FileHash`, expands into `%LOCALAPPDATA%\Programs\lunate`, adds that directory to the user `PATH` when missing, and prints the installed version plus a restart-shell hint. Unsupported architectures SHALL be refused, and it SHALL make no machine-wide changes.

#### Scenario: Windows installs from a release

- **GIVEN** a release containing the zip and `SHA256SUMS`
- **WHEN** `install.ps1` runs
- **THEN** the checksum is verified, `lunate.exe` is installed, runs, and the user `PATH` contains the install directory

#### Scenario: A checksum mismatch aborts

- **GIVEN** a zip whose bytes do not match `SHA256SUMS`
- **WHEN** `install.ps1` runs
- **THEN** it exits non-zero with a clear error and installs nothing

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

### Requirement: Dotnet tool package

`Lunate.Coding` SHALL pack as a framework-dependent .NET tool with package id `lunate` and command
`lunate`, targeting `net11.0` only, so installing and running it SHALL require the .NET 11 runtime;
it SHALL add no new dependencies. The release workflow SHALL attach the `.nupkg` to the GitHub
release; the release note SHALL state the .NET 11 runtime requirement; and the verify gate SHALL
prove a local roundtrip — pack, `dotnet tool install` from the local feed, run `lunate --version`.

#### Scenario: The tool roundtrips from a local feed

- **GIVEN** a `dotnet pack` of the CLI and an empty tool path
- **WHEN** the tool is installed from the local feed and run on a machine with the .NET 11 runtime
- **THEN** `lunate --version` reports the current version

#### Scenario: The tool package names its runtime requirement

- **GIVEN** a packed `lunate` tool package
- **WHEN** its target framework group is read
- **THEN** it carries one `tools/net11.0` asset, and the release note for that version states that
  the `dotnet tool` install requires the .NET 11 runtime

### Requirement: Extension package

The release SHALL publish one extension as a real NuGet package beside the tool: `Lunate.Extensions.Subagents`, the reference subagents extension under `extensions/subagents`, packed as a bundle — `lunate.package.json` at the package root and the extension's files under `extension/` — at the repository's `<Version>`, with no host assembly inside it. The same `v*` tag that publishes the tool SHALL push the package to nuget.org through the existing Trusted Publishing policy, and SHALL attach it to the GitHub release. The verify gate SHALL pack the supported extension and prove install, `lunate packages --verify` and uninstall from a folder feed with the published binary; the release smoke SHALL prove the same cycle against the published feed.

#### Scenario: The tag publishes the package

- **GIVEN** a `v*` tag that matches `<Version>` in `Directory.Build.props`
- **WHEN** the release workflow completes
- **THEN** the GitHub release carries `Lunate.Extensions.Subagents.<version>.nupkg`, `SHA256SUMS` covers it, and nuget.org serves that exact version

#### Scenario: A user installs the reference extension

- **GIVEN** a published `Lunate.Extensions.Subagents`
- **WHEN** `lunate install Lunate.Extensions.Subagents@<version>` runs against a feed that serves it
- **THEN** the extension is installed, listed by `lunate packages`, verifies with `--verify`, and `lunate uninstall` removes exactly what the install wrote

#### Scenario: A dry run publishes nothing

- **GIVEN** a manual workflow dispatch instead of a tag
- **WHEN** the release workflow runs
- **THEN** both packages are packed and uploaded as workflow artifacts, and neither is pushed to nuget.org

#### Scenario: The bundle layout is gated

- **GIVEN** a change to the extension's build output or packaging
- **WHEN** the verify gate runs
- **THEN** the packed nupkg installs from a folder feed, `lunate packages --verify` reports it verified, and the uninstall leaves no content and no record

### Requirement: Release smoke

The repository SHALL provide a `smoke.yml` workflow, dispatched with a release tag, that verifies that tag's *published* artifacts on ubuntu, macos and windows: both documented installers install the archive, the installed binary reports that version and starts, and the extension package the same tag published is installed by id from the feed a user reads, listed by `lunate packages`, verified with `--verify`, discovered by `lunate extensions` as pending trust, and uninstalled with neither content nor record left behind. The workflow SHALL keep the install in a state root outside the checkout, SHALL run keyless, and SHALL fail by name when the tag published no such package version. Before it installs the package, the workflow SHALL wait for the feed to list that version — polling the flatcontainer index, the package content and the registration resource under one budget of about ten minutes — and SHALL install once, against a feed it has just seen serve the version. When the budget runs out it SHALL fail naming the version it waited for and the state of each resource, rather than retrying a fixed number of times.

#### Scenario: A published tag passes the smoke

- **GIVEN** a release tag whose archive and extension package were published
- **WHEN** `smoke.yml` runs for that tag
- **THEN** all three operating systems install the archive, run the binary, and complete install → verify → uninstall of the package

#### Scenario: A tag without the package fails by name

- **GIVEN** a release tag cut before the extension package existed
- **WHEN** `smoke.yml` runs for that tag
- **THEN** the package step fails naming the package and the version it asked the feed for

#### Scenario: The smoke cannot be made to pass by a local store

- **GIVEN** a runner whose home already holds a Lunate store
- **WHEN** `smoke.yml` runs
- **THEN** every package command reads and writes a state root under the runner's temporary directory, and the pre-existing store is neither read nor written

#### Scenario: A freshly pushed version is waited for, not retried

- **GIVEN** a tag whose package was pushed minutes ago and is not yet indexed by the feed
- **WHEN** the smoke reaches the extension step
- **THEN** it waits until the feed lists the version in the flatcontainer index, serves its `.nupkg`, and lists it in the registration resource, and then installs it once

#### Scenario: An exhausted budget names the feed state

- **GIVEN** a tag whose package version does not become visible within the budget
- **WHEN** the wait ends
- **THEN** the step fails naming the package and version and what each resource served — absent from the index, content not downloadable, registration not served, or unreachable — and the install is not attempted

#### Scenario: The wait is proven without the network

- **GIVEN** fixture feeds served from the filesystem
- **WHEN** the gate's self-tests run
- **THEN** the wait succeeds for a version that appears in the middle of the budget, fails when the version never appears, and refuses a version whose content is missing

### Requirement: The SDK packages publish on the same tag

The release SHALL publish the extension contract beside the tool on the same `v*` tag through the
Trusted Publishing path the tool uses: `Lunate.Contracts` and `Lunate.Extensibility.Abstractions` as
**stable** packages, and `Lunate.Agent` with `Lunate.Extensibility.Testing` as **preview** packages.
The stable packages SHALL carry the version `ExtensionApi.Current` names — `1.15.0` at publication,
not the tool's version — and the preview pair SHALL be `0.2.0-preview.<n>` with "no API stability
promise before 1.0" in the package description and README. Every nupkg the release pushes SHALL pass
`scripts/check-package-metadata.sh` — licence expression, packed readme, icon and git repository URL
— and the preview pair SHALL pass it under a prerelease flag, in the gate and before any push.

#### Scenario: The tag publishes the contract set

- **GIVEN** a `v*` tag that matches `<Version>` in `Directory.Build.props`
- **WHEN** the release workflow completes
- **THEN** nuget.org serves `Lunate.Contracts.<ExtensionApi.Current>`,
  `Lunate.Extensibility.Abstractions.<ExtensionApi.Current>`, `Lunate.Agent.0.2.0-preview.<n>` and
  `Lunate.Extensibility.Testing.0.2.0-preview.<n>`, and the GitHub release carries all of them with
  `SHA256SUMS` coverage

#### Scenario: A preview package says preview

- **GIVEN** a packed `Lunate.Agent` or `Lunate.Extensibility.Testing`
- **WHEN** `check-package-metadata.sh` runs on it with the prerelease flag
- **THEN** the check passes only because the version carries the preview suffix; feeding a stable
  package to the same check fails naming the requirement

#### Scenario: A dry run uploads everything and publishes nothing

- **GIVEN** a manual workflow dispatch instead of a tag
- **WHEN** the release workflow runs
- **THEN** every package — the tool, the reference extension and the four SDK packages — is packed,
  metadata-checked and uploaded as workflow artifacts, and none is pushed to nuget.org

### Requirement: The template installs the contract from NuGet

The packaged-extension template's contract references SHALL be package references pinned to the
version `ExtensionApi.Current` names, compile-only as the host requires
(`PrivateAssets="all"`, `ExcludeAssets="runtime"`). A template that sits inside this repository MAY
keep project references while the packages it needs are not on the machine's feed; the verify gate
SHALL prove the NuGet shape by binding the template to a folder feed of freshly packed packages and
building the template that way.

#### Scenario: A template copy outside the repository compiles against NuGet

- **GIVEN** the template's csproj copied into a project whose checkout does not sit inside this
  repository
- **WHEN** the project restores and builds
- **THEN** the contract assemblies come from the NuGet feed pinned at the ExtensionApi version and
  the two contract dll names are absent from the extension's output directory

#### Scenario: The gate proves the package path without the packages on nuget.org

- **GIVEN** a fresh checkout of this repository
- **WHEN** the verify gate runs
- **THEN** the four SDK packages are packed into a folder feed and the template is built with NuGet
  references bound to that feed, or the gate fails if packaging or the template drift apart

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
