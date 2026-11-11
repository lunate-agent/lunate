## 1. Baseline spend is unknown, not zero

- [x] 1.1 Add failing tests that a zero and a missing mini-swe-agent `instance_cost` are recorded as unpriced, a positive cost is still reported, and an unpriced baseline row stops the baseline arm.
- [x] 1.2 Preserve unknown spend in `scripts/eval/swebench_baseline.py` instead of substituting `0.0`.
- [x] 1.3 Run the focused baseline and cap tests until green.

## 2. Documentation

- [x] 2.1 State in `eval/README.md` that the unknown-spend rule applies to both arms and that a non-positive baseline cost is unpriced.

## 3. Close-out

- [x] 3.1 Run the three Python test files, `openspec validate swebench-baseline-unpriced-spend --strict`, `openspec validate --all --strict` and `bash scripts/check-openspec-skills.sh`.
- [x] 3.2 Sync the `benchmark-harness` spec and archive `swebench-baseline-unpriced-spend` as the final commit on the branch.
