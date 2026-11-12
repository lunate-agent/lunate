# Changelog fragments

A pull request writes its release-note line as `changes/<pull-request-number>.md` and never edits
`CHANGELOG.md`. The fragment starts at `### Added`, `### Changed` or `### Fixed` — not at a
`## [Unreleased]` section heading — and holds bullets ending in one or more pull-request references,
`(#1234).` or `(#681, #682).`; the release pull request folds every fragment into the changelog with
`scripts/changelog-assemble --write`, and `scripts/changelog-assemble --check` fails when they drift.
