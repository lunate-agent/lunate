# SWE-bench Verified — first published run

**Date:** 2026-11-08 · **Phase:** 0.4.0 · **Change:** `swebench-first-run` ·
**Adapter:** `scripts/eval/swebench.py` (#623)

This is the first run of the SWE-bench harness adapter on real data. It fixes the adapter where the
fake-Docker tests could not see the official images' behaviour, then publishes the numbers, the
spend and the failures from a fixed subset. The official `swebench.harness.run_evaluation` is the
only scorer.

## Run definition

| Item | Value |
| --- | --- |
| Dataset | `SWE-bench/SWE-bench_Verified`, split `test` |
| Subset | 5 instances: every 100th id in lexicographic order (indices 0, 100, 200, 300, 400) |
| Budget model | `opencode-go/deepseek-v4.1-flash` (OpenCode Go plan) |
| Frontier model | `openrouter/anthropic/claude-opus-5.5` (OpenRouter) |
| Baseline | `mini-swe-agent` 2.4.6, same subset and models, 50-step limit |
| Cost cap | one cap for the whole run, US$30.00; the orchestrator passes the remaining budget to each invocation |
| Step limit | Lunate's default 50; mini-swe-agent capped at 50 for parity |
| Timeout | 1800 s per instance/evaluation |
| Environment | macOS arm64 host; Docker via podman, official `linux/amd64` images under QEMU; .NET needs `DOTNET_EnableWriteXorExecute=0` and `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` |

The pinned ids are in [`eval/swebench/instances.txt`](../../eval/swebench/instances.txt); the full
definition is [`eval/swebench/run.json`](../../eval/swebench/run.json).

## Results

<!-- SUMMARY_TABLE -->

## Cost

<!-- COST -->

## Failures

<!-- FAILURES -->

## What this run does and does not show

<!-- SCOPE -->

## Adapter findings from the first real instance

The fake-Docker tests in #623 could not expose these; the live instance did. Each fix has a test in
`tests/scripts/`.

1. **No ICU in the official images.** The self-contained .NET binary aborted before its first event
   (`Couldn't find a valid ICU package`). The adapter now sets
   `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` in the container.
2. **Stale default dataset.** `princeton-nlp/SWE-bench_Verified` has no `image` or `eval_script`,
   which `swebench` 5.0.2 requires. The default is now `SWE-bench/SWE-bench_Verified`.
3. **Missing image in the local dataset.** The official harness raised `KeyError: 'image'` on the
   adapter's `dataset.json`; the adapter now writes the resolved image into every record.
4. **Wrong summary filename.** `swebench` 5.0.2 writes `<model>.<run_id>.json`, not `results.json`;
   the adapter now finds that name as well.
5. **A crash core dump became the patch.** The QEMU core dump was collected as a 154 MB prediction;
   the collector now excludes `core`, `core.*`, `*.core`, `qemu_*` and `vgcore.*`.

## Reproduce

```bash
dotnet publish src/Lunate.Coding -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true -o artifacts/linux-x64
export OPENCODE_GO_API_KEY=... OPENROUTER_API_KEY=...
python3 scripts/eval/swebench_run.py --arm all --lunate-bin artifacts/linux-x64/lunate \
  --mini-extra mini-extra --run-id <run-id>
```

The orchestrator writes `eval/swebench/summary.json` and `summary.md`; the per-instance patches are
kept under `eval/runs/` (ignored by git) for audit.
