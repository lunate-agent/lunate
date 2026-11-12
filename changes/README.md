# Changelog fragments

A pull request writes its release-note line as `changes/<pull-request-number>.md` and never edits
`CHANGELOG.md`. Put the line under `### Added`, `### Changed` or `### Fixed`, as a bullet ending with
a reference such as `(#1234)`; the release pull request folds every fragment into the changelog with
`scripts/changelog-assemble --write`, and `scripts/changelog-assemble --check` fails when they drift.
