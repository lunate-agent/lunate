## Why

A push to `main` and the nightly `schedule` run share `github.ref == refs/heads/main`, so `ci.yml`'s
single concurrency group puts them in the same slot. With `cancel-in-progress` true for anything
except a `merge_group`, the nightly cancels an in-flight push run: run 42466169831 (`CI [push]` on
`2649040c`) ended `cancelled`, and a re-run of it was cancelled again. A cancelled run is not a
verdict, and main's invariant is that every commit has one (#705).

## What Changes

- `ci.yml` groups the nightly run separately from the push run and cancels only pull-request runs;
  a push to `main`, a `v*` tag, a `merge_group` and a `workflow_dispatch` are never cancelled.
- `docs-lint.yml` and `pack.yml` apply the same rule where they run on `main`: only a pull-request
  run may supersede an older run of the same ref. (Neither has a `schedule` trigger, so neither
  needs a separate nightly group.)
- A committed check asserts the effective concurrency decision per event for all three workflows, so
  "only pull requests cancel, and the nightly never shares a group with a push" cannot regress
  silently. It runs in the existing `ci-ok.sh --self-test` tooling step.

No matrix leg, shard count or timeout changes.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `ci-pipeline`: the per-commit verification contract gains the concurrency rule — a push to `main`,
  a tag, a `merge_group` and the nightly schedule SHALL NOT cancel or be cancelled by another run,
  and only pull-request runs SHALL supersede their own older runs.

## Impact

- `.github/workflows/ci.yml`, `.github/workflows/docs-lint.yml`, `.github/workflows/pack.yml`:
  the `concurrency` blocks only.
- `scripts/ci-ok.sh`: a workflow-invariant assertion in `--self-test` (no runtime decision changes).
- `openspec/specs/ci-pipeline/spec.md`: the concurrency requirement and scenarios.
- No product code, public API, packages or test matrix changes.

## Note on the project rules

The maintainer brief and issue #705 are the agreed intent; this run is unattended, so the
`grill-me` interview could not be held and the proposal records the brief's constraints instead.
