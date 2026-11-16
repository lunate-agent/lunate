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
  every red job either never started or only restates the others' results: a job GitHub never gave a runner
  is `cancelled` with `runner_id: 0` and an empty step list. Such a run is retried once and is never filed,
  because no change in this repository acquires a hosted runner.
- **The retry is requested after the run completes.** A run cannot rerun itself while it is in progress,
  so a new workflow, `.github/workflows/ci-capacity-retry.yml`, is triggered by `workflow_run: [CI]` on
  `completed` and issues `rerun-failed-jobs` for the triggering run when the decision says capacity and
  the attempt budget (`run_attempt < 2`) is unspent. It is restricted to a `push` to `main` and the
  nightly, the two events that run the full matrix and the only two the reporter reports on.
- **The reporter names the abandoned legs in the run and defers.** `scripts/nightly-issue.sh` detects the
  capacity red, writes the abandoned legs into the job summary and fails, so `rerun-failed-jobs` reruns the
  run with those legs and this job is present to see the retry. It opens no card and posts no comment.
  `ci-ok` does not need the reporter, so the gate's verdict is unchanged.
- **The budget's last attempt names the rerun.** At attempt two the same summary says the retry is spent
  and that a rerun is the remedy, and the reporter exits clean: a runner shortage never becomes a red
  reporting step, and a session is never sent after a step that never executed.
- **Derived jobs are not abandoned legs.** `ci-ok` fails because a required leg did not succeed and this
  reporter fails because it deferred: both qualify a run as a capacity red but are never named in the
  summary and never get a `CI failed: ci-ok` card.
- **One decision, one place.** The capacity predicate lives in `scripts/ci-issue-triage`
  (`--capacity-json`) beside the red-job, milestone and area decisions, is pure, and is covered by the
  existing `--self-test`; `scripts/nightly-issue.sh` keeps only the `gh` calls.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `ci-pipeline`: "Main-run failure reporting" gains the capacity-retry rule — a run whose red jobs never
  acquired a runner is retried once and never filed, the run's summary names the abandoned legs, and the
  budget's last attempt says a rerun is the remedy.

## Relationship to the unstarted-job rule (#922, PR #925)

Card #922 reached the same run from the other side — the reporter should file no card for a job GitHub
never gave a runner — and its pull request (#925) has landed on `main`, syncing its delta into
`openspec/specs/ci-pipeline/spec.md`. The two cards agree on the outcome that matters, and the delta here
is the union so that neither left a spec that contradicts the other:

- A job that never started files no card and gets no comment, and its `conclusion`, `runner_id` and
  `steps` are all three required before it counts as such (#922's rule, kept verbatim). This card adds
  that the run's summary names the leg, and that a run whose red jobs are all such legs is retried once.
- A `cancelled` job that reached at least one step stays red, and a document whose `steps` list or
  `runner_id` is missing stays red (fail closed). Both cards keep that.
- The first draft of this card filed the card once the retry budget was spent. That contradicts #922's
  rule, and the trade is not worth it: such a card sends a session after a step that never executed, while
  the summary and the rerun already say what to do. The exhausted attempt exits clean instead.
- #925 landed first, so this delta is re-applied on the synced requirement: it keeps #922's wording and
  scenarios, replaces #925's "this requirement does not decide the run level" paragraph with the
  capacity-retry rule, and adds this card's scenarios beside them.

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
