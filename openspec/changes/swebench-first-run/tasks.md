## 1. Run definition and evidence

- [x] 1.1 Add `eval/swebench/instances.txt` with the deterministic five-instance stride and its selection rule.
- [x] 1.2 Add `eval/swebench/run.json` with the budget and frontier models, one cost cap, the timeout, the evaluation workers and the baseline settings.
- [x] 1.3 Add `eval/swebench/litellm-models.json` and `eval/swebench/mini-swe-agent.yaml` so the baseline can price the two model ids and send the OpenCode Go session header.
- [x] 1.4 Commit the completed run summaries: `first-run-2026-11-08.lunate.json`, `first-run-2026-11-08.baseline.json`, `summary.json` and `summary.md`.
- [x] 1.5 Add `tests/fixtures/swebench/verified-test-ids.txt` and a test that the pinned list is exactly indices 0, 100, 200, 300 and 400 of the official order.

## 2. Adapter hardening found by the first live run

- [x] 2.1 Add failing adapter tests for the resolved image, the current dataset default, the cost/time/worker flags, invariant globalization, the official summary filename and the core-dump exclusion.
- [x] 2.2 Set `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` in the instance container.
- [x] 2.3 Default to `SWE-bench/SWE-bench_Verified` and write the resolved image into `dataset.json`.
- [x] 2.4 Find the official summary that `swebench` 5.0.2 writes (`<model>.<run_id>.json`) as well as the legacy `results.json`.
- [x] 2.5 Exclude core dumps from `patch.diff` and `predictions.jsonl`.
- [x] 2.6 Add `--max-cost`, `--max-time` and `--max-workers` to the adapter and forward them.

## 3. Run orchestrator and baseline

- [x] 3.1 Add failing tests for definition/subset loading, trajectory totals, catalog pricing, official counts, the summary renderer and the cap/unknown-spend rules.
- [x] 3.2 Add `scripts/eval/swebench_run_support.py`: definition/subset loading, trajectory totals, catalog pricing, official counts and the summary renderer.
- [x] 3.3 Add the Lunate arm: one invocation per instance/model, remaining budget as `--max-cost`, official evaluation, and resume support.
- [x] 3.4 Add the mini-swe-agent arm on the same subset/models with the LiteLLM registry, its own cost limit and official evaluation.
- [x] 3.5 Add `scripts/eval/swebench_run.py`, the arm/merge CLI, and wire `test_swebench_run.py` into `scripts/verify.sh`.

## 4. Write-up and claim

- [x] 4.1 Write `docs/dev/eval/swebench-first-run.md`: the numbers, the cost, the failures and what the run does and does not show.
- [x] 4.2 Update the README C# claim to cite that write-up and only the measured result.
- [x] 4.3 Document the run entry point, the pinned files and the baseline command in `eval/README.md`.
- [x] 4.4 Add the changelog fragment `changes/<pr-number>.md`.

## 5. Close-out

- [x] 5.1 Run the focused Python tests, `openspec validate swebench-first-run --strict`, `openspec validate --all --strict`, `bash scripts/check-openspec-skills.sh`, `python3 scripts/docs-check --check`, `dotnet csharpier check .` and `git status --short`.
- [x] 5.2 Sync the `benchmark-harness` spec and archive `swebench-first-run` as the final commit on the branch.
