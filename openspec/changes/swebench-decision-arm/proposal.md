## Why

The published SWE-bench run compares Lunate against mini-swe-agent on two single-model arms, and
each arm runs the chat model with the decision layer off. That is not the configuration Lunate is
built to run in, so the numbers say nothing about the decision layer's cost or its effect on
resolve rate. A third Lunate arm, running a main model with a decision model advising it, has to be
part of the same run and the same summary for that comparison to exist at all.

## What Changes

- `eval/swebench/run.json` gains a third Lunate arm of tier `decision`: a `main` model, a `decision`
  model, `decisionLayer: "on"` and a note, exactly the configuration the card defines. The run's
  single cost cap becomes US$2.00, so one run of the five-instance subset stops at that amount.
- The orchestrator (`scripts/eval/swebench_run.py`) learns the `decision` tier. It passes the main
  model id as `--model`, writes the arm's prepared `LUNATE_HOME` (`settings.json` binding the
  decision profile to the decision model, with the layer's consumers the arm definition turns on)
  from the definition itself, and forwards each model's credential variable by name. The arm's
  configuration therefore travels with the run: run definition, generated home and summary agree.
- The decision arm is a Lunate arm only. mini-swe-agent has no decision layer, so the baseline arm
  still runs the budget and frontier models on the same subset.
- Every instance row records wall time next to resolved, spend and steps; a decision call is
  counted as a decision step, not as an agent step. Every arm reports **cost per resolved
  instance**, so the three Lunate arms and the two baselines compare on the same four numbers.
- A multi-model arm's spend is the sum of the per-record `costUsd` the run's own catalog stamped on
  each usage event, which prices the main and decision models at their own rates; a usage record
  without a price makes the instance unpriced, never zero. Single-model arms keep the lane pricing
  the committed run used, so their numbers stay comparable with `rerun-624-bosgame-2`.
- `eval/README.md` documents the arm, the generated home, the metrics and the cap.

The card's own measurement — the live run that produces the resolved, spend, steps and wall time of
the third arm — needs Docker, a `linux-x64` single-file Lunate build and an `OPENCODE_GO_API_KEY`.
This change delivers the arm and the reporting that measurement needs; the numbers themselves come
from that run on a host that has them.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `benchmark-harness`: the published run definition names an optional third Lunate arm with a main
  and a decision model, the baseline runs only the single-model tiers, the summary records wall time
  per instance and cost per resolved instance for every arm, and a multi-model arm's per-instance
  spend comes from the priced usage records rather than one model's lane price.

## Impact

- `eval/swebench/run.json` (arm matrix, cap), `eval/swebench/summary.json` / `summary.md` (written by
  the next run).
- `scripts/eval/swebench_run.py`, `scripts/eval/swebench_run_support.py`,
  `scripts/eval/swebench_baseline.py` (per-instance wall time), `eval/README.md`.
- `tests/scripts/test_swebench_run.py`.
- No `src/` change: the decision layer, the decision usage and the prepared-home flag already
  exist.
