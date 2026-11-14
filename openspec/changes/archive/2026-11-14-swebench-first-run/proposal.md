## Why

The public C# claim currently rests on a six-task suite that every tool passes, so it cannot support a statement about real repository-scale work. #623 built the SWE-bench adapter but proved it only against a fake Docker client; #624 publishes the first run on real Verified instances, records its spend and failures, and makes the README claim cite that evidence instead of intent.

## What Changes

- Pin a reproducible pilot subset of SWE-bench Verified instances in `eval/swebench/instances.txt`, with the deterministic selection rule that produced it, and pin one budget OpenCode Go model and one frontier OpenRouter model in `eval/swebench/run.json`.
- Give the run exactly one cost cap (US$30) and one wall-clock limit, pass the remaining budget to each Lunate invocation as `--max-cost` and to mini-swe-agent as its cost limit, and report the final spend against the cap.
- Add `scripts/eval/swebench_run.py`, the run orchestrator: it reads the pinned definition, runs the Lunate arm through the adapter, runs the same subset and models through the mini-swe-agent baseline, and aggregates the official evaluation results, spend and failures into committed evidence. The official harness remains the only scorer.
- Harden the adapter with what the first real instance exposed: set invariant globalization in images that ship no ICU, default to the current dataset with `image` and `eval_script`, write the resolved image into `dataset.json`, find the summary filename that `swebench` 5.0.2 writes, keep a crash core dump out of the patch, and forward the cost, time and worker limits.
- Commit the completed run evidence under `eval/swebench/`: the per-arm summaries and the merged `summary.json` / `summary.md` for `first-run-2026-11-08`.
- Publish `docs/dev/eval/swebench-first-run.md` with the numbers, the spend, the failures and the honest scope, and update the README C# claim only from those measured results.
- Record the published-run contract in the `benchmark-harness` spec: pinned subset and models, one cost cap, the mini-swe-agent baseline on the same subset, official-only scoring and committed evidence.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `benchmark-harness`: the published-run contract — a pinned subset and model matrix, one cost cap per run, the mini-swe-agent baseline on the same subset, official-only scoring, the adapter fixes the first real instance exposed, and the committed evidence that backs the README claim.

## Impact

- New: `eval/swebench/` (subset, run definition, baseline registry, committed summaries), `scripts/eval/swebench_run.py`, `scripts/eval/swebench_baseline.py`, `scripts/eval/swebench_run_support.py`, `scripts/eval/swebench_process.py`, `docs/dev/eval/swebench-first-run.md`.
- Modified: `scripts/eval/swebench.py` and its support/execution modules (cost/time/worker flags, invariant globalization, current dataset default, resolved image, summary filename, core-dump exclusion), `eval/README.md`, `README.md`, `scripts/verify.sh`, `openspec/specs/benchmark-harness/spec.md`.
- Tests: Python tests for the pinned stride, run definition, spend and cap accounting, baseline spend and the adapter changes; no live API and no Docker in the test suite.
- No new NuGet package, no `src/` change and no public product API change. The cost/time flags come from #622; the adapter comes from #623.
