## Why

The first-run change archived with the published evidence incomplete: the mini-swe-agent arm
recorded one attempted instance per model, the committed baseline total was US$30.031674 against a
US$30 cap because an unpriced row was counted as the cap and then had another arm's spend added,
and the write-up and README still carried placeholders or an older claim. A published run whose
numbers cannot be reconciled is not evidence.

## What Changes

- Run the mini-swe-agent baseline on all five pinned instances with both pinned models and the
  committed run definition; keep every failed or unresolved row.
- Fix the orchestrator's unknown-spend accounting: an instance whose spend cannot be priced makes
  the run total unknown, never a fabricated number above the cap, and stops new instances.
- Add a fixture of the official Verified test split and assert the five pinned ids are exactly
  indices 0, 100, 200, 300 and 400 of its lexicographic order.
- Commit `eval/swebench/summary.json` and `summary.md` from the completed run, fill the write-up's
  cost, failure and scope sections, and make the README C# claim cite this run's measured numbers
  instead of the saturated six-task head-to-head.
- Document the run entry point, the pinned files and the baseline command in `eval/README.md`.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `benchmark-harness`: the published-run spend rule becomes explicit for an unpriced instance
  (the run total is unknown and the cap is treated as reached, never exceeded on paper), and the
  committed evidence must come from a completed baseline as well as the Lunate arm.

## Impact

- `scripts/eval/swebench_run.py`, `scripts/eval/swebench_run_support.py` and the orchestration tests;
  no adapter or product code changes.
- `eval/swebench/` gains the baseline evidence and the merged `summary.json` / `summary.md`;
  `tests/fixtures/swebench/` gains the official split ids.
- `docs/eval/swebench-first-run.md`, `eval/README.md` and `README.md`.
- No new NuGet package, no public product API change, no change to the pinned subset or models.
