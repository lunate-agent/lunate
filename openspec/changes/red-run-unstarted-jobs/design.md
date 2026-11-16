## Context

`ci.yml`'s `red-main-issue` job runs after a red `push` to `main` or a red nightly and calls
`scripts/nightly-issue.sh`, which reads the run's jobs from the API and asks `scripts/ci-issue-triage`
which of them are red. `scripts/ci-issue-triage` defines red as a conclusion in
`{failure, timed_out, cancelled, action_required, startup_failure}` and files one `kind/bug` card per
red job, or comments on the open card for it (spec: "Main-run failure reporting").

On run 43904858046 (push of `9dc051d7`) two macOS jobs started at 16:12:29Z and were cancelled at
16:27:31Z and 16:27:32Z with `steps: []`; the job page says "The job was not acquired by Runner of type
hosted even after multiple attempts". `ci-ok` only requires `verify`, so the run went red on
`verify (macos-latest, 1/2)`, and the reporter filed two cards — #922 for `perf (macos-latest)` and #923
for the verify shard — for legs that executed nothing. The perf leg is advisory to `ci-ok` on a push, so
#922 could not have been a gate failure at all.

In-force constraints: ADR-0005 and its addendum (startup budgets are re-calibrated from post-merge runs,
never from a guess), ADR-0037 (matrix sized by event), ADR-0041 (net11 target; budgets re-measured after
merge). None of them is revisited here; this change touches no budget and no matrix.

Container/component view of the reporter as it exists (C4, lightweight):

```
+---------------------+   jobs JSON (gh api)   +----------------------------+
| GitHub Actions API  |----------------------->| red-main-issue (ci.yml)    |
| run <id>/jobs       |                        | ubuntu-latest, actions     |
+---------------------+                        +-------------+--------------+
                                                             | bash
                                                             v
                                     +---------------------------------------+
                                     | scripts/nightly-issue.sh              |
                                     |  milestones, labels, per-job log,     |
                                     |  existing-issue lookup, gh issue ...   |
                                     +------+-------------------------+------+
                                            | python3                 | gh
                                            v                         v
                     +----------------------------------+   +----------------------+
                     | scripts/ci-issue-triage          |   | GitHub Issues API    |
                     |  --jobs-json  -> red jobs        |   | create / comment     |
                     |  --milestones-json -> version    |   +----------------------+
                     |  --log-file  -> area label       |
                     +----------------------------------+
```

The only component that decides "red" is `scripts/ci-issue-triage --jobs-json`; the reporter is a thin
pass-through. The change therefore lives in that decision, and the reporter's run log is where the
skipped job is named.

## Goals / Non-Goals

**Goals:**

- A job that no runner ever acquired files no card and posts no comment.
- Every job that ran and did not pass still files, unchanged.
- The red run stays visible in its own log when every red job was unstarted, with the run link and the
  rerun remedy named.
- Fail closed: an unreadable or absent `steps` list is red, not "probably never started".

**Non-Goals:**

- Changing any budget, matrix size, runner label or workflow permission (ADR-0005, ADR-0037).
- Retrying a job automatically, or rerunning a run from CI: the repo wants flakes visible, and an
  auto-rerun would hide a real regression behind a green second attempt.
- Touching the cards the flake already filed (#922, #923) or the red `ci-ok` on `main`: both need
  credentials this lane does not have.

## Decisions

1. **The discriminator is `conclusion: cancelled` with a present-and-empty `steps` list.** The jobs
   endpoint reports the steps a job reached; a job that never got a runner has an empty array, while a
   job cancelled mid-run, timed out or failed carries the steps it entered. A missing `steps` key (an
   older document, a hand-written fixture) is treated as red. Alternative considered: parse the job log
   for "not acquired by Runner" — rejected, because the log is best-effort (`gh api .../logs` may 404)
   and the reporter already tolerates its absence.
2. **The rule lives in `scripts/ci-issue-triage --jobs-json`, not in `nightly-issue.sh`.** The triage
   file owns every decision about what a red job is; the shell script owns the `gh` calls. Keeping it
   there means the self-test can assert the decision on a document with no network.
3. **The skipped job is named in the run log, not on a card.** `nightly-issue.sh` prints
   `nightly-issue: <job>: never started (no runner acquired); no card` to stderr, which is the job log
   of `red-main-issue`, and when nothing is left to file it prints a line naming the run and the rerun
   remedy. The run itself is already red in the Actions UI, and `ci-ok` reports the leg that matters.
4. **No new dependency, no workflow change.** `python3` and `gh` are already required; the reporter's
   invocation, permissions and triggers stay byte-for-byte the same.

## Risks / Trade-offs

- **A real failure hides as "cancelled with no steps"** -> The only way a job reaches that state is a
  cancellation before its first step (queue cancellation, never-acquired runner, a cancelled run). A
  regression in the tree cannot produce it, and a job cancelled after starting keeps its steps, so the
  case still files.
- **A genuine capacity outage now files nothing and someone must notice the red run** -> The run log
  names every unstarted job and the remedy ("rerun the run"), and `ci-ok` stays red on `main`, which the
  lane's digest and the maintainer's own review see. The trade is deliberate: a card for an unstarted
  leg is unactionable by any change in this repository.
- **The jobs document shape changes** -> `steps` starts carrying entries on a job that ran, so the
  fail-closed default (missing list = red) keeps the false-negative window to documents GitHub itself
  no longer produces.

## Migration Plan

None: a script and a self-test. Rollback is `git revert` of the change commit, which restores the
previous red set.

## Open Questions

None. ADR-0005, ADR-0037 and ADR-0041 stay in force unchanged; no new ADR is warranted for a reporting
predicate inside an existing requirement.
