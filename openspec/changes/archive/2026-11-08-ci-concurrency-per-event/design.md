## Context

`ci.yml` currently has one concurrency group, `ci-${{ github.ref }}`, and cancels everything except
`merge_group`. `github.ref` is `refs/heads/main` for both a push to `main` and the nightly
`schedule` run, so the nightly supersedes an in-flight push run (run 42466169831 on commit
`2649040c`). `docs-lint.yml` and `pack.yml` use `cancel-in-progress: true` on the same kind of group.

The constraint is GitHub Actions' concurrency model: each group holds at most one running and one
pending run; a new run in the same group with `cancel-in-progress: true` cancels the running one.
The group and the cancellation decision are both expressions over the event context, so the fix is
in the two expressions, not in the jobs.

No in-force ADR constrains this; ADR-0005 mentions the existing `concurrency` setting only in passing.

## Goals / Non-Goals

**Goals:**

- A push to `main` reaches a completed verdict even when the nightly starts.
- The nightly is a separate obligation: it cancels nothing and is cancelled by nothing.
- Only a pull-request run supersedes its own older run.
- `merge_group`, `v*` tags and `workflow_dispatch` keep today's never-cancelled behaviour.
- `docs-lint` and `pack` follow the same rule on `main`.
- A committed test evaluates the expression per event, so the rule cannot regress silently.

**Non-Goals:**

- Changing the matrix, shard counts, timeouts or any job.
- Making CI faster or cheaper; this is a correctness change for the verdict.
- Fixing the nightly's own failure on the same commit (tracked separately).

## Decisions

### Group by obligation, not by branch alone

`ci.yml` appends `-nightly` only for `schedule`:

```yaml
concurrency:
  group: ci-${{ github.ref }}${{ github.event_name == 'schedule' && '-nightly' || '' }}
  cancel-in-progress: ${{ github.event_name == 'pull_request' }}
```

The event-to-group mapping:

| Event | `github.ref` | Group | Cancels |
| --- | --- | --- | --- |
| `push` to `main` | `refs/heads/main` | `ci-refs/heads/main` | nothing |
| `push` tag `v*` | `refs/tags/v…` | `ci-refs/tags/v…` | nothing |
| `pull_request` | `refs/pull/N/merge` | `ci-refs/pull/N/merge` | its own older run |
| `merge_group` | `refs/heads/gh-readonly-queue/…` | `ci-refs/heads/gh-readonly-queue/…` | nothing |
| `schedule` | `refs/heads/main` | `ci-refs/heads/main-nightly` | nothing |
| `workflow_dispatch` | the dispatched ref | `ci-<ref>` | nothing |

Pull requests share a group only with the same pull request (`github.ref` is per-PR), so a new push
supersedes that PR's older run — the reason the setting exists. Two pushes to `main` now share a
group but neither cancels: the second queues behind the first instead of replacing it, so the older
commit keeps its verdict. Tags, merge groups and manual dispatches are already isolated by ref.

Alternatives considered: dropping `concurrency` from `ci.yml` entirely would also stop the
cancellation, but it would leave every PR push queueing behind its predecessor and lose the intended
supersede. Keying the group on `github.run_id` would isolate everything and lose it equally.

### Only `pull_request` may cancel in the advisory workflows

`docs-lint.yml` and `pack.yml` have no `schedule` trigger, so their groups stay keyed on `github.ref`
and only the cancellation expression changes to `${{ github.event_name == 'pull_request' }}`. In
`pack.yml` a pull request still supersedes its own older pack; a push to `main` no longer cancels an
older push. In `docs-lint.yml` (push only) the expression is always false, which is the same rule
stated once rather than a special case.

### Assert the decision, not the YAML text

`scripts/ci-ok.sh --self-test` already asserts workflow invariants by reading the workflow. This
change adds a small evaluator for the two expression forms in use (`github.ref`, the
`event_name == 'schedule'` suffix, and literal or `event_name ==/!=` cancellation) and checks the
effective group and cancellation decision for every event, for each of the three workflows. It runs
in the existing "Tooling self-tests" step, so a regression fails CI. Asserting the text verbatim was
rejected: it would pin spacing and would not show which event broke.

## Risks / Trade-offs

- [A second push to `main` queues instead of cancelling, so a superseded commit runs the full gate] →
  intended: every commit on `main` needs its own completed verdict.
- [The evaluator understands only the expression forms in use, not general GitHub expressions] →
  it fails closed on an unrecognised form, and the three workflows are the only inputs.
- [The nightly can run concurrently with the push run and compete for runner capacity] → accepted;
  they are separate obligations and the queue is the right place to serialise them.

## Migration Plan

Merge the workflow change; no rollout, no state. Rollback is reverting the three `concurrency`
blocks. Existing in-flight runs are unaffected.

## Open Questions

None.
