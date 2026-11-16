## Why

The red-run reporter files a `kind/bug` card for every job whose conclusion is red, and `cancelled`
is red. A job that GitHub never acquired — the job page says "The job was not acquired by Runner of
type hosted even after multiple attempts" — concludes `cancelled` with an empty step list, so a hosted
macOS capacity shortage files a card for a leg that ran nothing, that no change in this repository can
fix, and whose only remedy is a rerun (#922, and #923 from the same run). The reporter's own card body
says a runner flake should be rerun and commented on "instead of opening a second card", and the push
run's perf leg is advisory to `ci-ok` anyway, so the card is not even a gate failure.

## What Changes

- The red-run reporter SHALL NOT treat a job that never started as a red job: a `cancelled` job whose
  `steps` list is present and empty was never acquired by a runner, so it files no card and no comment.
- The reporter SHALL still file for every job that ran and did not pass, and a `cancelled` job that
  did start still counts as red; a missing or unreadable `steps` list stays red, so the rule fails
  closed rather than passing on a document it cannot read.
- A red run whose only red jobs never started SHALL say so in the run log, naming each such job and
  the run link, so the red run itself is still visible; the remedy there is a rerun, which the issue
  cannot provide.
- Budgets (ADR-0005), the matrix sizes (ADR-0037) and the perf leg's advisory role on a push are
  unchanged.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `ci-pipeline`: the "Main-run failure reporting" requirement gains the rule that a job which never
  started is not a red job, with the run-log line that replaces the card.

## Impact

- `scripts/ci-issue-triage` (the red-job decision, `--jobs-json`).
- `scripts/nightly-issue.sh` (the reporter's run-log lines) and its self-test
  `scripts/nightly-issue-test.sh`.
- `CONTRIBUTING.md`'s description of the red-run report.
- No product code, no public API, no NuGet dependency, no workflow trigger or permission change.

## Intent decisions

The grill-me questions and the answers chosen from the card, the specs and the code:

1. **Did the card's job fail?** No. `perf (macos-latest)` concluded `cancelled` with `steps: []`,
   started 16:12:29Z and cancelled 16:27:31Z on run 43904858046; the job page carries "The job was not
   acquired by Runner of type hosted even after multiple attempts". Not one step ran.
2. **Is the startup budget breached?** There is no measurement to breach. The pushing commit never
   built or benchmarked in that job, the previous push run (3224) and the nightly (3225) measured
   green, and a miss would have printed `startup: floor …` before failing. Budgets stay as they are:
   ADR-0005 and ADR-0041 re-calibrate them from post-merge runs, never from a guess.
3. **Is the net11 retarget (#910) to blame?** No evidence: the job published and ran nothing, and the
   retarget's own pull-request gate and its merge-group gate were green.
4. **Is the gate itself wrong?** No. A push's perf result is advisory in `scripts/ci-ok.sh`; the run
   went red because `verify (macos-latest, 1/2)` was cancelled in the same 15-minute window, and
   `ci-ok` requires that one.
5. **Raise the budget, add a job retry, or fix the reporter?** The first is forbidden by ADR-0005/0041;
   GitHub has no native job retry, and an automatic rerun would hide the flakes this repository wants
   visible. What the card *is*, is a false report: one run filed two cards for legs that never ran, and
   the card's own text says a runner flake must not become a card.
6. **How is "never started" recognised, and can it hide a real failure?** A job that never got a runner
   reports `conclusion: cancelled` with an empty `steps` list. A job cancelled mid-run, timed out or
   failed carries the steps it reached. Only a present-and-empty list counts; a missing list stays red.
7. **Does a nightly whose runner never arrived then go unreported?** No: the run log names each such
   job with the run link, and every job that ran and did not pass still files. The missing piece for
   that class is a rerun, which no issue can carry out.
8. **Does this need an ADR?** No: it refines one requirement's rule inside the reporting layer; no
   dependency, protocol, loop or tool contract changes.
9. **Does the product change?** No user-visible product behaviour; CI reporting only. It is still a
   spec-level behaviour change, so it ships as an OpenSpec change with a `ci-pipeline` delta.
10. **What happens to the red run itself and the twin card?** Neither can be touched from a worker
    branch (no credentials): rerunning run 43904858046 and commenting on/closing #922 and #923 is a
    maintainer step, recorded in the pull request.
11. **Do the matrix sizes or the macOS legs change?** No. That run asked for three macOS jobs (two
    verify shards and the perf leg) against the plan's own budget; nothing shows this repository
    over-asked, and the acquisition failure was on GitHub's side.
