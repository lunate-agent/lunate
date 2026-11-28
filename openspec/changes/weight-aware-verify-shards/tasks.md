# Tasks — weight-aware verify shards

## 1. The weights and the partition

- [x] 1.1 Add `scripts/verify-shard-weights.tsv`: the recorded wall time of one culture pass per test
      project plus the `#primary` weight of shard 1's non-test steps, with a header naming the run,
      the machine and the refresh command. It carries the repository's recorded reference measurement
      for `Lunate.Coding.Tests` (3m46s a pass) and a `#primary` derived from the same run's full gate
      minus its test phases; every other project uses the default weight until a real run records it.
- [x] 1.2 In `scripts/test-parallel.sh`, add the weight lookup (table row, else the recorded default),
      `verify_shard_plan` (one unit per culture pass, largest recorded weight first, LPT into the
      least-loaded shard, shard 1 pre-loaded with `#primary`, deterministic tie-breaks) and
      `verify_shard_stale_rows`; make `verify_shard_projects`, `verify_shard_modules` and
      `verify_shard_queue` take the culture pass and read the plan. Confirm with a sourced shell check
      that the old position rule fails the new per-pass completeness check.

## 2. The runner

- [x] 2.1 In `scripts/verify.sh`, build one module expression per culture pass and run each pass with
      its own list; record each project's wall time for each pass (default and de-AT) under
      `artifacts/verify-timing/`; keep the pooled path (`LUNATE_VERIFY_JOBS`) on the same plan and make
      `verify_check_test_pass` iterate the pass's own queue so a sharded pooled run reports only its
      own projects. Update the file header's description of `--shard`.
- [x] 2.2 Add `scripts/shard-weights.sh`: `record` parses the test platform's per-module summary out of a
      pass log and writes the per-project record, `weights` folds a lane's project records into a
      weight table, and `--self-test` covers the parse, the merge and the refusals.

## 3. The gate

- [x] 3.1 In `scripts/gate-tests.sh`, replace the position checks with the per-pass weight checks: for
      1, 2, 3 and 4 shards every culture pass of every `lunate.slnx` test project is in exactly one
      shard, no shard is empty, the assignment is byte-identical across calls, no weight row names a
      project the solution lacks, and at four shards no shard's weight (shard 1 including `#primary`)
      exceeds one third of the lane's summed weight, and every shard count stays within LPT's bound.
      Run the new `shard-weights.sh` self-test too, and prove the balance check and the stale-row check
      fail on a deliberately broken table.

## 4. Documentation

- [x] 4.1 Update the `.github/workflows/ci.yml` comment on the verify matrix and the `shard-timing.sh`
      header to describe the weight partition and the per-project record; nothing under `docs/`
      describes the shard partition today, so no page changes.
- [x] 4.2 Write `changes/<pr-number>.md` and commit it before the archive.

## 5. Verification

- [x] 5.1 Run the sourced partition checks, `bash scripts/shard-timing.sh --self-test`,
      `bash scripts/gate-tests.sh` where the published binary allows it, `bash -n`/`shellcheck`-style
      syntax checks on every touched script, `dotnet csharpier format .` and
      `openspec validate weight-aware-verify-shards --strict`.
- [x] 5.2 Run `openspec validate --all --strict`, sync, and archive the change as the branch's last
      commit.

## 6. Rework — both passes kept, and the card's lane counts restored

- [x] 6.1 Give the macOS lane its two-shard cap back in `.github/workflows/ci.yml`
      (`macos_shards=$((SHARDS < 2 ? SHARDS : 2))`, and the macOS leg passes it), so the card's decision
      holds: the third is claimed for Ubuntu and Windows, and macOS is accepted on the best wall time
      within two shards. `scripts/ci-ok.sh`'s workflow self-test asserts both.
- [x] 6.2 In `scripts/gate-tests.sh`, read every lane's shard count out of the workflow and check each
      against its own rule: Ubuntu and Windows at the dispatch's count with the third, macOS at its
      two-shard cap. Print the Ubuntu/Windows pole, and the macOS two-shard pole against the position
      partition it replaced, so the card's numbers are the gate's own output; fail when a lane is given
      the wrong count or when the two-shard weight partition is heavier than the position one. Prove the
      reader and the check on fixture workflows.
- [x] 6.3 In `scripts/shard-weights.sh`, put the culture pass in the per-project record's file name, so a
      shard that runs both passes keeps both, and extend the self-test to the same shard's two passes
      (the fold must still take the slower one and keep a pass-only project).
- [x] 6.4 Update the delta spec, proposal, design and the ADR manifest to the reworked behaviour; write
      the rework line in `changes/<pr-number>.md`.
- [x] 6.5 Report the acceptance numbers from the gate's own output: the Ubuntu and Windows pole at four
      shards, the macOS pole and wall pole within two shards before and after, and the Windows pole's
      before/after from the recorded lane.
