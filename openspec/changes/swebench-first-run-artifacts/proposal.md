## Why

The reviewer of the first published run found the evidence behind the numbers unreachable: `summary.json` recorded each Lunate instance's `workDir` as a `/private/var/.../T` path on the run host, and the write-up pointed the per-instance artifacts at `eval/runs/`, which is git-ignored. A reader of the pull request or of a clean checkout therefore could not follow a resolved count back to the official verdict, while the requirement says the summary must come from a completed run "whose artifacts are linked". The orchestrator made this inevitable: its default `--work-dir` was inside `eval/runs/`, so every new run reproduced the same dead links.

## What Changes

- Commit the published run's per-instance evidence under `eval/swebench/runs/rerun-624-bosgame-2/`: each instance's submitted `predictions.jsonl`, the official `report.json` for every instance that produced a patch, and the run's summary, plus a README that states what the run host retained and what it did not. The rerun replaces the first run's record, whose raw artifacts the host no longer held.
- Point every instance row of `eval/swebench/summary.json` and the write-up at those committed paths instead of the run host's absolute path.
- Default the orchestrator's `--work-dir` to the tracked `eval/swebench/runs`, so a run's `workDir` values resolve inside the checkout, and ignore only the bulk of a working tree there (trajectories, logs, the resolved dataset) rather than the whole tree.
- Add tests that the default work dir is the committed tree and that every committed summary row links a committed prediction and official report whose verdicts agree with the row.

No product code, no `src/` change, no dependency.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `benchmark-harness`: the committed-evidence requirement now names where the per-instance official result is committed, requires each summary row to record a repository-relative path that resolves in a checkout, and states that a run's un-retained raw artifacts are declared rather than reconstructed.

## Impact

- New: `eval/swebench/runs/rerun-624-bosgame-2/` (predictions, official per-instance reports, the run summary and its README), `eval/swebench/runs/.gitignore`.
- Removed: the first run's `eval/swebench/runs/first-run-2026-11-08/` and its two arm records, replaced by the rerun's evidence.
- Modified: `eval/swebench/summary.json`, `eval/swebench/summary.md`, `eval/swebench/run.json` (the published run's US$5 cap), `scripts/eval/swebench_run.py`, `tests/scripts/test_swebench_run.py`, `eval/README.md`, `docs/dev/eval/swebench-first-run.md`, `README.md`, `openspec/specs/benchmark-harness/spec.md`.
- No live API, no Docker and no model call in the change or its tests.
