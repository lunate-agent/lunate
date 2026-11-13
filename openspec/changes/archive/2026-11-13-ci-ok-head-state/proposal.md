## Why

`ci-ok` reports on the run that happened to wake it, not on the live head: it reads `draft` from
the run's frozen trigger payload and its own `needs` results. A rerun of a draft-triggered run
therefore creates a newer red `ci-ok` on a head whose ready run already passed (#815), and a draft
run shares a concurrency group with the ready run, so it can cancel the run that would have proved
the head (#816). The lane relies on `ci-ok` meaning "the required gates passed on this head"; a
verdict that depends on when it ran is the same defect class as a gate that fails open.

## What Changes

- `scripts/ci-ok.sh` decides a pull-request verdict from the **live pull request and the live head
  commit's check runs via the GitHub API**, never from the run's `draft` payload or its own `needs`
  results. It reads the pull request's current draft state, resolves the newest attempt of each
  required job across all runs on the live head, ignores cancelled and draft-payload attempts, and
  reuses the existing profile/path decision rules.
- A ready gate whose required jobs are still queued or running fails with **"gate still running"**
  and names the pending jobs. It neither reports green nor turns its own stale payload into a red.
- Draft and ready pull-request runs use separate concurrency groups, so a draft run cannot cancel a
  ready run. Within a group, a newer run of the same obligation still supersedes the older one.
- `scripts/ci-ok.sh --self-test` gains API-shaped fixtures for the #815 draft-rerun case (green when
  the ready gate passed, red when it failed), the same head shapes with an older ready run rerun that
  re-executes its jobs (green when it passed, red when it failed), the #816 cancelled-leg case, a
  still-running gate and a live draft. It also pins the two pull-request concurrency groups.
- The live gate is complete only when every leg of the plan's verify matrix has a check run on the
  head: the shard count comes from the legs that ran, and a missing shard or a missing OS leg fails
  the aggregate with the leg named, so a matrix that shrank cannot pass on its surviving shards. The
  self-test ties the OS list to the plan job's own `leg` lines and covers a gate that lost one shard
  and a gate that lost a whole OS.
- `merge_group`, push, schedule and manual-dispatch decisions keep the existing path and behaviour.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `ci-pipeline`: the aggregate merge gate is defined over the live head's check runs and the live
  draft state, with a named "gate still running" outcome and separate draft/ready run concurrency.

## Impact

`scripts/ci-ok.sh`, a new `scripts/ci-ok-state` helper (Python standard library), its fixtures under
`tests/fixtures/ci-ok/`, `.github/workflows/ci.yml` (concurrency, the `ci-ok` job's API permissions
and token), `changes/<pr>.md`, and the `ci-pipeline` spec. No product code, public API, dependency or
session format changes.
