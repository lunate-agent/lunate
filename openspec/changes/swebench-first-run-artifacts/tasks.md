## 1. Committed evidence

- [x] 1.1 Add `eval/swebench/runs/first-run-2026-11-08/` with one `official-results.json` per instance and arm, transcribed from the committed arm records. _(superseded by 5.1: the maintainer's rerun supplies the real predictions and official reports)_
- [x] 1.2 Add the run's evidence README: the layout, the official-result fields, and what the run host did not retain.
- [x] 1.3 Add `eval/swebench/runs/.gitignore` for a working tree's bulk (trajectories, logs, resolved dataset).
- [x] 1.4 Re-point every instance row of `summary.json`, `first-run-2026-11-08.lunate.json` and `first-run-2026-11-08.baseline.json` at the committed evidence directories.

## 2. Durable orchestrator links

- [x] 2.1 Fail a test that the orchestrator's default `--work-dir` is the tracked `eval/swebench/runs`.
- [x] 2.2 Fail a test that every committed summary row links a committed official result whose verdict agrees with the row.
- [x] 2.3 Default `--work-dir` to `eval/swebench/runs` and document it.
- [x] 2.4 Update `eval/README.md` for the default and the committed evidence tree.

## 3. Write-up

- [x] 3.1 Replace the write-up's `eval/runs/` audit note with the committed evidence links and the stated gap for the text and trajectories the host did not retain.

## 4. Close-out

- [x] 4.1 Add the changelog fragment line for this change in `changes/839.md`.
- [x] 4.2 Run `python3 tests/scripts/test_swebench_run.py`, the docs checks, `python3 scripts/check-spec-archive.py` and `openspec validate --all --strict`.
- [x] 4.3 Sync the `benchmark-harness` spec and archive `swebench-first-run-artifacts` as the final commit on the branch.

## 5. Rerun evidence (rework after the maintainer decision)

- [x] 5.1 Replace `eval/swebench/runs/first-run-2026-11-08/` and the two first-run arm records with `eval/swebench/runs/rerun-624-bosgame-2/`: 20 submitted predictions, 17 official per-instance `report.json` files, the run summary and an evidence README (132 KiB, under the 5 MB limit).
- [x] 5.2 Re-point every instance row of `eval/swebench/summary.json` at the rerun's evidence and record the published run's US$5.00 cap in `eval/swebench/run.json`.
- [x] 5.3 Update the committed-evidence test to require each row's prediction and, when the row reports a patch, its official report whose verdict agrees with the row.
- [x] 5.4 Rewrite the write-up and the README claim from the rerun's numbers, failures, spend and scope, and state that the baseline was ahead.
- [x] 5.5 Re-run the named checks and re-archive the change as the final commit.
