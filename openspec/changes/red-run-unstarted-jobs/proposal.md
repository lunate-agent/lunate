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
  `runner_id` is `0` and whose `steps` list is present and empty was never acquired by a runner, so it
  files no card and no comment.
- The reporter SHALL still file for every job that ran and did not pass, and a `cancelled` job that
  did start still counts as red; a missing or unreadable `steps` list or `runner_id` stays red, so the
  rule fails closed rather than passing on a document it cannot read.
- Every skipped job SHALL be named in the run log together with the run link, whether or not another red
  job of the same run files a card, so the red run is still visible and the rerun remedy is named where
  the job is named.
- Budgets (ADR-0005), the matrix sizes (ADR-0037) and the perf leg's advisory role on a push are
  unchanged.

## Relationship to #923's capacity retry (open PR #924)

#923's card asks for the same run to be retried automatically instead of reported, and its branch
(`openspec/changes/ci-capacity-retry/`) carries an open delta on the same requirement. The two changes
are reconciled here rather than left to contradict each other:

- **One recognition.** This change adopts #924's predicate verbatim — `conclusion: cancelled`,
  `runner_id: 0` and an empty `steps` list, all three — because an empty step list alone also describes a
  job cancelled by a superseding run. A job is never "unstarted" in one change and red in the other.
- **Different levels.** This change decides the *job*: an abandoned job is not evidence that anything the
  repository builds or tests failed, so it is not a red job and files nothing. Whether a run whose red
  jobs all never started is retried before a card is filed, and what that card says, is the *run*-level
  capacity rule, which this change neither adds nor forbids; `openspec/specs/ci-pipeline/spec.md` says so
  in the requirement's own words so that both can hold at once.
- **Merge order.** An OpenSpec delta replaces a whole requirement, so whichever of the two lands first,
  the other re-applies its delta on the synced requirement before it archives. This change is written to
  survive that: it adds one predicate and one log line to the reporting rule, and names the capacity
  question as out of its scope instead of deciding it against #924.
- **No double retry, no double card.** With both merged, a capacity red is retried by #924's rule and the
  reporter files nothing for the abandoned legs on its own account; a mixed run files the legs that ran
  and logs the abandoned ones with the run link, which is what this change's mixed-run scenario asserts.

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

1. **Did the card's job fail?** No. `perf (macos-latest)` concluded `cancelled` with `runner_id: 0` and
   `steps: []`, started 16:12:29Z and cancelled 16:27:31Z on run 43904858046; the job page carries "The
   job was not acquired by Runner of type hosted even after multiple attempts". Not one step ran.
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
   reports `conclusion: cancelled`, `runner_id: 0` and an empty `steps` list — all three, the same triple
   #924's delta uses, because an empty step list alone also describes a job a superseding run cancelled.
   A job cancelled mid-run, timed out or failed carries the steps it reached. Only a present-and-empty
   list beside `runner_id: 0` counts; a missing list or a missing runner stays red.
7. **Does a nightly whose runner never arrived then go unreported?** No: the run log names each such
   job with the run link — in every case, including a run where another red job still files a card — and
   every job that ran and did not pass still files. The missing piece for that class is a rerun, which no
   issue can carry out; #923's card is the automatic form of that rerun and this change leaves its
   run-level rule alone.
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
12. **What about #923's retry, which the same run produced?** Its delta and this one share a requirement
    and must not disagree: this change takes #924's predicate unchanged, decides only the job (an
    abandoned job is not a red job), names the run-level capacity question as out of its scope, and says
    so in the requirement so that a retry rule can sit beside it. Whoever lands second re-applies its
    delta on the synced requirement; see "Relationship to #923's capacity retry" above.
