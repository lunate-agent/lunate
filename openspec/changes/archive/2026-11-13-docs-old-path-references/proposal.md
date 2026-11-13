## Why

Slice S4 moved the specs, spikes and eval evidence under `docs/dev/` (#795), but three current,
non-record places still name the pre-move paths: two requirements in `openspec/specs/extensions/`
point at `docs/spec/` and `docs/eval/`, and the documentation checker still lists those old prefixes
as exempt, so a page reintroduced there would be skipped instead of reported. Records — ADRs, spike
files, eval write-ups, the changelog and the signed-off plan — keep their paths by the records rule
and are not touched.

## What Changes

- The `extensions` spec names the paths that exist: `docs/dev/spec/extensibility.md` in the skill
  sources requirement and `docs/dev/eval/roslyn-built-in-migration-baseline.md` in the Roslyn
  built-in requirement. Both are current instructions, not records.
- `scripts/docs_check` drops the pre-move `docs/spikes/` and `docs/eval/` record prefixes and the
  dead `spec/` maintainer prefix, so a page at an old path is reported as an un-navigated user page
  instead of being silently treated as a record or a maintainer page.
- Self-test cases pin the rule in both directions: a page under `docs/spikes/` or `docs/eval/` fails
  the check, and the `docs/dev/` record and maintainer paths stay exempt.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `extensions`: the skill sources requirement and the Roslyn built-in requirement name the contract
  and baseline paths that the S4 move left in place under `docs/dev/`.

## Impact

`openspec/specs/extensions/spec.md` (two requirement texts, paths only), `scripts/docs_check`'s
prefix constants and self-test. No product code, no runtime behaviour, no public API, no new package
and no change to the documentation tree itself.
