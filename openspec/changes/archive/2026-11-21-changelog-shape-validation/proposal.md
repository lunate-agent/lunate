## Why

Fragments are validated, `CHANGELOG.md` itself is not. The #813 review met a fragment the assembler
could not consume (`changes/800.md` carried `## [Unreleased]` and text outside the heading), and the
changelog has the same blind spots one level up: a second `### Added` in the unreleased section makes
`--write` drop the first block's hand-written bullets, an entry above the first `###` heading is
carried outside every heading into the next release, and a second `## [Unreleased]` hides everything
under it from both `--write` and `--release`. In each case the run exits 0 and `--check` reports the
tree as up to date.

## What Changes

- `scripts/changelog-assemble` gains a changelog shape check, run by `--check` and `--write` (and
  `--release`, which assembles through the same path), that fails with one diagnostic per finding,
  each naming `CHANGELOG.md`, the line and what was found, when in `CHANGELOG.md`:
  - a `##` or `###` heading repeats within one version section;
  - a non-blank line sits outside any `###` heading in the unreleased section;
  - `## [Unreleased]` appears more than once.
- `--write` counts the entries of the unreleased section that the generated marker region does not
  own before and after the rewrite and refuses, writing nothing, when the counts differ, so a write
  can never carry fewer entries than it read.
- `tests/scripts/test_changelog_assemble.py` gains a case per diagnostic and a clean-changelog case.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `ci-pipeline`: the changelog assembly contract gains the shape check and its named diagnostics, and
  the entry-count refusal that keeps `--write` from dropping an entry.

## Impact

`scripts/changelog-assemble`, `tests/scripts/test_changelog_assemble.py`, this change's own fragment,
and the `ci-pipeline` spec delta. No `src/` or C# test project is touched, no dependency is added, no
mode, option or output of a successful run changes, and the fragment grammar stays as it is.
