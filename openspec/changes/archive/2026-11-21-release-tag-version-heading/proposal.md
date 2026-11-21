## Why

The tag-time changelog gate proves that `CHANGELOG.md` matches the `changes/<n>.md` fragments, but it
never proves that the changelog carries the tag's own version heading. A release pull request that
forgot to promote `## [Unreleased]` — or promoted another version, or a date the release process does
not spell — passes `--check` and publishes notes under the wrong heading. The tag-time gate already
compares the tag with `<Version>` in `Directory.Build.props` in a separate step, so the two halves of
"this tag is the release this tree describes" live in different places.

## What Changes

- `scripts/changelog-assemble --check` gains `--tag <tag>`, which additionally requires that the
  first level-2 heading below `## [Unreleased]` in `CHANGELOG.md` is exactly
  `## [<version>] - <YYYY-MM-DD>` (an exactly dashed, calendar-valid date), and that `<Version>` in
  `Directory.Build.props` is `<version>`, where `<version>` is the tag without its leading `v`.
- The check fails by name, printing what it expected and what it found for a missing heading, a
  heading for another version, a malformed date and a `Directory.Build.props` mismatch; every failure
  is collected before the command exits, so one run lists all of them.
- `--tag` is a modifier of `--check` only; `--write`, `--release` or a standalone run with `--tag` is
  a usage error, and a tag without its `v` is refused by name.
- `.github/workflows/release.yml` runs `python3 scripts/changelog-assemble --check --tag
  "${GITHUB_REF_NAME}"` at the tag, in the gate job that every publish job needs, and the separate
  "Check tag against version" step is removed because the new check subsumes its equality and adds
  the heading beside it.
- `docs/dev/distribution.md` names the tag-time check and what it proves.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `ci-pipeline`: the changelog assembly contract gains the tag check — the heading, the date spelling,
  the `Directory.Build.props` equality, the named failures, and the Release workflow step that runs it
  before anything is published.

## Impact

`scripts/changelog-assemble`, `.github/workflows/release.yml`,
`tests/scripts/test_changelog_assemble.py`, `tests/scripts/test_release_workflow.py`,
`docs/dev/distribution.md`, and this change's own fragment. No `src/` or C# test project is touched,
no new dependency is added, and the fragment grammar, `--write`, `--release` and the plain `--check`
contract are unchanged.
