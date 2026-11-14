# Run evidence — rerun-624-bosgame-2

This directory holds the durable per-instance evidence behind
[`eval/swebench/summary.json`](../../summary.json) and the write-up
[`docs/dev/eval/swebench-first-run.md`](../../../../docs/dev/eval/swebench-first-run.md).
It replaces the first run's evidence (`first-run-2026-11-08`), whose patch text the run host did
not retain; this run's artifacts were kept and reconciled with the official reports before they
were committed.

## Layout

| Path | Contents |
| --- | --- |
| `summary.json`, `summary.md` | The run's summary exactly as the orchestrator wrote it, with every row's `workDir` rewritten to this directory. |
| `lunate/<tier>-<instance>/predictions.jsonl` | The one prediction line submitted for that instance: `instance_id`, `model_name_or_path` and `model_patch`. |
| `lunate/<tier>-<instance>/report.json` | The official `swebench.harness.run_evaluation` per-instance report: patch presence, resolution, and the `FAIL_TO_PASS`/`PASS_TO_PASS` results it scored. |
| `baseline/<tier>-<instance>/predictions.jsonl` | The same prediction line for the mini-swe-agent submission. |
| `baseline/<tier>-<instance>/report.json` | The same official report for the mini-swe-agent submission. |

Every instance row in [`eval/swebench/summary.json`](../../summary.json) has a `workDir` that points
at one of these directories, so a reader of a clean checkout can follow a resolved count back to the
official verdict: the report's `resolved`, `patch_exists` and `patch_successfully_applied` fields are
exactly what the row's `resolved` and `hasPatch` values came from. Seventeen of the twenty rows have
a report. The other three (`lunate/budget-django__django-13128`, `lunate/budget-sphinx-doc__sphinx-8120`,
`lunate/frontier-django__django-13128`) submitted no non-empty patch, so the adapter did not ask the
official harness to evaluate them and no report exists; their committed prediction line carries the
empty `model_patch` and their row records `resolved: false`.

## What is not here

- **Trajectories.** The `lunate -p --json` event stream and the mini-swe-agent `.traj.json` files
  came from the run host's working tree, `/home/daniel/swebench-624/runs/rerun-624-bosgame-2/`, which
  is not part of the repository. They were not committed: a trajectory is large, carries the full
  model conversation, and the per-instance spend and step counts it produced are recorded in
  `summary.json`.
- **The rest of the working tree.** The adapter's `run.json`, `dataset.json`, `patch.diff`, logs and
  the official harness's evaluation logs and resolved datasets stayed on the run host for the same
  reason.
- **The official harness's run-level summary.** `swebench.harness.run_evaluation` also wrote an
  `official-results.json` per instance that aggregates the same verdicts; it is not committed. The
  per-instance `report.json` is the record that names each test result, and every published number
  is derived from it.

The `.gitignore` in the parent directory keeps trajectories and logs out of a commit. The
orchestrator's default `--work-dir` is `eval/swebench/runs`, so a later run writes its working tree
here and its `workDir` values resolve in the checkout too.
