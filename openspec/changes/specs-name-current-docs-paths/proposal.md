## Why

The 0.4.0 documentation slices moved pages under `docs/dev/` and split the key table out of the
guide, but two current requirements still name the pre-move homes. S3's archived change repaired the
`repo-quality` and `distribution` paths (#820) and S4's repaired the `extensions` paths (#825), so the
remaining disagreement is `openspec/specs/ci-pipeline/spec.md` naming `docs/guide.md` in a prose
example and `openspec/specs/tui/spec.md` naming `docs/guide.md` as the home of the key table that S2
moved to `docs/keybindings.md`. A spec that points a maintainer at a redirect stub, or at a page that
no longer owns the table, is a broken instruction even when no product code changes.

## What Changes

- The `ci-pipeline` "Prose beside code still runs the full matrix" scenario names
  `docs/dev/guide.md`, the guide's current home, in its GIVEN line. No rule, step or expectation
  changes.
- The `ci-pipeline` delta is copied from the open merge-queue tree-checks delta (#855), which changes
  the same requirement, so the two landings cannot drop each other's merge-group rules. Its two
  modified requirements — "Per-commit verification matrix" and "Aggregate merge gate" — carry the
  tree-check text verbatim and differ from #855 only in the guide's path. The tree-checks job itself
  is #855's; this card changes no workflow.
- The `tui` key-table requirement names `docs/keybindings.md` as the page that owns the generated
  table, and stops calling it the guide's table. The title, the heading reference, the two diagnostic
  examples and the "deleted table" scenario's wording follow the same page; the two-way comparison,
  the exemptions, the heading/table/header requirement and every scenario's behaviour stay exactly
  as they are.
- Nothing else changes. The old paths remain as redirect stubs for one release and their redirects
  stay in `docs/docs.json`; records (ADRs, spikes, eval write-ups, `CHANGELOG.md`, the signed-off
  plan) keep the paths they were written with.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `ci-pipeline`: the docs-path prose example in "Per-commit verification matrix" names the guide's
  current home; the requirement block is the one #855's open delta carries, so both deltas describe
  the same merge-group gate.
- `tui`: "The guide's key table matches the catalog" names the keybindings reference that now owns
  the table.

## Impact

- `openspec/specs/ci-pipeline/spec.md` and `openspec/specs/tui/spec.md` reach the new text through
  this change's delta and the archive commit; no spec file is hand-edited.
- `openspec/specs/ci-pipeline/spec.md` states the merge-queue tree checks from the moment this
  archive lands, whether or not #855 landed first; the job that runs them is #855's change, and this
  card neither adds nor edits it.
- No product code, test, dependency, public API, packaging or release-workflow change.
- The acceptance grep over `openspec/specs/` returns nothing for the pre-move paths after the
  archive.
