## Why

The release pull request's changelog edit is a page of manual steps: run
`scripts/changelog-assemble --write`, promote `## [Unreleased]` to `## [<version>] - <date>`, strip
the generated markers from the promoted section, leave a fresh empty `## [Unreleased]` above it,
delete the consumed fragments and bump `<Version>` in `Directory.Build.props`. The lane opens release
pull requests itself (lunate-lane#21), so that edit has to be one deterministic command instead of a
runbook every operator re-reads and half-remembers.

## What Changes

- `scripts/changelog-assemble` gains a third mode, `--release <version> [--date YYYY-MM-DD]`, that
  performs the whole release pull request edit in one run: assemble the fragments into the unreleased
  section, promote that section to `## [<version>] - <date>` with the marker comments removed, leave a
  fresh empty `## [Unreleased]` above it, delete the consumed `changes/<n>.md` fragments and set
  `<Version>` in `Directory.Build.props`. `--date` defaults to today in UTC.
- The mode refuses, by name and with nothing written, when the version is not newer than the current
  `<Version>`, when a `## [<version>]` heading already exists, or when any fragment is invalid — so a
  second run is refused and never promotes twice.
- `docs/dev/distribution.md` step 1, `changes/README.md` and `CONTRIBUTING.md` name the command
  instead of the manual steps; the changelog guard's message names it too.
- No new dependency, no public API, no runtime behaviour: this is a release-process script.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `ci-pipeline`: adds the `--release` mode to the assembler contract — what it does, in which order,
  and the three named refusals that leave the tree untouched.

## Impact

`scripts/changelog-assemble`, `scripts/changelog-guard` (its message only),
`tests/scripts/test_changelog_assemble.py`, `docs/dev/distribution.md`, `changes/README.md`,
`CONTRIBUTING.md`, and this change's own fragment. Nothing under `src/` or `tests/` (C#) is touched.
