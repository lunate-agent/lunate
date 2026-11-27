## Why

The verify matrix splits the test projects across four legs per OS by position in the sorted project
list (`p % n`), so a heavy project is parked whole in one shard beside five light ones. The recorded
Windows lane is the proof: its shards took 561 s, 900 s, 162 s and 467 s, and the pole is shard 2/4 at
900 s — 43 % of the lane's 2090 s serial total against the balance clause's "no shard more than about a
third", while Linux sits at 34.7 % and macOS at 36.6 %. Shard 2 carries `Lunate.Coding.Tests`, the
project that spends its time waiting on child processes (≈7 minutes per culture pass on the Windows
runner), together with five projects that finish long before it. No shard count can balance a lane
whose pole is one project running as one unit, so the scheduler has to be given the weights.

## What Changes

- Replace the position round-robin with a **weight-aware partition**: the unit is one culture pass of
  one test project, and the units are placed **largest-first** (longest-processing-time) into the
  least-loaded shard. Both culture passes of the pole project therefore land in different shards — the
  card's "split so its culture passes can be sharded" — so no shard carries a whole slow project.
- Add `scripts/verify-shard-weights.tsv`: the recorded wall time of one culture pass of each test
  project, one row per project, plus the recorded cost of shard 1's non-test steps. It is the single
  input the partition reads, in every lane, so the four shards of a lane agree without talking to each
  other.
- Record **per-project** wall times in every gate run, beside the per-shard records
  `scripts/shard-timing.sh` already writes, and add `scripts/shard-weights.sh` to write the per-project
  record and fold a run's records back into the table (`weights --from <dir>`), so the next real run
  refreshes the weights instead of guessing them.
- Give shard 1's non-test work a weight in the partition: it starts pre-loaded with the recorded cost
  of its publish, startup budget, roundtrip, formatting, docs and public-API steps, so the leg that
  owns them is not silently the heaviest.
- Make `scripts/gate-tests.sh` check the property instead of the position rule: every culture pass of
  every `lunate.slnx` test project is in exactly one shard, no shard is empty, the bound rows name
  projects that exist, the partition is deterministic, and for four shards no shard's weight exceeds a
  third of the lane's summed weight.
- Keep the pooled local runner (`LUNATE_VERIFY_JOBS`) on the same assignment: a pass's queue and its
  module list come from one partition, so the two runners cannot disagree about who runs what.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `ci-pipeline`: the verify shards' partition. A new requirement fixes the unit (one culture pass of
  one test project), the order (largest recorded weight first), the recorded weight table as the only
  input, shard 1's non-test pre-load, the balance bound at four shards, and the completeness,
  disjointness, determinism and staleness rules the gate self-tests enforce.

## Impact

- `scripts/test-parallel.sh`: `verify_shard_projects`, `verify_shard_modules`, `verify_shard_queue` and
  the pass checker work per culture pass and read the weight table.
- `scripts/verify.sh`: the test phase asks for a module list per culture pass; the run records its
  per-project wall times; the header comment stops describing a position partition.
- `scripts/shard-weights.sh` (new): a `record` command that writes the per-project record next to the
  shard record, a `weights` command that folds recorded project timings into the table, and the
  self-test that covers both.
- `scripts/gate-tests.sh`: the partition checks become per-pass, weight-aware and table-aware, and the
  shard-timing self-test gains the new commands.
- `scripts/verify-shard-weights.tsv` (new): the recorded weight table.
- `.github/workflows/ci.yml`: the comment on the verify matrix describes the weight partition; no job,
  matrix entry or artifact name changes.
- No new NuGet package, no src change, no public API change, no session-format change.
