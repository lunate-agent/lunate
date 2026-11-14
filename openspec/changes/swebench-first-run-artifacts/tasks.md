## 1. Committed evidence

- [x] 1.1 Add `eval/swebench/runs/first-run-2026-11-08/` with one `official-results.json` per instance and arm, transcribed from the committed arm records.
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
