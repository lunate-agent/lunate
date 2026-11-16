## Context

Run 43904858046 (push to `main`, commit `9dc051d7`) is red for one reason, and it is not the code. Two of
the run's three macOS jobs — `verify (macos-latest, 1/2)` and `perf (macos-latest)` — were cancelled after
fifteen minutes with `runner_id: 0`, `steps: []` and GitHub's own annotation on their check runs:

```text
The job was not acquired by Runner of type hosted even after multiple attempts
Due to capacity constraints, jobs targeting macOS arm64 runners may experience longer queue times.
```

Every job that held a runner succeeded: the four Ubuntu shards, the four Windows shards, the PowerShell
twin, the platform job and `verify (macos-latest, 2/2)`. `ci-ok` then did what the spec asks — a cancelled
leg is not a pass — so it reported `verify=abandoned` and the `red-main-issue` reporter filed a `kind/bug`
card, this one.

The two facts that shape the change:

- **A run cannot rerun itself.** GitHub accepts a rerun only for a completed run, and the reporter runs
  inside the run it would have to rerun. Whatever retries the run has to be a workflow the completion of
  CI triggers.
- **`rerun-failed-jobs` only reruns failed jobs.** That keeps the green Ubuntu and Windows work from being
  paid for twice, but it also means a job that succeeded on attempt one is not present on attempt two —
  the reporter has to be a failed job of attempt one to observe the retry.

## Goals / Non-Goals

**Goals:**

- A leg GitHub never gave a runner is retried once, automatically, and is never turned into a card: the run
  names it, and the remedy is a rerun.
- The retry can never hide a failure that executed: any red job with a step disqualifies the run.
- A runner shortage that survives the retry still leaves a `main` run that says what happened and what to
  do, without a card that asks a session to triage a step that never ran.
- The decision is pure, lives beside the red-job and area decisions, and fails before the change.

**Non-Goals:**

- Retrying a test that failed, a build that broke or a perf budget that was breached: those ran, and the
  existing flake budget (`scripts/verify.sh`'s single retry with its ledger) already owns in-job flakes.
- Retrying a pull request or merge-group run: neither runs macOS, both are re-triggered by a push, and the
  reporter does not report them.
- Raising or hard-coding a macOS budget or shard count: the capacity constraint is GitHub's, not the
  gate's, and `#831` already sized the matrix.
- Turning a missing leg green: `ci-ok` still fails closed on an abandoned leg in every attempt.

## The flow

The dynamic view of one capacity red, as Mermaid `sequenceDiagram` (plain syntax, no C4 specifics):

```mermaid
sequenceDiagram
    participant GH as GitHub Actions
    participant Run as ci.yml (attempt 1)
    participant Rep as red-main-issue
    participant Ret as ci-capacity-retry.yml

    GH->>Run: push to main, 3 macOS jobs requested
    GH-->>Run: 2 jobs never acquire a runner (cancelled, zero steps)
    Run->>Run: ubuntu/windows/powershell/platform/macOS 2-of-2 green
    Run->>Rep: needs all legs, always()
    Rep->>Rep: every red job cancelled with no steps → capacity red
    Rep-->>GH: summary names the abandoned legs (no card, no comment), exit 1
    GH->>Ret: workflow_run completed, conclusion failure
    Ret->>Rep: read the triggering run's jobs (actions: read)
    Ret->>Ret: capacity red and run_attempt < 2 → rerun-failed-jobs
    GH->>Run: attempt 2, the two macOS jobs and ci-ok and the reporter
    alt a runner is acquired
        Run-->>GH: green, nothing filed
    else no runner again
        Rep-->>GH: summary names the legs and the rerun; no card is filed
    end
```

Boundaries and responsibilities, in prose:

- `scripts/ci-issue-triage` owns the decision (which jobs are red, is the red a capacity red, which
  milestone and area a card takes). It is pure Python over documents the shell hands it, so its self-test
  needs no network — the same split `scripts/ci-ok-state` and `scripts/ci-ok.sh` already use.
- `scripts/nightly-issue.sh` owns the `gh` calls and the run's own attempt number
  (`GITHUB_RUN_ATTEMPT`), so it decides "defer and fail" (the budget is unspent) versus "say the rerun is
  the remedy and exit clean" (the budget is spent). No capacity red is filed: a job that never started is
  not a red job (#922), and the summary is where the run reports it.
- `.github/workflows/ci-capacity-retry.yml` owns the one side effect a run cannot perform on itself:
  requesting the rerun, gated on the same decision. It needs `actions: write`, which no other CI job holds.
- `ci-ok` stays unchanged: it keeps requiring every leg of the plan's matrix, in whichever attempt it runs.

## The capacity predicate

A job is *never acquired* when `conclusion == cancelled`, `runner_id == 0` and `steps` is a present, empty
list. A run is a *capacity red* when it has at least one such job and every red job is either never acquired
or one of the two derived jobs (`ci-ok`, `red-main-issue`). The three conditions are checked together
because each alone is ambiguous: a job cancelled by a `cancel-in-progress` run also has no steps, and a
started-then-cancelled job has steps but may still report `runner_id: 0` in a paginated payload. All three
together are only satisfied by a job that never reached a machine. A missing `steps` list is not evidence
of anything and stays red, so the rule fails closed.

The derived jobs are counted for the run's classification and for nothing else. `ci-ok` fails because a
required leg did not succeed and the reporter fails because it deferred, so both are red in every capacity
red; naming either in the summary ("`ci-ok` never acquired a runner") or filing `CI failed: ci-ok` would be
a false statement about a job that did get a runner. `scripts/ci-issue-triage --abandoned-json` is the
summary's only source, and it lists never-acquired jobs minus the derived ones.

Red is the reporter's existing set — `failure`, `timed_out`, `cancelled`, `action_required`,
`startup_failure`. `skipped` is not red.

## Alternatives considered

- **Retry the whole run (`rerun`, not `rerun-failed-jobs`).** It would rerun the reporter unconditionally and
  remove the need for the reporter to fail on the deferral path, at the cost of repeating ~20 minutes of
  green Ubuntu and Windows work for a macOS queue problem. Rejected: `rerun-failed-jobs` keeps the retry
  proportional to what actually failed.
- **Do the retry from inside `ci-ok`.** It runs at the end of the same run, so its own run is still in
  progress and the API refuses the rerun. Rejected as impossible, not as undesirable.
- **Suppress the card without retrying.** `main` would then be red with nothing filed and nothing retried,
  which is the failure mode the "red run reports itself" rule exists to prevent. Rejected.
- **File the card when the retry is spent (the first draft of this change).** It keeps a card in the
  shortage's wake, but it contradicts #922's rule that a job which never started is not a red job, and the
  card can only send a session after a step that never executed while the summary already names the rerun.
  Rejected in the rework; the union with #922 is recorded in `proposal.md`.
- **Reduce the macOS legs on a push (fewer shards, drop the perf leg).** `#831` already cut macOS to two
  shards and the perf leg is the quiet measurement the shared runner needs; cutting again trades coverage
  for a queue that was still short with three jobs. Rejected inside this card; a genuine capacity policy
  change would be its own card.
- **Poll the run from the retry workflow until it completes.** Unnecessary: `workflow_run: completed` is the
  completion event.

## Risks

- The retry workflow only runs once the file exists on the default branch, so the change cannot be exercised
  before the merge. Its decision is unit-tested and its wiring is pinned by the workflow invariants in
  `tests/scripts/`; the first real capacity event is the only end-to-end evidence, and it is a maintainer
  observation after the merge.
- A capacity red costs one extra run attempt. The budget of one retry bounds that.
- The capacity red is reported only in the run's summary and the rerun, so a shortage that lasts needs a
  person to read the summary rather than an issue. #922 chooses the same visibility, and GitHub's own
  annotation sits on the cancelled job's check run.
- If GitHub ever reruns a run in place of creating an attempt, `run_attempt` would not advance and the
  budget would not bind. The workflow reads the attempt from the triggering run payload, which GitHub
  documents as the attempt number, and files no card of its own — so the worst case is a second rerun, not
  a loop that reruns before `red-main-issue` can report.
