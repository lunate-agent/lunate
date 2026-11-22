# Changelog fragments

A pull request writes its release-note line as `changes/<pull-request-number>.md` and never edits
`CHANGELOG.md`. The fragment starts at `### Added`, `### Changed` or `### Fixed` — not at a
`## [Unreleased]` section heading — and holds bullets ending in one or more pull-request references,
`(#1234).` or `(#681, #682)`; the release pull request does the whole changelog edit with
`scripts/changelog-assemble --release <version>`, and `scripts/changelog-assemble --check` fails when
they drift. The assembler reads `CHANGELOG.md` itself too and refuses, naming the line, a heading
repeated within one version section, a non-blank line outside any `###` heading in the unreleased
section, and a second `## [Unreleased]`, because each of those loses or misplaces an entry.

A link in a fragment is written from `changes/` and assembled into `CHANGELOG.md` at the repository
root, so the assembler rewrites every relative destination for the root: `[ADR-0043](../adr/0043-contract-unions.md)`
assembles as `[ADR-0043](adr/0043-contract-unions.md)`, and `[the rules](README.md)` as
`[the rules](changes/README.md)`. An anchor, a root path, a URL and a link inside inline code are
carried as written. A link that leaves the repository from `changes/` — `../../docs/dev/guide.md` —
is refused when the fragment is read, naming the fragment, the link and the path it resolves to, so
the author fixes it in their own pull request instead of the release.
