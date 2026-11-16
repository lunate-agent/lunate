## Context

`ci.yml`'s `red-main-issue` job runs after a red `push` to `main` or a red nightly and calls
`scripts/nightly-issue.sh`, which reads the run's jobs from the API and asks `scripts/ci-issue-triage`
which of them are red. `scripts/ci-issue-triage` defines red as a conclusion in
`{failure, timed_out, cancelled, action_required, startup_failure}` and files one `kind/bug` card per
red job, or comments on the open card for it (spec: "Main-run failure reporting").

On run 43904858046 (push of `9dc051d7`) two macOS jobs started at 16:12:29Z and were cancelled at
16:27:31Z and 16:27:32Z with `runner_id: 0` and `steps: []`; the job page says "The job was not acquired
by Runner of type hosted even after multiple attempts". `ci-ok` only requires `verify`, so the run went
red on `verify (macos-latest, 1/2)`, and the reporter filed two cards — #922 for `perf (macos-latest)` and
#923 for the verify shard — for legs that executed nothing. The perf leg is advisory to `ci-ok` on a
push, so #922 could not have been a gate failure at all.

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
- The red run stays visible in its own log in every case: each unstarted job is named with the run link,
  including a run where another red job files a card, and a run red with nothing else says that a rerun is
  the remedy.
- Fail closed: an unreadable `steps` list or `runner_id` is red, not "probably never started".
- Agree with #923's open capacity-retry delta on what a never-started job is, so the two changes cannot
  recognise the same job differently.

**Non-Goals:**

- Retrying a job or a run from CI **in this change**. A run-level capacity red and its one bounded retry
  are #923's card (`ci-capacity-retry`, open PR #924); this change neither adds that rule nor argues
  against it, and says in the requirement that the run-level question is not decided here.
- Changing any budget, matrix size, runner label or workflow permission (ADR-0005, ADR-0037).
- Touching the cards the flake already filed (#922, #923) or the red `ci-ok` on `main`: both need
  credentials this lane does not have.

## Decisions

1. **The discriminator is `conclusion: cancelled`, `runner_id: 0` and a present-and-empty `steps` list —
   all three.** The jobs endpoint reports the steps a job reached and the runner it held; a job that never
   got a runner has an empty array and no runner, while a job cancelled mid-run, timed out or failed
   carries the steps it entered. The triple is deliberate and is the same one #923's open delta uses,
   because each condition alone is ambiguous: a job a superseding run cancelled also has no steps, and a
   started job can still report `runner_id: 0`. A missing `steps` key or a missing `runner_id` (an older
   document, a hand-written fixture) is treated as red. Alternative considered: parse the job log for
   "not acquired by Runner" — rejected, because the log is best-effort (`gh api .../logs` may 404) and the
   reporter already tolerates its absence.
2. **The rule lives in `scripts/ci-issue-triage --jobs-json`, not in `nightly-issue.sh`.** The triage
   file owns every decision about what a red job is; the shell script owns the `gh` calls. Keeping it
   there means the self-test can assert the decision on a document with no network.
3. **The skipped job is named in the run log, not on a card, and always with the run link.**
   `scripts/ci-issue-triage` prints `ci-issue-triage: <job>: never started (no runner was acquired); no
   card is filed for it; rerun <run link>` to stderr, which is the job log of `red-main-issue`, for every
   skipped job on every run — the line is emitted before the red set is printed, so a run that still files
   the other legs links the abandoned one too. When nothing is left to file, `nightly-issue.sh` adds a
   line naming the run and the rerun remedy. The run itself is already red in the Actions UI, and `ci-ok`
   reports the leg that matters. The run link is required with `--jobs-json` rather than optional: the
   reporter always has it, and a job named without its run is the defect this line exists to avoid.
4. **No new dependency, no workflow change.** `python3` and `gh` are already required; the reporter's
   invocation, permissions and triggers stay byte-for-byte the same.

## Risks / Trade-offs

- **A real failure hides as "cancelled with no steps"** -> The only way a job reaches `cancelled` with
  `runner_id: 0` and no steps is a cancellation before its first step with no machine attached (queue
  cancellation, never-acquired runner, a cancelled run). A regression in the tree cannot produce it, and a
  job cancelled after starting keeps its steps, so the case still files; a job that held a runner and was
  cancelled keeps its `runner_id`, so it files too.
- **A genuine capacity outage files fewer cards and someone must notice the red run** -> The run still
  files once, for the `ci-ok` leg that a cancelled required job reddens, so the red `main` push keeps its
  card; the run log additionally names every unstarted job with the run link and the rerun remedy. Only a
  run whose every red job never started files nothing, and that run is visible in the Actions UI, in the
  lane's digest, and — if #924's capacity retry lands — is retried before anyone is told. The trade is
  deliberate: a card for an unstarted leg is unactionable by any change in this repository.
- **#924 is open on the same requirement** -> This change takes its predicate unchanged, keeps the rule
  at the job level, and defers the run-level capacity question to that rule in the requirement text, so
  neither delta has to be read as contradicting the other. An OpenSpec delta replaces a whole
  requirement, so whichever lands second re-applies its delta on the synced text; the proposal states
  this so the reviewer reads it as a decision, not an oversight.
- **The jobs document shape changes** -> `steps` starts carrying entries on a job that ran, so the
  fail-closed default (missing list = red) keeps the false-negative window to documents GitHub itself
  no longer produces.

## Migration Plan

None: a script and a self-test. Rollback is `git revert` of the change commit, which restores the
previous red set.

## Open Questions

None. ADR-0005, ADR-0037 and ADR-0041 stay in force unchanged; no new ADR is warranted for a reporting
predicate inside an existing requirement.

## Relationship to #923's capacity retry

#923's card is the automatic form of the rerun this rule cannot perform, and its open delta modifies the
same requirement. The reconciliation, in one sentence per level: this change decides that an abandoned
job is not a red job and names it with the run link; #924 decides whether a run red only through such
jobs is retried once and what the surviving card says; the requirement states both, and whichever change
archives second re-applies its delta on the synced requirement. No behaviour of this change depends on
the other landing first.
