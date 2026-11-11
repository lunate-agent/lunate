## Why

#623 built the SWE-bench adapter but proved it only against a fake Docker client; no real
instance has ever produced a published number. The public C# claim currently rests on a six-task
suite that every tool passes, so it cannot support a statement about real repository-scale work.
This change runs the adapter on real Verified instances, records the spend and failures, and makes
the README claim cite that evidence instead of intent.

## What Changes

- Pin a reproducible subset of SWE-bench Verified instances in `eval/swebench/instances.txt`, with
  the selection rule that produced it recorded next to the list.
- Pin the model matrix in `eval/swebench/run.json`: one budget OpenCode Go model and one frontier
  OpenRouter model, the same subset for both.
- Give every run one cost cap (US dollars) and one wall-clock limit, pass them to the agent through
  `--max-cost` / `--max-time`, and report the spend and any limit stop in the summary.
- Add `scripts/eval/swebench_run.py`, the run orchestrator: it reads the pinned subset and matrix,
  invokes the adapter once per model and instance, runs the same subset through mini-swe-agent on
  the same models, and aggregates the official evaluation results, spend and failures into a
  committed summary. The official harness remains the only scorer.
- Harden the adapter with what the first real run exposes: set invariant globalization inside
  images that ship no ICU, forward the cost/time limits, and keep a crash core dump out of the
  prediction patch.
- Publish `docs/eval/swebench-first-run.md` with the numbers, the spend, the failure modes and the
  honest scope, and update the README C# claim only from that evidence.
- Record the published-run contract in the `benchmark-harness` spec: pinned subset and models, one
  cost cap, the baseline on the same subset, official-only scoring and committed evidence.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `benchmark-harness`: the published-run contract — a pinned subset and model matrix, one cost cap
  per run, the mini-swe-agent baseline on the same subset, official-only scoring and the committed
  evidence that backs the README claim.

## Impact

- New: `eval/swebench/` (subset, run config, committed summary), `scripts/eval/swebench_run.py`,
  `docs/eval/swebench-first-run.md`.
- Modified: `scripts/eval/swebench.py` and its support/execution modules (cost and time limits,
  invariant globalization, core-dump exclusion), `eval/README.md`, `README.md`,
  `openspec/specs/benchmark-harness/spec.md`.
- Tests: Python tests for subset/config parsing, limit accounting, summary aggregation and the new
  adapter flags; no live API and no Docker in the test suite.
- No new NuGet package and no public product API change. The cost/time flags come from #622, and
  the adapter comes from #623; both are stacked as dependencies of this branch.
