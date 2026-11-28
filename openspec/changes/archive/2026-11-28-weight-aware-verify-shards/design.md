## Context

`scripts/verify.sh --shard i/n` splits the solution's test projects across the matrix legs
(`.github/workflows/ci.yml`, four shards on Ubuntu and Windows, two on macOS). The split is
positional: `scripts/test-parallel.sh` numbers the `LC_ALL=C`-sorted project list and shard `i` takes
every project whose index is `i-1 mod n`. Every shard builds the whole solution and runs both culture
passes over its slice, so a lane's wall time is roughly `4 × build + Σ(per-shard test wall)`.

The recorded Windows lane (PR #764, `verify-timing-win-x64-*` artifacts) is
`561 / 900 / 162 / 467` s, summed 2090 s. Shard 2/4 is 43 % of that: under the position rule it owns
`PermissionGate`, `MemoryProvider`, `WorktreeTasks`, `Lunate.Coding.Tests`, `Extensibility.Testing` and
`Tui.Tests`. `Lunate.Coding.Tests` is the project the repository already knows as process-bound (its
own comment in `test-parallel.sh` records 3m46s alone on the reference Mac, spent waiting on children),
and on the Windows runner one culture pass of it costs about 390 s: 900 − ~120 build = 2 × 390, i.e.
the shard's wall time is dominated by its heaviest module, because the test platform runs a shard's
modules concurrently. Linux (34.7 %) and macOS (36.6 %) are the same shape, smaller.

The change is confined to the gate scripts: no src project, no public API, no session format, no
dependency.

## Goals / Non-Goals

**Goals:**

- No shard carries a whole slow project: a project's two culture passes are placed independently.
- At four shards no shard is above a third of its lane's summed recorded wall time, on the Ubuntu and
  Windows lanes, from one table the shards read without coordinating.
- Within the macOS lane's two shards, the weight partition is no heavier than the position partition it
  replaced, and the gate reports both numbers.
- The partition stays complete, non-overlapping and deterministic, enforced by `scripts/gate-tests.sh`.
- The weights are recorded, not estimated: a real run writes per-project wall times, and one command
  folds them back into the table.

**Non-Goals:**

- Splitting `Lunate.Coding.Tests` into two projects; the pass-level unit makes that unnecessary here and
  a project split stays a separate change if the measured numbers ever ask for it.
- Changing the number of shards a lane runs: Ubuntu and Windows stay at the dispatch's count (four by
  default) and macOS keeps its two-shard cap. The card claims the third for Ubuntu and Windows and gives
  macOS the best-wall-time-within-two-shards acceptance instead, because two shards leave one carrying
  at least half the lane and no partition can meet the third there (#789).
- Changing the matrix's events, the artifact names, the retry/flake budget or `verify.ps1` (the Windows
  twin runs the gate unsharded).
- Making the weights authoritative for correctness: they decide balance only.

## Decisions

### The unit is one culture pass of one project

A shard's wall time is not the sum of its modules' times: the test platform starts a shard's modules at
once, so the leg is bounded by its heaviest module across both passes. Partitioning whole projects
therefore cannot lower the pole — the pole is `Lunate.Coding.Tests`' own cost, twice. That is the
measurement behind the card's "no shard count can balance the Windows lane while that project runs as
one unit", and behind its alternative: split the culture passes so they can be sharded.

This change takes that alternative inside the scheduler: the unit is `(project, culture pass)`, and the
two passes of a project are two units that carry the same recorded weight. Placed largest-first, they
land in different shards whenever the project is heavier than a shard's fair share, so the Windows pole
becomes `build + one pass` instead of `build + two passes`. A small project's two passes may also land
apart; that is harmless (they already run as separate `dotnet test` invocations with separate
environments) and it keeps the rule one rule instead of a special case for the pole.

Alternative considered: keep the project as the unit and split `Lunate.Coding.Tests` into two test
projects. Rejected for this card — it moves test files, fixtures and golden sessions for a benefit the
pass-level unit already delivers, and the card offers it as the alternative, not the requirement.

### Longest processing time first, over the recorded table

`verify_shard_plan` reads `scripts/verify-shard-weights.tsv`, builds one unit per culture pass, sorts by
recorded weight descending (ties by project path, then by pass, so the order never depends on the shell
or the platform), and drops each unit into the currently least-loaded shard, ties to the lowest shard
number. That is the classic LPT rule. `gate-tests.sh` enforces its bound on the fair share — the
busiest shard is at most `(4n-1)/(3n)` of the lane's weight divided by the shard count — which at four
shards is 31.25 %, inside the card's third, and it adds the card's stricter third on top at the shard
count Ubuntu and Windows run, read from the workflow so a lane that is given fewer shards fails the gate
instead of being checked against a weaker bound. macOS is read from the same workflow: the gate fails
unless the lane is capped at two, and reports the two-shard pole beside the position partition's so the
card's macOS acceptance is a number in the gate's own output rather than a claim.

Before the rest, the heaviest unit of each pass is handed out once per shard. That is the first move
LPT would make anyway, and it is what keeps every shard running both culture passes when the recorded
weights are flat: without it, equal weights make the placement alternate passes, and a shard can end up
owning a pass it never runs.

Shard 1 starts pre-loaded with the table's `#primary` weight: that leg pays the publish, the startup
budget, the install/tool/extension roundtrips and the two `Category=Perf` budgets on top of its tests,
and a partition that ignores them hands the pole to shard 1 by construction.

The partition is computed independently inside each shard from the same table, which is what makes the
matrix work without coordination: no shard needs another shard's result, so a shard can be re-run
alone. Determinism is a checked property, not a hope.

### The table is recorded, reviewed and refreshable

`scripts/verify-shard-weights.tsv` holds the recorded wall time of one culture pass per test project,
plus the `#primary` weight. It is a ratchet point: reviewed like code, and refreshed from a real run
rather than re-estimated. Every gate run now calls `shard-weights.sh record`, which parses the test
platform's own per-module summary out of the pass output and writes
`artifacts/verify-timing/verify-<rid>-<shard>-of-<n>-<pass>-projects.jsonl`; the culture pass is part of
the name because a shard runs both passes and one file per (shard, pass) keeps both records.
`shard-weights.sh weights --from <dir>` folds a lane's eight records into a table, taking the slower of
a project's two recorded passes so the heavier pass is what the partition compensates for.

A project with no row gets `VERIFY_SHARD_DEFAULT_WEIGHT`, so adding a test project never fails the
gate; a row for a project `lunate.slnx` no longer has fails `gate-tests.sh`, so a rename or a deletion
is what triggers a refresh. Weights only move units between shards — completeness never depends on
them.

### Both runners read the same assignment

The CI path (`dotnet test --test-modules`) and the pooled local runner (`LUNATE_VERIFY_JOBS`) take
their project list from the same plan, per culture pass. The pass checker iterates the pass's own
queue, which is also the fix for a sharded pooled run reporting every project of the other shards as
"produced no result".

### The architecture and its data flow

Lightweight C4, dynamic: one gate run, one lane.

```
  CI matrix leg                    verify.sh --shard i/n
  (os, rid, i, n)
        |                                   |
        v                                   v
  +----------------+            +--------------------------+
  | plan job       |            | shard-timing.sh          |
  | (shard count)  |            |  record   (wall time)    |
  +----------------+            |  projects (per project)  |
                                +--------------------------+
                                           |         ^
                                           v         |
                     +---------------------------------------+
                     | test-parallel.sh                      |
                     |  verify-shard-weights.tsv (recorded)  |
                     |    -> verify_shard_plan (LPT)         |
                     |    -> verify_shard_projects <pass>    |
                     |    -> verify_shard_modules  <pass>    |
                     +---------------------------------------+
                              |                  |
                              v                  v
                     dotnet test --test-modules   artifacts/verify-timing/
                     (default pass)               verify-<rid>-<i>-of-<n>.json
                     dotnet test --test-modules   verify-<rid>-<i>-of-<n>-<pass>-projects.jsonl
                     (de-AT pass)                        |
                                                         v
                                              shard-weights.sh weights --from
                                                         |
                                                         v
                                              scripts/verify-shard-weights.tsv
```

- Boundary: the weight table is the only state the partition shares between legs; it is committed, so
  a leg's assignment is a pure function of the table and the shard spec.
- The record path is write-only for the run; nothing in the gate reads its own artifact back.
- The gate self-test (`gate-tests.sh`) is the third consumer: it partitions with the same functions to
  prove completeness, non-overlap, determinism and the bound.

## Risks / Trade-offs

- **The table goes stale.** Balance degrades slowly, never correctness; the recorder and the stale-row
  check make the drift visible, and the PR that changes a suite's cost is where the refresh belongs.
- **The weights are one reference measurement for all lanes.** Relative cost is a property of the suite
  (process-bound projects are slow everywhere); a lane that disagrees is corrected by refreshing the
  table from its own run. The third holds only at four shards: below four the fair share is at least
  half the lane, which is why the card claims it for Ubuntu and Windows and caps macOS at two with the
  best-wall-time acceptance instead of a bound no partition can meet. LPT's own guarantee stays checked
  at every count `n`, macOS's two included, as the fallback for a baseline dispatch that picks fewer
  shards.
- **A split pass changes what a shard runs.** A failure is reported against the same project and pass
  as before (`verify_check_test_pass` prints per project), and the retry budget is per pass, unchanged.
- **Rollback:** dropping `--shard` to 1/1 or emptying the table both keep the gate complete
  (default weights fall back to the recorded default); reverting the commit restores the position
  rule with no data migration.

## Migration Plan

1. Land the table with the first recorded run's numbers and the recorder that produces them.
2. Refresh the table from the head's own `verify-timing` artifacts after the first CI run, whose pole
   is the before/after evidence for the card.

## Open Questions

None. The card claims the third for the Ubuntu and Windows lanes, which run the dispatch's four
shards, and keeps the macOS lane at two with the best-wall-time acceptance; a manual dispatch that
picks fewer shards is a baseline measurement held to LPT's bound.
