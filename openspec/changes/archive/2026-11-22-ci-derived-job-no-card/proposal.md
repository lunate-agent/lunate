## Why

The red-run reporter files one card for every job whose conclusion is red, and two of a run's jobs only
restate the others' results: the `ci-ok` aggregate fails because a required leg failed, and
`red-main-issue` fails because it deferred a capacity red. When the aggregate's record happens to be in
the jobs document at the moment the reporter reads it, the run files a second card that names no failure
of its own — run 45013417456 filed "CI failed: ci-ok" (#1002) beside the card for the leg that actually
failed (#1000, commented on in the same reporter run). The aggregate's card body is boilerplate ("This job
carries the full matrix…"), its failing step is only "Every required job succeeded", and the leg it
restates is the finding. A whole session went to a card that no change in this repository can act on.

The duplicate is a race, not a rule: the reporter (`red-main-issue`) does not need `ci-ok`, so it reads
the run's jobs while `ci-ok` is still running. Run 45010395784 read them 1 s before its `ci-ok` concluded
and filed no aggregate card; run 45013417456 read them 4 s after, and filed one. The same red tree
therefore files a card that exists only because of timing.

## What Changes

- The reporter SHALL NOT file a card or comment for a job whose result is derived from the other jobs' —
  the `ci-ok` aggregate and the reporter itself — when the run has at least one red job that ran and is
  neither of those: that job's card already names the failure the derived job restates.
- The reporter SHALL name the derived red it did not file in the run's own log, with the run link, so the
  decision is visible where the cards are filed.
- When the run's only red jobs that ran are the derived ones, the aggregate's card SHALL still be filed:
  then nothing else in the run speaks for the gate, and a wrong aggregate verdict is the whole finding.
- A job that never started keeps its existing rule (named in the log and the run summary, never filed), and
  a capacity red keeps its existing run-level rule (retried, not filed).
- No budget, matrix size, workflow trigger, permission or gate verdict changes.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `ci-pipeline`: the "Main-run failure reporting" requirement gains the rule that the two derived jobs are
  not filed beside a job that ran, with the run-log line that replaces the card, and keeps their card when
  they are the run's only red.

## Impact

- `scripts/ci-issue-triage` (the red-job decision, `--jobs-json`).
- `scripts/nightly-issue-test.sh` (the reporter's self-test: the mixed fixture gains the assertion that the
  aggregate is not filed, and a new case pins the aggregate's card when it is the only red).
- `scripts/nightly-issue.sh` and `CONTRIBUTING.md`: the comments and the prose that describe what the
  reporter files.
- No product code, no public API, no NuGet dependency, no workflow change.

## Intent decisions

The grill-me questions, answered from the card, the specs and the code (nobody answers an unattended run):

1. **Did `ci-ok` fail on its own account?** No. Its annotation for run 45013417456 reads
   `ci-ok: the PowerShell leg was required by the changed paths but did not run (verify-powershell=failure)`.
   Its only failing step is `Every required job succeeded`, so the verdict restates
   `verify-powershell (windows)`, which failed with a real error (the contract-feed packing) and is carded
   as #1000 and fixed in flight by #1001.
2. **Is this a runner flake that a rerun would clear?** No. Every leg that ran on that run either succeeded
   or failed on its own content; nothing was cancelled before its first step, so the capacity retry in
   `ci-capacity-retry.yml` found nothing to rerun. Rerunning would reproduce the same PowerShell failure
   until #1001 lands.
3. **Is the fix in `verify-powershell` or in `ci-ok`?** Neither. `verify-powershell` is #1000's card and
   #1001's change; `ci-ok` is behaving exactly as specified (a required leg did not succeed). What this
   card is, is a duplicate report: the run filed two cards for one failure.
4. **Why did the run file a `ci-ok` card at all, when earlier red runs did not?** It is a race between two
   sibling jobs. `red-main-issue` does not list `ci-ok` in `needs`, so it reads the run's jobs while the
   aggregate is still running. Run 45010395784's `ci-ok` concluded at 11:04:18, one second after the
   reporter's read, and filed no aggregate card; run 45013417456's `ci-ok` concluded at 11:36:47, four
   seconds before the reporter's comment/file at 11:36:51/11:36:53, and filed one. The outcome must not
   depend on that ordering.
5. **Drop the aggregate's card always, or only beside a job that ran?** Only beside a job that ran. If a
   required leg were `skipped` or the plan's profile were unreadable, `ci-ok` is the only red the run has,
   and then its card is the whole finding; dropping it unconditionally would make that wiring failure
   silent — the opposite of "a failure cannot sit unnoticed".
6. **What about `red-main-issue` itself?** It is the other derived job and gets the same rule, for the same
   reason. Its own cancellation-with-steps case (the existing "a cancelled job is red" self-test) has no
   other red, so it still files, unchanged.
7. **Should the reporter instead wait for `ci-ok` (`needs: ci-ok`)?** No: that makes the duplicate
   deterministic instead of removing it, and it couples the reporter to the gate it reports on. The
   decision belongs in the reporter's own red-job rule, which already knows about the derived jobs for the
   capacity path (`DERIVED_JOBS`).
8. **Is the run log enough visibility, or does the skipped derived job need the job summary?** The log is
   enough here, unlike a job that never started: the filed leg's card and `ci-ok`'s own `::error::`
   annotation already name the same failure, so nothing is lost. The never-started rule keeps its summary
   line, because there the filed card does not speak for the abandoned leg.
9. **Does this need an ADR?** No. It refines one predicate inside the existing "Main-run failure
   reporting" requirement; it changes no dependency, protocol, loop, tool contract or gate verdict.
10. **Does the product change?** No user-visible product behaviour; CI reporting only. It is still a
    spec-level behaviour change, so it ships as an OpenSpec change with a `ci-pipeline` delta.
11. **What happens to card #1002 itself and to the red run?** A worker branch cannot close an issue or
    rerun a run (no credentials). The card is a duplicate whose real failure is #1000/#1001, so closing it
    and watching run 45016780346 (the next push) green are maintainer steps, recorded in the pull request.
