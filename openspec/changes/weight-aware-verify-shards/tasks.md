# Tasks — weight-aware verify shards

## 1. The weights and the partition

- [ ] 1.1 Add `scripts/verify-shard-weights.tsv`: the recorded wall time of one culture pass per test
      project plus the `#primary` weight of shard 1's non-test steps, with a header naming the run,
      the machine and the refresh command.
- [ ] 1.2 In `scripts/test-parallel.sh`, add the weight lookup (table row, else the recorded default),
      `verify_shard_plan` (one unit per culture pass, largest recorded weight first, LPT into the
      least-loaded shard, shard 1 pre-loaded with `#primary`, deterministic tie-breaks) and
      `verify_shard_stale_rows`; make `verify_shard_projects`, `verify_shard_modules` and
      `verify_shard_queue` take the culture pass and read the plan. Confirm with a sourced shell check
      that the old position rule fails the new per-pass completeness check.

## 2. The runner

- [ ] 2.1 In `scripts/verify.sh`, build one module expression per culture pass and run each pass with
      its own list; record each project's wall time for each pass (default and de-AT) under
      `artifacts/verify-timing/`; keep the pooled path (`LUNATE_VERIFY_JOBS`) on the same plan and make
      `verify_check_test_pass` iterate the pass's own queue so a sharded pooled run reports only its
      own projects. Update the file header's description of `--shard`.
- [ ] 2.2 In `scripts/shard-timing.sh`, add `projects` (parse the test platform's per-module summary out
      of a pass log and write the per-project record) and `weights` (fold a lane's project records into
      a weight table), with self-test cases for the parse, the merge and the refusals.

## 3. The gate

- [ ] 3.1 In `scripts/gate-tests.sh`, replace the position checks with the per-pass weight checks: for
      1, 2, 3 and 4 shards every culture pass of every `lunate.slnx` test project is in exactly one
      shard, no shard is empty, the assignment is byte-identical across calls, no weight row names a
      project the solution lacks, and at four shards no shard's weight (shard 1 including `#primary`)
      exceeds one third of the lane's summed weight, and every shard count stays within LPT's bound.
      Run the new `shard-timing.sh` self-tests too, and prove each check fails on a deliberately broken
      table.

## 4. Documentation

- [ ] 4.1 Update the `.github/workflows/ci.yml` comment on the verify matrix and the `shard-timing.sh`
      header to describe the weight partition and the per-project record; nothing under `docs/`
      describes the shard partition today, so no page changes.
- [ ] 4.2 Write `changes/<pr-number>.md` and commit it before the archive.

## 5. Verification

- [ ] 5.1 Run the sourced partition checks, `bash scripts/shard-timing.sh --self-test`,
      `bash scripts/gate-tests.sh` where the published binary allows it, `bash -n`/`shellcheck`-style
      syntax checks on every touched script, `dotnet csharpier format .` and
      `openspec validate weight-aware-verify-shards --strict`.
- [ ] 5.2 Run `openspec validate --all --strict`, sync, and archive the change as the branch's last
      commit.
