## MODIFIED Requirements

### Requirement: Channel artifacts

The repository SHALL carry the distribution-channel artifacts next to the code: a Homebrew formula (`packaging/homebrew/lunate.rb`) with macOS arm64 and Linux x64 release URLs, a Scoop manifest (`packaging/scoop/lunate.json`) with `checkver`/`autoupdate`, and winget manifests (`packaging/winget/`) for a portable x64 install. Each artifact SHALL name the release asset filenames and the current `<Version>` from `Directory.Build.props`, with SHA-256 placeholders at rest that the release runbook fills. `docs/dev/distribution.md` SHALL document the live-channel runbook (tap and bucket repositories, winget submission including the pre-release name check, NuGet push, first-release checklist).

#### Scenario: Packaging files carry the current version

- **GIVEN** the packaging files and `Directory.Build.props`
- **WHEN** the verify gate runs
- **THEN** every packaging file exists and contains the current version string
