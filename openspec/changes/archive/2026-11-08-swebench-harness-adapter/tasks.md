## 1. CLI and preflight

- [x] 1.1 Add `scripts/eval/swebench.py` with the documented flags (`--instance`, `--dry-run`, `--model`, `--lunate-bin`, `--dataset-file`, `--dataset-name`, `--split`, `--output-dir`, `--run-id`, `--evaluate`, `--env`, `--timeout`, `--keep`) and a usage error path that exits 2 without creating a run directory.
- [x] 1.2 Load the selected instance from a local JSON/JSONL dataset file, or through the evaluation interpreter's `datasets` package when `--dataset-name` is used; reject an instance id that is not in the dataset.
- [x] 1.3 Preflight Docker and its daemon, the ELF/executable Lunate binary, the model id, at least one forwarded provider credential, and the official `swebench` import when evaluation will run; print one reason per failure and exit 1.

## 2. Inference and artifacts

- [x] 2.1 Ensure the official instance image (inspect, then pull) and run it with the run directory mounted, `-e NAME` credential forwarding, `/testbed` as the working directory and `lunate -p --json --yolo` with the instance problem statement.
- [x] 2.2 Record the pre-existing untracked files, run Lunate, and collect a baseline-aware `git diff HEAD --binary` as `patch.diff` while keeping the full `--json` stdout as `trajectory.jsonl`.
- [x] 2.3 Write the per-instance log, exit code, prompt and root `run.json` with relative artifact paths, model, image, timings, forwarded environment names and the Lunate exit status.
- [x] 2.4 Scan every text artifact, the patch and later the predictions/report for forwarded secret values and replace occurrences with `[REDACTED:<NAME>]`, recording the redaction in `run.json`.

## 3. Official evaluation

- [x] 3.1 Write `predictions.jsonl` in the official schema and invoke `python -m swebench.harness.run_evaluation` with the dataset, split, run id, instance ids and predictions path; capture `evaluation.log`.
- [x] 3.2 Locate the official `report.json` and `results.json` under the run directory, copy the instance report next to the artifacts, and print the official verdict without computing a score.
- [x] 3.3 Implement `--evaluate auto|always|never` and the dry-run defaults: a dry run is `never` unless explicitly overridden, and a requested evaluation with a missing official package fails honestly.

## 4. Tests

- [x] 4.1 Add `tests/scripts/test_swebench_adapter.py` with a fake Docker executable, a fake official `swebench.harness.run_evaluation` module and a local dataset; assert the one-instance dry run writes `trajectory.jsonl`, `patch.diff`, `run.json` and the documented exit code.
- [x] 4.2 Test the unit boundaries: image fallback name, non-ELF refusal, missing credential preflight, `-e NAME` without values, secret redaction, official invocation flags, official report copy and the ProgramBench documentation boundary.
- [x] 4.3 Run the new test from `scripts/verify.sh` in both the docs and full profiles.

## 5. Documentation and close-out

- [x] 5.1 Document the adapter in `eval/README.md`: the publish command, every CLI flag, artifact paths, credential forwarding, the official evaluation handoff and the explicit ProgramBench out-of-scope reason.
- [x] 5.2 Run the focused checks: the new Python test, `python3 scripts/check-scratch-paths.py`, `openspec validate swebench-harness-adapter --strict`, `openspec validate --all --strict`, `bash scripts/check-openspec-skills.sh`, `dotnet csharpier check .` and `git status --short`.
- [x] 5.3 Run `openspec sync` and `openspec archive swebench-harness-adapter --yes` as the final commit on the branch, after review and before merge.
