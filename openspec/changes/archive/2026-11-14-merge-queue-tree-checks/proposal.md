## Why

Two pull requests each looked at a tree that was correct on its own: #840 took ADR-0036 while the
number was free, and #843's full gate never ran the ADR numbering check at all. The merge queue then
combined them, and nothing in the queue ran the tree checks, so a duplicate ADR number reached
`main` — twice, because #843's queue entry also met #840's record on the same tree. Those checks are
seconds of work and they are exactly the rules a batch of two pull requests can break without either
pull request being wrong; a check the queue does not run is a check the queue cannot reject.

## What Changes

- **One script for the tree checks.** `scripts/tree-checks.sh` runs the five cheap repository-wide
  checks in order, naming each one: ADR numbering, the spec-archive rule, the generated docs regions,
  the docs link/navigation check and the docs lint. Both workflows that need them call it, so their
  command lists cannot drift.
- **The merge group runs them.** `.github/workflows/ci.yml` gains a `tree-checks` job that runs the
  script against `github.event.merge_group.base_sha...HEAD` — the batch's own tree, against the
  branch it will land on — with full history and the release tags, because the spec-archive check
  diffs the batch and the version region reads the tags. It is a few seconds: no .NET, no build, no
  test.
- **`ci-ok` requires them.** The aggregate gate takes the new job as a needed job, requires it to
  succeed on a `merge_group` run, and requires it skipped on every other event. A queue entry whose
  tree checks fail is red, so the queue ejects it instead of merging a duplicate.
- **The push to `main` keeps its own check.** `tree-checks.yml` stays the trigger for a push to
  `main` (a docs-only push skips `ci.yml` entirely) and now calls the same script.
- **The pull request path is unchanged.** A pull request still runs the checks its profile already
  carries; the queue is where two pull requests meet, and that is the tree the new job judges.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `ci-pipeline`: a `merge_group` run adds the tree checks — ADR numbering, the spec-archive rule, the
  generated docs regions, the docs link/navigation check and the docs lint — and the aggregate gate
  requires them, so a batch that carries a duplicate ADR number is rejected by the queue.

## Impact

- `.github/workflows/ci.yml`: the new `tree-checks` job, the `ci-ok` `needs` list and its environment.
- `.github/workflows/tree-checks.yml`: the push-to-main job calls the shared script instead of
  spelling the five commands out.
- `scripts/tree-checks.sh` (new): the single command list for the tree checks.
- `scripts/ci-ok.sh` and `scripts/ci-ok-state`: the merge-group requirement, the skipped-elsewhere
  rule, the live pull-request value, the self-test cases and the workflow invariants.
- `tests/scripts/test_tree_checks.py`: the wiring test now pins the shared script and both callers.
- `CONTRIBUTING.md` and `docs/dev/README.md`: the gate description names the merge queue's tree
  checks.
- No product code, public API, package or release-workflow change.
