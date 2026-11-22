## Context

`ci.yml`'s `red-main-issue` job runs after a red `push` to `main` or a red nightly and calls
`scripts/nightly-issue.sh`, which reads the run's jobs from the API and asks `scripts/ci-issue-triage`
which of them are red. `scripts/ci-issue-triage` defines red as a conclusion in
`{failure, timed_out, cancelled, action_required, startup_failure}` and files one `kind/bug` card per red
job, or comments on the open card for it (spec: "Main-run failure reporting").

Two of a run's jobs have no coverage of their own: `ci-ok` fails because a required leg did not succeed,
and `red-main-issue` fails because it deferred a capacity red. The triage file already names them
(`DERIVED_JOBS`) and applies that knowledge in two places — they are never an abandoned job to name, and
they never turn a capacity red into an ordinary one — but the plain red-job decision does not apply it.

Run 45013417456 (push of `8ad33bca`, docs-only `#995`) is the case that reached a card: only
`verify-powershell (windows)` failed, with the contract-feed packing error #1000 tracks and #1001 fixes
in flight. `ci-ok` failed four seconds later, because that leg is required by the changed paths.
`red-main-issue` started at 11:36:43, read the run's jobs, commented on #1000 at 11:36:51 and filed
"CI failed: ci-ok" as #1002 at 11:36:53 — because `ci-ok` had concluded at 11:36:47, before the read.
The previous run of the same red tree, 45010395784, read the jobs one second *before* its `ci-ok`
concluded (11:04:18 vs 11:04:17) and filed only the leg's card. `red-main-issue` does not list `ci-ok` in
`needs`, so the difference is scheduler timing, not a code difference.

In-force constraints: ADR-0037 (the gate is sized by event; its job set and the `ci-ok` aggregate are
unchanged) and ADR-0038's reporting layer. None of them is revisited here; this change touches no budget,
matrix, trigger or permission.

Container/component view of the reporter as it exists (lightweight C4, ASCII):

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
                     |  --abandoned-json -> never ran   |   +----------------------+
                     |  --capacity-json  -> retryable   |
                     |  --milestones-json / --log-file  |
                     +----------------------------------+
```

The only component that decides which jobs are filed is `scripts/ci-issue-triage --jobs-json`; the
reporter is a thin pass-through that already forwards stderr to the job log. The change therefore lives in
that decision.

## Goals / Non-Goals

**Goals:**

- A derived job (`ci-ok`, `red-main-issue`) never gets a card or comment beside a red job that ran: the
  leg's card is the finding, and the aggregate only restates it.
- The run's own log names the derived red that was not filed, with the run link.
- A run whose only red is the aggregate still files, so a wrong gate verdict stays reported.
- The outcome is independent of whether the aggregate's record is in the jobs document at the reporter's
  read.
- The never-started rule and the capacity-red rule stay exactly as they are.

**Non-Goals:**

- Rerunning run 45013417456, commenting on or closing #1002, or touching #1000/#1001: all need
  credentials this lane does not have.
- Changing `ci-ok`'s verdict, its `needs`, its annotation text, or `red-main-issue`'s `needs`.
- Changing what the never-started rule names in the run summary, or the capacity retry's budget.
- Any budget, matrix size, runner label or workflow permission (ADR-0005, ADR-0037, ADR-0041).

## Decisions

1. **The rule is "derived, and a job that ran is being filed beside it".** `red_jobs()` computes whether
   the document holds at least one red job that is neither derived nor never-started, and skips the derived
   jobs only in that case. Alternatives considered: (a) drop derived jobs unconditionally — rejected,
   because a run whose `verify` leg concluded `skipped` or whose plan produced no usable profile is red
   only through `ci-ok`, and that wiring failure would then file nothing; (b) make `red-main-issue` depend
   on `ci-ok` — rejected, it makes the duplicate deterministic instead of removing it and couples the
   reporter to the gate it reports on.
2. **The decision stays in `scripts/ci-issue-triage --jobs-json`.** The triage file owns every decision
   about what a red job is; the shell script owns the `gh` calls. The self-test can then assert the rule on
   a document with no network, next to the never-started and capacity cases that already use the same
   `DERIVED_JOBS` set.
3. **The skipped derived red is named in the run log, not in the job summary.** `scripts/ci-issue-triage`
   prints `ci-issue-triage: <job>: only restates the jobs that ran; no card is filed for it; <run link>` to
   stderr, which is the `red-main-issue` job log — the same channel the never-started line uses. The job
   summary is reserved for the abandoned legs, where the filed card does not speak for the leg that never
   started; here the filed card names the very failure the aggregate restates.
4. **The replacement text is honest about which record it read.** The reason names the run and the job, so
   a reviewer of a red run sees that the aggregate was considered and deliberately not filed, rather than
   wondering why an aggregate that failed produced no card.
5. **No new dependency, no workflow change.** `python3` and `gh` are already required; the reporter's
   invocation, permissions, triggers and `needs` stay byte-for-byte the same.

## Risks / Trade-offs

- **A run whose only red is `ci-ok` now still files, so the class is not silenced.** The predicate keeps
  the aggregate's card whenever nothing else of the run explains it; the self-test pins that case both as
  a create and as a comment on an open card.
- **A derived job's red could hide a bug in the aggregate itself.** It cannot hide: the aggregate's own
  `::error::` annotation still lands on the run, `ci-ok` is red in the UI, and every run where the
  aggregate is the only red still files. What stops is a second card for a failure another card names.
- **The rule depends on the record order of the jobs document.** It does not: `red_jobs()` reads the whole
  document before deciding, so the presence or absence of the aggregate's conclusion at the reporter's
  read no longer changes the card set, only whether the "not filed" line is printed.
- **A future third derived job is forgotten.** The two names live in one constant that the capacity
  predicate, the abandoned predicate and now the red-job rule share, and a self-test already asserts that
  every name in it is a real `ci.yml` job.

## Migration Plan

None: one Python decision, its shell caller unchanged, and a self-test. Rollback is `git revert` of the
change commit, which restores the previous red set.

## Open Questions

None. No ADR is warranted: this refines one predicate inside an existing requirement and changes no
boundary, dependency, protocol or gate verdict.
