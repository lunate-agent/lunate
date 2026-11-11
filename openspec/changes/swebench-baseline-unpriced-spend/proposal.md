## Why

The mini-swe-agent baseline converts a missing or nonpositive `instance_cost` to `0.0`, so the
orchestrator treats an unpriced baseline instance as priced and can keep starting instances while
certifying a cost cap it cannot prove. The committed first run happened to have positive baseline
costs, so its numbers stand, but the next run with an unpriced baseline row would under-report
spend and break the rule the Lunate arm already follows.

## What Changes

- Baseline spend extraction returns unknown (`None`) when the trajectory carries no positive
  `instance_cost`, instead of substituting zero.
- The baseline arm stops before the next instance when a row is unpriced and reports the run total
  as unknown, matching the Lunate arm.
- The published-run spec names the baseline arm in the unknown-spend rule.
- Tests cover zero, missing and positive baseline cost and the baseline-arm stop.
- `eval/README.md` states that both arms follow the unknown-spend rule.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `benchmark-harness`: the one-cost-cap requirement now states explicitly that a baseline instance
  without a positive cost is unpriced, stops the arm and leaves the run total unknown.

## Impact

- `scripts/eval/swebench_baseline.py` and `tests/scripts/test_swebench_run.py`.
- `eval/README.md` documents the rule for both arms.
- No product code, no public API, no new dependency, no rerun of the committed evidence.
