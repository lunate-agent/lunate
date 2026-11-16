## Why

The push run 43904858046 on `main` went red without a single step failing: `verify (macos-latest, 1/2)`
and `perf (macos-latest)` never acquired a hosted macOS arm64 runner, and GitHub cancelled both after
fifteen minutes of queueing with its own annotation — "The job was not acquired by Runner of type hosted
even after multiple attempts", beside the notice "Due to capacity constraints, jobs targeting macOS arm64
runners may experience longer queue times". Every job that did get a runner was green, including
`verify (macos-latest, 2/2)`. The red-main reporter then filed this card as if the gate had executed and
failed (#923), which costs a session and teaches it nothing.

The card's own instruction is the rule the pipeline should follow by itself: *"If it is a runner flake,
rerun the run and comment what you saw instead of opening a second card."*

## What Changes

- **A leg that never ran is not a failure to report, it is one to retry.** A run is a *capacity* red when
  every red job of the run was cancelled before any step started and never held a runner. Such a run is
  retried once, and only a red that survives the retry becomes a card.
- **The retry is requested after the run completes.** A run cannot rerun itself while it is in progress,
  so a new workflow, `.github/workflows/ci-capacity-retry.yml`, is triggered by `workflow_run: [CI]` on
  `completed` and issues `rerun-failed-jobs` for the triggering run when the decision says capacity and
  the attempt budget (`run_attempt < 2`) is unspent. It is restricted to a `push` to `main` and the
  nightly, the two events that run the full matrix and the only two the reporter reports on.
- **The reporter defers instead of filing.** `scripts/nightly-issue.sh` detects the capacity red, writes
  what it saw into the job summary, comments on an already-open card for the leg when one exists (never
  opening a new one) and fails, so `rerun-failed-jobs` reruns it with the abandoned legs and the retry's
  outcome is reported by the same job that owns reporting. `ci-ok` does not need the reporter, so the
  gate's verdict is unchanged.
- **A capacity red that survives the retry is still filed, with the cause named.** Attempt two files the
  same deduplicated `CI failed: <job>` card as today, but its body states that no hosted runner was
  acquired and that the red is not a code failure, so the next session reruns or escalates instead of
  triaging a step that never executed.
- **One decision, one place.** The capacity predicate lives in `scripts/ci-issue-triage`
  (`--capacity-json`) beside the red-job, milestone and area decisions, is pure, and is covered by the
  existing `--self-test`; `scripts/nightly-issue.sh` keeps only the `gh` calls.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `ci-pipeline`: "Main-run failure reporting" gains the capacity-retry rule — a red run whose red jobs
  never acquired a runner is retried once and reported only when the retry is spent, and its card names
  the capacity cause.

## Impact

- `.github/workflows/ci-capacity-retry.yml` (new): the `workflow_run` trigger, `actions: write` and the
  single rerun call.
- `.github/workflows/ci.yml`: the reporter job's comments and its attempt input; no change to the job
  set, the matrix or `ci-ok`.
- `scripts/ci-issue-triage`: the capacity decision; `scripts/nightly-issue.sh` and
  `scripts/nightly-issue-test.sh`: the deferral path and its self-test cases.
- `tests/scripts/`: workflow invariants for the new workflow.
- `CONTRIBUTING.md`, the CI spec and `changes/<pull-request-number>.md`.
- No product code, no public API, no package, no release-workflow and no budget change.
