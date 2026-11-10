## 1. Spend accounting

- [x] 1.1 Add a failing test that an unpriced instance leaves the run total unknown, stops the run and never reports more than the cap.
- [x] 1.2 Fix `run_arm` / `merge` so unknown spend is tracked separately, `totalSpendUsd` is `null`, and `capReached` is true.
- [x] 1.3 Render an unknown total honestly in `summary.md`.

## 2. Pinned stride

- [x] 2.1 Add `tests/fixtures/swebench/verified-test-ids.txt` with the official Verified test ids and their provenance.
- [x] 2.2 Add a test asserting the five pinned ids equal the fixture at indices 0, 100, 200, 300 and 400.

## 3. Baseline run

- [x] 3.1 Smoke-run one instance and both models; confirm the baseline exits with a submission rather than `EOFError`.
- [ ] 3.2 Run the baseline on all five instances with both models into `first-run-2026-11-08`.
- [ ] 3.3 Commit `first-run-2026-11-08.baseline.json` and the merged `summary.json` / `summary.md`.

## 4. Write-up and claim

- [ ] 4.1 Fill the cost, failure and scope sections of `docs/eval/swebench-first-run.md` from the completed run.
- [ ] 4.2 Replace the README benchmark sentence with this run's measured numbers and a link to the write-up.
- [x] 4.3 Document the run entry point, pinned files and baseline command in `eval/README.md`.

## 5. Close-out

- [ ] 5.1 Run the three Python test files, `openspec validate swebench-first-run-evidence --strict`, `openspec validate --all --strict`, `bash scripts/check-openspec-skills.sh`, `python3 scripts/check-spec-archive.py --base origin/main` and `git status --short`.
- [ ] 5.2 Sync the benchmark-harness spec and archive `swebench-first-run-evidence` as the final commit on the branch.
