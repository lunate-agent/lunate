## Why

The reviewer of the first published run found the evidence behind the numbers unreachable: `summary.json` recorded each Lunate instance's `workDir` as a `/private/var/.../T` path on the run host, and the write-up pointed the per-instance artifacts at `eval/runs/`, which is git-ignored. A reader of the pull request or of a clean checkout therefore could not follow a resolved count back to the official verdict, while the requirement says the summary must come from a completed run "whose artifacts are linked". The orchestrator made this inevitable: its default `--work-dir` was inside `eval/runs/`, so every new run reproduced the same dead links.

## What Changes

- Commit the run's per-instance evidence under `eval/swebench/runs/first-run-2026-11-08/`: one `official-results.json` per instance and arm, naming the ids the official harness resolved, left unresolved or saw with an empty patch, plus a README that states what the run host retained and what it did not.
- Point every instance row of `eval/swebench/summary.json`, of `first-run-2026-11-08.lunate.json` and of `first-run-2026-11-08.baseline.json`, and the write-up, at those committed paths instead of the machine-local one.
- Default the orchestrator's `--work-dir` to the tracked `eval/swebench/runs`, so a run's `workDir` values resolve inside the checkout, and ignore only the bulk of a working tree there (trajectories, logs, the resolved dataset) rather than the whole tree.
- Add tests that the default work dir is the committed tree and that every committed summary row links a committed official result whose verdict agrees with the row.

No product code, no `src/` change, no dependency.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `benchmark-harness`: the committed-evidence requirement now names where the per-instance official result is committed, requires each summary row to record a repository-relative path that resolves in a checkout, and states that a run's un-retained raw artifacts are declared rather than reconstructed.

## Impact

- New: `eval/swebench/runs/first-run-2026-11-08/` (one official result per instance and arm, plus its README), `eval/swebench/runs/.gitignore`.
- Modified: `eval/swebench/summary.json`, `eval/swebench/first-run-2026-11-08.lunate.json`, `eval/swebench/first-run-2026-11-08.baseline.json`, `scripts/eval/swebench_run.py`, `tests/scripts/test_swebench_run.py`, `eval/README.md`, `docs/dev/eval/swebench-first-run.md`, `openspec/specs/benchmark-harness/spec.md`.
- No live API, no Docker and no model call in the change or its tests.
