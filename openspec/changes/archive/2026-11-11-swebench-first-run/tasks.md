## 1. Run definition

- [x] 1.1 Add `eval/swebench/instances.txt` with the deterministic five-instance stride and its selection rule.
- [x] 1.2 Add `eval/swebench/run.json` with the budget and frontier models, one cost cap, the timeout, the evaluation workers and the baseline settings.
- [x] 1.3 Add `eval/swebench/litellm-models.json` so the baseline can price the two new model ids.

## 2. Adapter hardening found by the first live run

- [x] 2.1 Set `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` in the instance container.
- [x] 2.2 Default to `SWE-bench/SWE-bench_Verified` and write the resolved image into `dataset.json`.
- [x] 2.3 Find the official summary that `swebench` 5.0.2 writes (`<model>.<run_id>.json`) as well as the legacy `results.json`.
- [x] 2.4 Exclude core dumps from `patch.diff` and `predictions.jsonl`.
- [x] 2.5 Add `--max-cost`, `--max-time` and `--max-workers` to the adapter and forward them.
- [x] 2.6 Extend the adapter tests for every fix; run the two adapter test files.

## 3. Run orchestrator

- [x] 3.1 Add `scripts/eval/swebench_run_support.py`: definition/subset loading, trajectory totals, catalog pricing, official counts and the summary renderer.
- [x] 3.2 Add the Lunate arm: one invocation per instance/model, remaining budget as `--max-cost`, official evaluation.
- [x] 3.3 Add the mini-swe-agent arm on the same subset/models with the LiteLLM registry and official evaluation.
- [x] 3.4 Add `tests/scripts/test_swebench_run.py` and run it.

## 4. The published run

- [x] 4.1 Run the Lunate arm on all five instances with both models; keep every failed or unresolved instance.
- [x] 4.2 Run the mini-swe-agent baseline on the same five instances and both models.
- [x] 4.3 Commit `eval/swebench/summary.json` with the official counts, the spend against the cap and the failure list.

## 5. Write-up and claim

- [x] 5.1 Write `docs/eval/swebench-first-run.md`: the numbers, the cost, the failures and what the run does and does not show.
- [x] 5.2 Update the README C# claim to cite that write-up and only the measured result.
- [x] 5.3 Document the run entry point, the pinned files and the baseline command in `eval/README.md`.

## 6. Close-out

- [x] 6.1 Run the focused checks: the three Python test files, `openspec validate swebench-first-run --strict`, `openspec validate --all --strict`, `bash scripts/check-openspec-skills.sh`, `dotnet csharpier check .` and `git status --short`.
- [x] 6.2 Sync the benchmark-harness spec and archive `swebench-first-run` as the final commit on the branch.
