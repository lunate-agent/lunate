## ADDED Requirements

### Requirement: The release packs the decision model package

The release and the pack dry run SHALL pack `src/Lunate.Decisions.Model` beside the stable contract
pair. Its package id SHALL be `Lunate.Decisions.Model`, it SHALL carry the contract API version plus
the tag's prerelease suffix, and it SHALL appear in the pack list's expected file names and metadata
checks, so a release cannot publish `Lunate.Extensibility.Abstractions` whose declared dependency the
feed does not carry. The verify gate's contract-package roundtrip SHALL pack it into the feed the
template restores from, so a template built against packages proves the dependency resolves.

#### Scenario: A tag publishes the model package

- **GIVEN** a `v*` tag that passes the gate
- **WHEN** the release packs its packages
- **THEN** `Lunate.Decisions.Model.<contract-version>.nupkg` is built, metadata-checked and pushed
  to nuget.org before `Lunate.Extensibility.Abstractions`

#### Scenario: A pull request proves the pack list

- **GIVEN** a pull request that changes the release package set
- **WHEN** the pack dry run executes
- **THEN** it packs and metadata-checks `Lunate.Decisions.Model` and fails when the expected package
  file is missing

#### Scenario: The template restores the model from the feed

- **GIVEN** the verify gate's contract-package feed
- **WHEN** the packaged-extension template restores against it
- **THEN** the template's own `Lunate.Decisions.Model` reference and the dependent
  `Lunate.Extensibility.Abstractions` both resolve from the freshly packed feed, not from a released
  copy
