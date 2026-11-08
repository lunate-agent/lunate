## Why

The repository's eval suite scores Lunate on six small fixture tasks; it says nothing about the SWE-bench Verified number the positioning depends on. Issue #623 needs a reproducible harness adapter that runs Lunate's `print` mode with `--json` inside the official SWE-bench images and hands the resulting patch to the official evaluation, so #624 can publish a first run from one committed entry point instead of a one-off local script.

## What Changes

- Add `scripts/eval/swebench.py`, one documented CLI with `--instance`, `--dry-run`, `--model`, `--lunate-bin`, `--dataset-file`, `--output-dir`, `--evaluate` and `--timeout`.
- Run a self-contained `linux-x64` Lunate inside the instance's official SWE-bench image at `/testbed` as `lunate -p --json --yolo`, capturing the event stream as the trajectory and `git diff` as the patch.
- Write the run under a known directory (`<output-dir>/<run-id>/<instance-id>/{trajectory.jsonl,patch.diff,run.json,…}`) and produce `predictions.jsonl` at the run root.
- Hand the predictions to `python -m swebench.harness.run_evaluation` and copy the official report into the run directory; the adapter contains no scoring logic.
- Forward credentials by environment-variable name only, never write the values, and scan/redact every forwarded secret before an artifact is left on disk.
- State explicitly that ProgramBench is not covered, with the reason: its submission is a whole-codebase archive from images with no git `/testbed`, so the SWE-bench patch handoff does not apply.

## Capabilities

### New Capabilities

- `benchmark-harness`: the reproducible SWE-bench adapter — its CLI contract, image/workspace execution, artifact paths, official-evaluation handoff, credential handling and the explicit ProgramBench boundary.

### Modified Capabilities

(none)

## Impact

- New: `scripts/eval/swebench.py`, `tests/scripts/test_swebench_adapter.py`, a section in `eval/README.md`.
- `scripts/verify.sh` runs the new Python test in both the docs and full profiles.
- No `src/` change, no public API change, no new package: the adapter is stdlib-only and calls a user-provided `swebench` installation for the official evaluation.
- Out of scope: `#624` publishes the first run and its numbers. ProgramBench is explicitly excluded with the reason above rather than silently dropped.
