## 1. The release mode

- [ ] 1.1 `scripts/changelog-assemble`: `--release <version> [--date YYYY-MM-DD]` as a third mode,
      exclusive with `--check` and `--write`, with `--date` rejected outside it.
- [ ] 1.2 Assemble in memory, then promote: strip the marker comments from the unreleased body,
      emit a fresh empty `## [Unreleased]`, the `## [<version>] - <date>` section, and the rest of
      the file unchanged.
- [ ] 1.3 The version gate: read `<Version>` from `Directory.Build.props`, compare by semantic-version
      precedence (prerelease before its release), refuse a version that is not newer.
- [ ] 1.4 The named refusals, collected before any write: not newer, `## [<version>]` already present,
      no `## [Unreleased]`, no `<Version>` element, invalid fragment (its own message).
- [ ] 1.5 The writes: `CHANGELOG.md`, `Directory.Build.props`, then the consumed fragments deleted;
      `--date` default today in UTC; the summary line names version, date, fragments and the bump.

## 2. Tests

- [ ] 2.1 `tests/scripts/test_changelog_assemble.py`: a `Directory.Build.props` and a `version=`
      argument in the tree helper for the release cases.
- [ ] 2.2 A normal release: promoted heading with the date, no markers, fresh empty unreleased
      section, fragments deleted, `<Version>` bumped, `--check` passes and released history untouched.
- [ ] 2.3 A prerelease: `0.5.0-rc.1` on a `0.5.0-rc.1`-ready tree promotes that heading.
- [ ] 2.4 The UTC default date, and that `--date` is required to parse as `YYYY-MM-DD`.
- [ ] 2.5 Each refusal writes nothing: not newer (equal and lower), an existing heading, an invalid
      fragment; a second run is refused and leaves exactly one release heading.
- [ ] 2.6 A release with no fragments left: empty promoted section, version bumped, `--check` passes.

## 3. Documentation and bookkeeping

- [ ] 3.1 `docs/dev/distribution.md` step 1 names the command instead of the manual steps; the
      "Later releases" bullet follows it.
- [ ] 3.2 `changes/README.md` and `CONTRIBUTING.md` name `--release` for the release pull request;
      the changelog guard's message names it too.
- [ ] 3.3 `changes/962.md`: this change's own fragment under `### Added`.
- [ ] 3.4 `python3 tests/scripts/test_changelog_assemble.py`, `python3 tests/scripts/test_changelog_guard.py`,
      `scripts/changelog-assemble --check`, markdownlint for the touched Markdown and
      `openspec validate changelog-release-command --strict`.
- [ ] 3.5 `openspec archive changelog-release-command --yes` as the final commit.
