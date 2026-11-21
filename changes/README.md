# Changelog fragments

A pull request writes its release-note line as `changes/<pull-request-number>.md` and never edits
`CHANGELOG.md`. The fragment starts at `### Added`, `### Changed` or `### Fixed` — not at a
`## [Unreleased]` section heading — and holds bullets ending in one or more pull-request references,
`(#1234).` or `(#681, #682).`; the release pull request does the whole changelog edit with
`scripts/changelog-assemble --release <version>`, and `scripts/changelog-assemble --check` fails when
they drift. The assembler reads `CHANGELOG.md` itself too and refuses, naming the line, a heading
repeated within one version section, a non-blank line outside any `###` heading in the unreleased
section, and a second `## [Unreleased]`, because each of those loses or misplaces an entry.
