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

The change is confined to the gate scripts, the verify matrix and the test projects: no product code,
no public API, no session format, no dependency.

## Goals / Non-Goals

**Goals:**

- No shard carries a whole slow project: the process-bound half of the Coding suite is its own test
  project, and every test project is placed whole in exactly one shard.
- At four shards no shard is above a third of its lane's summed recorded wall time, on the Ubuntu and
  Windows lanes, from one table the shards read without coordinating.
- Within the macOS lane's two shards, the weight partition is no heavier than the position partition it
  replaced, and the gate reports both numbers.
- The partition stays complete, non-overlapping and deterministic, enforced by `scripts/gate-tests.sh`.
- The weights are recorded, not estimated: a real run writes per-project wall times, and one command
  folds them back into the table.

**Non-Goals:**

- Splitting every suite, or moving the test files out of the source folders they mirror. Only the
  process-bound Coding half moves into its own project, and its files are linked by the new project.
- Changing the number of shards a lane runs: Ubuntu and Windows stay at the dispatch's count (four by
  default) and macOS keeps its two-shard cap. The card claims the third for Ubuntu and Windows and
  gives macOS the best-wall-time-within-two-shards acceptance instead, because two shards leave one
  carrying at least half the lane and no partition can meet the third there (#789).
- Changing the matrix's events, the artifact names, the retry/flake budget or `verify.ps1` (the Windows
  twin runs the gate unsharded).
- Making the weights authoritative for correctness: they decide balance only.

## Decisions

### The unit is one test project, and the slow suite is split

A shard's wall time is not the sum of its modules' times: the test platform starts a shard's modules at
once, so the leg is bounded by its heaviest module across both passes. The pole was not the number of
passes alone but the fact that `Lunate.Coding.Tests` carried every process-bound test as one assembly
in whatever shard owned it. Two changes remove that:

- the unit is a whole `lunate.slnx` test project, with both culture passes in the same shard, so the
  completeness clause is literal: every project is in exactly one shard; and
- the Bash and Process test files are compiled by `Lunate.Coding.Process.Tests`, a second test project,
  while the files stay under `tests/Lunate.Coding.Tests/Bash` and `.../Process` so the layout rule still
  sees them under the folders they mirror. The old project removes exactly the `*Tests.cs` and
  `*Tests.*.cs` files in those folders; the new project links the whole folders plus the shared support
  they use.

The card offers exactly this as its alternative ("split `Lunate.Coding.Tests` so its culture passes can
be sharded"), and the split is what makes the Windows pole drop: the shard that owned the old suite now
owns at most one of its two smaller projects, not the whole 2 × ~390 s block.

Alternative considered: keep the project as the unit but place each `(project, culture pass)` as its own
unit. Rejected: it violates the acceptance's "every `lunate.slnx` test project in exactly one shard",
and it splits all projects' passes to hide a pole that belongs in the suite's own shape.

### Longest processing time first, over the recorded table

`verify_shard_plan` reads `scripts/verify-shard-weights.tsv`, builds one unit per test project with its
recorded weight counted for both passes, sorts by recorded weight descending (ties by project path, so
the order never depends on the shell or the platform), and drops each project into the currently
least-loaded shard, ties to the lowest shard number. That is the classic LPT rule, and it is a pure
function of `(table, shard count)` so every leg computes the same assignment.

Shard 1 starts pre-loaded with the table's `#primary` weight: that leg pays the publish, the startup
budget, the install/tool/extension roundtrips and the two `Category=Perf` budgets on top of its tests,
and a partition that ignores them hands the pole to shard 1 by construction.

`gate-tests.sh` enforces LPT's bound on the larger of the lane's fair share and its heaviest project,
because no partition can beat a single project heavier than the fair share. On the recorded table at
four shards the heaviest project alone is over a quarter of the lane, so the fair-share-only form of
the bound would reject the best possible partition; the gate names the lower bound it used when it
fails.
On the recorded table the four-shard Ubuntu and Windows loads are `704 / 960 / 704 / 704` s of
3072 s, a pole of 31.25 %, inside the third. The position partition's own run on `main`
(`46105412860`, commit 218aa75d) measured the Windows verify step at `748 / 887 / 134 / 480` s, with a
887 s pole; the issue's earlier record was `561 / 900 / 162 / 467` s, a 900 s pole. The Windows
measurement run on the split (`measure-timing-win-x64-*`, run 46114774702, commit 7fb6c096) measured
`715 / 759 / 517 / 331` s from verify-start to verify-end, so the pole fell to 759 s (14.4 % below the
same run's 887 s and 15.7 % below the issue's 900 s). The final table restores `#primary`, which moves
the 480 s `Lunate.Extensibility.Testing.Tests` from the 715 s shard that carried it beside ten small
projects to a shard of its own; the Coding shard remains the measured 759 s pole, now beside a lighter
set of projects, so the final `#primary`-aware assignment's pole is the measured 759 s rather than the
715 s shard it splits.

macOS is measured at two shards rather than argued from the weights. The local same-machine run of the
test phase measured the position partition at `172` s and `3627` s and the weight partition at `954` s
and `966` s, so the busiest shard fell from 3627 s to 966 s; the position shard's de-AT pass includes
the sandbox's process-tree timeout, recorded here as the environment skew rather than hidden. The
position partition's own CI run (`46105412860`) measured the macOS verify step at `398` and `913` s, a
913 s pole. The gate reports the recorded-weight comparison -- the weight partition's busiest shard at
1536 s against the position partition's 2216 s -- and the predicted wall pole at 960 s against 1070 s.
The `#primary`-aware final assignment was not separately run on a CI macOS runner in this session;
the same-machine two-shard measurement above is the before/after evidence, and the gate's prediction is
the enforceable comparison.

### The table is recorded, reviewed and refreshable

`scripts/verify-shard-weights.tsv` holds the recorded wall time of one culture pass per test project,
plus the `#primary` weight. It is a ratchet point: reviewed like code, and refreshed from a real run
rather than re-estimated. Every gate run now calls `shard-weights.sh record`, which parses the test
platform's own per-module summary out of the pass output and writes
`artifacts/verify-timing/verify-<rid>-<shard>-of-<n>-<pass>-projects.jsonl`; the culture pass is part of
the name because a shard runs both passes and one file per (shard, pass) keeps both records. The
recorder runs on a pass that fails after its retry budget as well as on a green one, and the pooled
runner concatenates its per-project logs into the same record, so a red leg and an opt-in local run do
not lose the wall times they measured. `shard-weights.sh weights --from <dir>` folds a lane's records
into a table, taking the slower of a project's two recorded passes so the heavier pass is what the
partition compensates for.

A project with no row gets `VERIFY_SHARD_DEFAULT_WEIGHT`, so adding a test project never fails the
gate; a row for a project `lunate.slnx` no longer has fails `gate-tests.sh`, so a rename or a deletion
is what triggers a refresh. The gate also requires the `#primary` row, so a table refresh cannot
silently drop shard 1's non-test load. Weights only move projects between shards — completeness never
depends on them.

### Both runners read the same assignment

The CI path (`dotnet test --test-modules`) and the pooled local runner (`LUNATE_VERIFY_JOBS`) take
their project list from the same plan, for each culture pass. The pass checker iterates the pass's own
queue, which is also the fix for a sharded pooled run reporting every project of the other shards as
"produced no result". The new process project is in `SERIAL_TEST_PROJECTS` with the other process-bound
suites, so the pooled runner gives it its own lane.

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
  prove completeness, non-overlap, determinism, the third and the LPT bound.

## Risks / Trade-offs

- **The table goes stale.** Balance degrades slowly, never correctness; the recorder and the stale-row
  check make the drift visible, and the PR that changes a suite's cost is where the refresh belongs.
- **The split links source files across project directories.** It keeps the layout rule and the test
  names (`RootNamespace` stays `Lunate.Coding.Tests`) intact, at the cost of a new project file that
  must list the shared support it compiles; a missing entry fails its build, not the gate silently.
- **The weights are one reference measurement for all lanes.** Relative cost is a property of the suite
  (process-bound projects are slow everywhere); a lane that disagrees is corrected by refreshing the
  table from its own run. The third holds only at four shards: below four the fair share is at least
  half the lane, which is why the card claims it for Ubuntu and Windows and caps macOS at two with the
  best-wall-time acceptance instead of a bound no partition can meet.
- **A project split changes what a shard runs, not which tests exist.** A failure is still reported
  against the project that owns the file and the same culture pass; the retry budget is per pass,
  unchanged.
- **Rollback:** dropping `--shard` to 1/1 or emptying the table both keep the gate complete (default
  weights fall back to the recorded default); reverting the commits restores the position rule and the
  single Coding project with no data migration.

## Migration Plan

1. Land the table with the first recorded run's numbers, the recorder that produces them, and the
   split's two Coding rows.
2. Refresh the table from the head's own `verify-timing` artifacts after the first CI run, whose pole
   is the before/after evidence for the card.

## Open Questions

None. The card claims the third for the Ubuntu and Windows lanes, which run the dispatch's four
shards, and keeps the macOS lane at two with the best-wall-time acceptance; a manual dispatch that
picks fewer shards is a baseline measurement held to LPT's bound.
