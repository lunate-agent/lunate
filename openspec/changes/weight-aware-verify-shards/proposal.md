## Why

The verify matrix splits the test projects across four legs per OS by position in the sorted project
list (`p % n`), so a heavy project is parked whole in one shard beside five light ones. The recorded
Windows lane is the proof: its shards took 561 s, 900 s, 162 s and 467 s, and the pole is shard 2/4 at
900 s — 43 % of the lane's 2090 s serial total against the balance clause's "no shard more than about a
third", while Linux sits at 34.7 % and macOS at 36.6 %. Shard 2 carries `Lunate.Coding.Tests`, the
project that spends its time waiting on child processes (≈7 minutes per culture pass on the Windows
runner), together with five projects that finish long before it. No shard count can balance a lane
whose pole is one project running as one unit, so the scheduler has to be given the weights — and the
suite that owns the pole has to stop being one unit.

## What Changes

- Replace the position round-robin with a **weight-aware partition**: the unit is one test project and
  both culture passes run in the same shard, so every project is in exactly one shard. Projects are
  placed **largest-first** (longest-processing-time) into the least-loaded shard, so a slow suite is
  never parked whole beside several light projects.
- Split the process-bound half of `Lunate.Coding.Tests` into `Lunate.Coding.Process.Tests`: the Bash
  and Process test files are compiled by the new project and removed from the old one, while the
  source folders they mirror stay where they are. The old suite's ≈390 s Windows culture pass is then
  two smaller projects that can land in different shards — the card's "split `Lunate.Coding.Tests` so
  its culture passes can be sharded" — so the Windows pole becomes build plus a smaller project's
  pass, not build plus both passes of the whole slow suite.
- Add `scripts/verify-shard-weights.tsv`: the recorded wall time of one culture pass of each test
  project, one row per project, plus the recorded cost of shard 1's non-test steps. It is the single
  input the partition reads, in every lane, so the shards of a lane agree without talking to each
  other.
- Record **per-project** wall times in every gate run, beside the per-shard records
  `scripts/shard-timing.sh` already writes, and teach `scripts/shard-weights.sh` to fold a run's records
  back into the table (`weights --from <dir>`), so the next real run refreshes the weights instead of
  guessing them. Each record names its culture pass, so a shard that ran both passes keeps both and the
  fold takes the slower one. A pass that fails after its retry budget, and each pass of the opt-in
  pooled runner, record too: those legs still ran their projects before they exited.
- Keep the macOS lane on the card's two-shard cap: it does not gain a third shard, because the card
  gives it the best-wall-time-within-two-shards acceptance instead of the third, which no partition can
  meet at two shards. Both partitions are measured at two shards and their busiest-shard wall times are
  recorded before and after, so the acceptance is a measurement and not only the gate's prediction.
- Give shard 1's non-test work a weight in the partition: it starts pre-loaded with the recorded cost
  of its publish, startup budget, roundtrip, formatting, docs and public-API steps, so the leg that
  owns them is not silently the heaviest.
- Make `scripts/gate-tests.sh` check the property instead of the position rule: every `lunate.slnx`
  test project, with both culture passes, is in exactly one shard, no shard is empty, the bound rows
  name projects that exist, the table records shard 1's pre-load, the partition is deterministic, and
  on the Ubuntu and Windows lanes' shard count no shard's weight exceeds a third of the lane's summed
  weight. The gate reads every lane's count out of the workflow, fails when Ubuntu or Windows is given
  fewer than four shards, and fails when macOS is not at its two-shard cap; it reports the macOS
  two-shard pole against the position partition it replaced, which is the card's acceptance for that
  lane.
- Keep the pooled local runner (`LUNATE_VERIFY_JOBS`) on the same assignment: a pass's queue and its
  module list come from one partition, so the two runners cannot disagree about who runs what.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `ci-pipeline`: the verify shards' partition. A new requirement fixes the unit (one test project,
  both culture passes together), the split of the process-bound suite into its own project, the order
  (largest recorded weight first), the recorded weight table as the only input, shard 1's non-test
  pre-load, the Ubuntu and Windows shard count with the third-of-the-lane bound on it, the macOS
  two-shard cap with its best-wall-time acceptance, one per-project record per shard and culture pass,
  and the completeness, disjointness, determinism, staleness and non-zero pre-load rules the gate
  self-tests enforce.

## Impact

- `tests/Lunate.Coding.Process.Tests/` (new): the Bash and Process test files, linked from
  `tests/Lunate.Coding.Tests` so their source folders still mirror `src/Lunate.Coding`, plus the shared
  test support they need; `tests/Lunate.Coding.Tests` no longer compiles those test files.
- `src/*/*.csproj`: `InternalsVisibleTo` entries for the new test project.
- `lunate.slnx`: the new test project, which `scripts/test-parallel.sh` sees and the partition places.
- `scripts/test-parallel.sh`: `verify_shard_plan` places whole projects, both passes together, from
  the weight table; `verify_shard_projects`, `verify_shard_modules` and `verify_shard_queue` read it.
- `scripts/verify.sh`: the test phase runs the same project list in both culture passes; the run
  records its per-project wall times; the header comment describes the project-level partition; the
  process suite is in the serial lane.
- `scripts/shard-timing.sh`: the header describes the per-project record that sits beside its own shard
  record.
- `scripts/shard-weights.sh` (new): `record` writes one per-project record per shard and culture pass,
  and `weights` folds recorded project timings into the table; the self-test covers both.
- `scripts/gate-tests.sh`: the partition checks become project-aware and table-aware; the balance
  check runs at the shard count the lanes actually run, read from the workflow; the shard-weights and
  shard-timing self-tests are required to pass.
- `scripts/verify-shard-weights.tsv` (new): the recorded weight table.
- `.github/workflows/ci.yml`: the comment on the verify matrix describes the project-level weight
  partition, and the macOS lane keeps its two-shard cap while Ubuntu and Windows keep the dispatch's
  count; no other job, artifact name or event changes.
- `scripts/ci-ok.sh`: the workflow self-test keeps the macOS lane on its two-shard cap and the other two
  lanes on the dispatch's count.
- No new NuGet package, no public API change, no session-format change.
