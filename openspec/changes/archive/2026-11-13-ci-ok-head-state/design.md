# Design — ci-ok judges the live head

## Context

`ci-ok` is the single required context, so its verdict must be a fact about the pull request's live
head, not about the run that happened to report it. The current job receives `draft` from
`github.event.pull_request.draft` and every job result from `needs`; both are frozen at the run's
trigger. A rerun of a draft-triggered run therefore publishes a newer red check on a head whose ready
run passed (#815), and the draft run shares one concurrency group with the ready run it can cancel
(#816).

The repository already has the pieces this needs: a committed decision script with a self-test, a CI
job that always runs, the `plan` job as the gate's shape decision, and `gh api` as the established
way scripts read GitHub data (the release and process workflows use it). The aggregate job already
has a checkout of the workflow it reads.

## Goals / Non-Goals

**Goals:**

- Make a pull-request `ci-ok` a function of the live pull request and live head check runs.
- Ignore cancelled attempts and draft-payload attempts, and say "gate still running" by name instead
  of inventing a verdict.
- Keep the existing profile, changelog, PowerShell-path and macOS rules untouched.
- Keep every non-pull-request event, especially `merge_group`, exactly as it is.

**Non-Goals:**

- No change to the verification matrix, profile selection or which jobs gate a head.
- No change to branch protection, the ruleset or the required check's name.
- No new dependency, no product code, no session format.

## The live-head path

```
Developer / rerun
      |
      v
ci.yml  ci-ok job (always())
      |  GITHUB_TOKEN, PR_NUMBER, GITHUB_REPOSITORY
      v
scripts/ci-ok.sh
      |  gh api /repos/{repo}/pulls/{n}            -> live draft + head.sha
      |  gh api /repos/{repo}/commits/{sha}/check-runs?filter=all
      v
scripts/ci-ok-state  (stdlib Python, no network)
      |  group by check_suite; newest non-cancelled plan decides whether the suite gated
      |  newest non-cancelled attempt per job name across all gate suites
      v
DRAFT / PLAN / PROFILE / ... / GATE_RUNNING
      |
      v
ci-ok.sh decide()  (existing rule set, unchanged)
```

C4 level 3 view of the decision:

```
[ci-ok job] --reads--> [pull request live state] --head sha--> [commit check runs]
     |                                                                    |
     +-------------------> [ci-ok-state] <-------------------------------+
                                |
                                v
                        [existing decide rules] --> green / red / gate still running
```

## Decisions

### A check-suite whose plan skipped or was cancelled is not a gate

Every job in a suite carries the same `check_suite.id`, including reruns of that suite. A draft run
skips `plan` because the workflow's `plan` job is gated on `draft == false`, so all of its attempts
are "skipped"; a cancelled run has a cancelled plan. Neither suite ever gated the head. Checking the
suite's **newest non-cancelled plan** is the signal that distinguishes those suites from a real gate
without reading the run's event payload. This is what makes a rerun of the #815 draft payload
irrelevant rather than a newer red.

### Per-job newest attempt, not the run that woke ci-ok

Within a gating suite, a rerun of one failed job creates a newer check run with the same name. Across
suites on the same SHA, a newer gate run's jobs are the newest attempts. Selecting the newest
non-cancelled attempt for each exact job name (including each expanded matrix leg) reproduces what
branch protection sees and satisfies "newest attempt across all runs on that SHA". A cancelled
attempt is ignored when another attempt exists and survives only when it is the sole evidence, so it
can fail closed but cannot override a real verdict.

### A gate is only complete when every matrix leg is on the head

The matrix is the plan's decision, and a check run only exists for a leg that was scheduled. Reading
the selection as "whatever `verify` runs are there" would therefore let a matrix that lost a shard
report green on the shards that did run — the same fail-open shape as a skipped required check. Each
leg's own name carries the matrix's size (`verify (ubuntu-latest, 1/4)`), so the helper takes the
shard count from the legs that ran, requires every shard `1..N` for each OS the plan covers on a pull
request, and reports the absent legs by name before `decide()` runs. A leg that is queued or running
is *present* and is reported as `gate still running` instead. The OS list is a constant in the helper,
and the self-test ties it to the plan job's `leg` lines in the workflow, so adding, renaming or
removing a leg in CI fails the self-test instead of silently widening what a pass covers.

### The existing rules remain the deciding layer

`ci-ok-state` only recovers the inputs the decision already used. It infers the profile and the
PowerShell path decision from the check runs it selected: `verify-docs` live means the docs profile,
`platform`/`verify` live means the full profile, a skipped PowerShell leg after a successful
`changes` job means the paths did not require it, and a skipped leg after an unreadable changes
result is left unreadable so the existing rule fails closed. Then `decide()` runs exactly as before,
including the changelog, macOS and "a leg ran although the profile did not select it" rules. This
keeps the change surface small and makes the old env-only self-test cases remain meaningful.

### Pending is its own outcome

The selected newest attempt may be `queued` or `in_progress`; that is not "plan failed". Before
`decide()` is called, `ci-ok-state` reports the pending job names and `ci-ok.sh` fails with
`gate still running: <names>`. The draft check still wins first because a live draft is not reviewable
regardless of running jobs.

### Concurrency is split by obligation, not by run

The group suffix `-draft` is derived from the trigger's draft flag, which is the right place to use
the payload: it selects the obligation the run belongs to. Draft and ready runs then have different
groups and `cancel-in-progress` can stay true for both, preserving "a newer run of the same
obligation supersedes the older one" while making the #816 cancellation impossible. Because the
expression is evaluated by the self-test per event, a regression names the workflow and event.

### API access is read-only and job-scoped

The `ci-ok` job gains `pull-requests: read` and `checks: read` and receives `github.token` as
`GH_TOKEN`. The calls are `gh api`, already used by release and process workflows. The helper has no
network code, so the self-test reads fixtures under `tests/fixtures/ci-ok/` rather than live APIs.

## Risks and mitigations

- **API failure would otherwise pass or invent a red.** The script fails closed with a named reason
  and says it refuses to decide from the run's payload.
- **A newer gate run is still starting.** Pending check runs are named as "gate still running"; the
  job does not green a partial run.
- **A matrix leg never got a check run.** The live verdict names the missing leg and fails; the shard
  count comes from the legs that ran, so a shrunken matrix cannot pass on its survivors.
- **Historical heads lack a job added later.** The live path follows the check runs on that head; the
  self-test fixtures pin the current job shape. This change does not try to reinterpret an old head's
  workflow definition.
- **`gh` pagination shape.** The script asks for `--paginate --slurp` and the helper accepts either a
  single check-runs page or an array of pages.

## Test strategy

`scripts/ci-ok.sh --self-test` keeps every existing env-only decision case and adds live-fixture
cases for #815 green and red, #816 cancelled legs, a still-running gate, a live draft, a gate missing
one verify shard and a gate missing a whole verify OS. It also evaluates the concurrency expression
for a draft and a ready pull request, requires different groups, and compares the OS list the live
decision requires against the plan's own `leg` lines. The fixtures are committed under
`tests/fixtures/ci-ok/`; no test calls the network. Every fixture gate carries the four ubuntu and
four windows shards the workflow's plan produces, so a fixture cannot pass by being smaller than a
real matrix.
