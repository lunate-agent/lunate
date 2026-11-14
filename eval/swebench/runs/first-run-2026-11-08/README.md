# Run evidence — first-run-2026-11-08

This directory holds the durable per-instance evidence behind
[`eval/swebench/summary.json`](../../summary.json) and the write-up
[`docs/dev/eval/swebench-first-run.md`](../../../../docs/dev/eval/swebench-first-run.md).

## Layout

| Path | Contents |
| --- | --- |
| `lunate/<tier>-<instance>/official-results.json` | The official `swebench.harness.run_evaluation` summary for that instance's evaluation: the ids it resolved, left unresolved or recorded as an empty patch. |
| `baseline/<tier>-<instance>/official-results.json` | The same record for the mini-swe-agent submission, scored by the same official harness. |

Every instance row in `summary.json` has a `workDir` that points at the directory carrying its
record, so a reader of a clean checkout can follow a resolved count back to the official verdict:
the `resolved_ids` and `unresolved_ids` in the record are exactly the `resolved` and `failure`
values of the row.

## What is not here

- **Trajectories.** The `lunate -p --json` event stream and the mini-swe-agent `.traj.json` files
  stayed in the run's working tree on the run host, which is also where the adapter read the token
  usage and the spend. They were not committed: a trajectory is large, carries the full model
  conversation, and the per-instance spend and step counts it produced are recorded in
  `summary.json`.
- **Prediction patch text.** The patches went to the official harness as `predictions.jsonl` from
  the same run-host working tree. That tree is not part of the repository, so the committed record
  carries each instance's official verdict plus the row's patch presence, not the patch text.
- **Container and evaluation logs** (`docker.log`, `lunate.log`, `evaluation.log`), the resolved
  `dataset.json` and the image pull logs: same reason.

A run started now writes its working tree under this directory — the orchestrator's default
`--work-dir` is `eval/swebench/runs` — so its `workDir` values resolve inside the checkout instead
of a machine-local temporary directory. The `.gitignore` here keeps trajectories and logs out of a
commit and leaves the predictions and the official results to be committed deliberately.
