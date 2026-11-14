# Design — pre-ready pull-request checks

## Context

The lane's readiness check is `~/.hermes/scripts/lunate-pr-ready.py`, a per-machine script. It sees
the four findings that have cost this week's reviews: an OpenSpec change that was not archived, a
missing `changes/<pr-number>.md`, a spec edited without its archive, and an acceptance check with no
answer. None of them is a build or test failure, so the existing gate does not report them, and a
reviewer spends the round-trip instead.

The repository already has the shape a fix needs: committed Python decision scripts with a stdlib
test suite under `tests/scripts/`, a `pull_request` event that can read the body and comments through
`gh api`, and a separate `Process` workflow for cheap process rules. The `ci-pipeline` spec already
defines the changelog-fragment and spec-archive rules this job re-uses, and `scripts/ci-ok.sh`
already reads check runs live. This change deliberately does not touch `ci-ok`: the card requires the
new job to stay advisory until it has run green once and a maintainer adds it to the ruleset.

Stakeholders are the lane (which sees the job on the pull request), the reviewer (who no longer
reports these four findings by hand) and the maintainer (who enables the required check later).

## Goals / Non-Goals

**Goals:**

- Make the four mechanical readiness checks visible on the pull request before a reviewer is spent.
- Name the failed check, the offending path or issue, and the fixing action in every failure.
- Keep the decision in a committed, stdlib-only script with tests for the green case and each real
  finding.
- Make the job cheap and independent of the build: no .NET, no test suite, seconds of wall clock.

**Non-Goals:**

- Not a required check yet: no ruleset entry and no `ci-ok` dependency in this change.
- Not a replacement for `ci-ok`, `changelog`, `archive-only` or `check-spec-archive.py`; it is the
  single advisory surface the lane reads before those required jobs are spent.
- No new dependency, no product code, no session format, no release change.
- No automatic fixing, labelling or commenting: the job only reports.

## The job in context

```
Developer / lane push
        |
        v
GitHub pull_request event (opened, synchronize, reopened, ready_for_review, edited)
        |
        +--> ci.yml: plan, verify, platform, changelog, ci-ok      (required today)
        |
        +--> pre-ready.yml: pre-ready job                          (advisory, this change)
                 |  checkout the pull request head, fetch-depth 0
                 |  gh api: pull request, issue comments, linked issues
                 v
            scripts/pre-ready.py  (stdlib Python, no network)
                 |  git diff base...HEAD           -> checks 1, 2, 3
                 |  git show HEAD                  -> "the archive is the last commit"
                 |  PR body + comments + issues    -> check 4
                 v
            per-check ok lines, or named failures with the action
```

Component view of the decision script:

```
[workflow step]
   |-- pr.json, comments.json, issues/<n>.json
   |-- git: base...HEAD, HEAD commit, HEAD tree, base tree
   v
[pre-ready.py]
   |-- linked issue numbers (title + body refs)
   |-- acceptance items (list items under an "Acceptance" heading)
   |-- answer blocks (PR body, then comments by created_at; last answer wins)
   v
[check 1 archive] [check 2 fragment] [check 3 spec/archive] [check 4 answers]
   |                 |                  |                     |
   +-----------------+------------------+---------------------+
                              v
              exit 0 with four ok lines / exit 1 with every finding
```

The diagrams are C4-inspired: one context view of the job beside the required CI, one component view
of the script's inputs and decisions. Assumption: the lane pushes branches in this repository (the
worktree flow), so checking out `github.event.pull_request.head.sha` with full history is enough; a
fork pull request would need the head repository named explicitly, which this change does not add.

## Decisions

### A new workflow, not a job in `ci.yml` or `Process`

A new `.github/workflows/pre-ready.yml` makes the check its own visible status context (`pre-ready`)
and keeps its advisory state explicit: it cannot inherit `ci-ok`'s requirements, and enabling it is a
single ruleset action. Adding it to `ci.yml` would tempt a `needs` edge into `ci-ok`, which the card
forbids for this change; adding it to `Process` would mix a network/API job into the workflow whose
other jobs are deliberately checkout-only and label-driven. The cost is one more workflow file and
the shared maintenance of a second `gh api` call, both small.

### The job runs on drafts and on body edits

The job has no draft gate, so a worker sees the findings while the branch is being built, and the
`edited` trigger re-runs it when the body gains answers. `ready_for_review` is in the trigger list
because the lane posts the session report around the moment the draft flag comes off; that event
gives the acceptance check a run in which the final comment exists. A run is cancelled only by a
newer run of the same pull request (`concurrency` group on the pull request number), which is the
same "newer push supersedes" rule the other workflows use.

### Check 1 proves the last commit, not just the presence of an archive

Checking only "an archive path exists" would pass a pull request that archived its change in an
earlier commit and then added a later commit — the archive is still in the diff, but it is not the
final commit the card requires. The script therefore requires three facts for every unarchived
change touched by the diff: no path under `openspec/changes/<name>/` at `HEAD`; the `HEAD` commit
deletes or renames out of that directory (the old side of a rename counts as a deletion); and the
`HEAD` commit adds or renames into `openspec/changes/archive/<YYYY-MM-DD>-<name>/`. The date shape is
the one `openspec archive` writes. A change that is only edited under
`openspec/changes/archive/` is not an unarchived change and is ignored, so a standalone archive or a
typo fix in an archived change is not forced to re-archive.

### Check 2 requires a fragment for every non-release pull request

The card says "a behaviour change", but the repository's own rule (`changes/README.md`, the
`ci-pipeline` spec and the lane instruction) is that every pull request writes a fragment; CI,
documentation and test pull requests in this repository have carried one since #813. A narrower
"behaviour" classifier would either duplicate `scripts/ci-plan.sh` or let a code pull request through
by classification accident. The script therefore requires the fragment for every pull request that
is not the release pull request, and re-uses the exact release signals (`release/<version>` branch,
`chore(release)` title) and the inert-until-`changes/README.md`-on-base rule that
`scripts/changelog-guard` already defines. The fragment must be in the diff and in the `HEAD` tree,
so a deleted or stale file cannot satisfy it.

### Check 3 keeps the existing spec-archive rule

The script applies the same decision as `scripts/check-spec-archive.py`: a changed path under
`openspec/specs/` requires a changed path under `openspec/changes/archive/` in the same diff. It does
not judge the archive's content; `openspec validate --all --strict` and the archive command own that.
The test suite runs both implementations over the same cases, so the two cannot drift silently. The
rule is not imported from the other script because `check-spec-archive.py` is a hyphenated executable,
and a five-line rule with a cross-check is clearer than an import shim.

### Check 4 reads linked issues, the body and the comments

The linked issues are the `#<number>` references in the pull request title and body. Every referenced
issue is fetched; the ones with an `Acceptance` heading are the ones checked. This is more robust than
requiring a closing keyword, because the lane's body is generated and the issue is also named in the
title. If no referenced issue has an `Acceptance` heading, the check fails and says to link the issue
with `Closes #<number>`.

An acceptance section is the list items directly under the first heading whose text starts with
`Acceptance` (case-insensitive), up to the next heading of the same or a higher level. Items are
numbered in order. An answer is a line that starts with the check's number (optionally behind a
bullet and bold markers, followed by `.`, `)`, `-`, `—` or whitespace) and continues until the next
numbered answer or heading; continuation lines are part of the same answer, so wrapped evidence is
not lost. Answers are collected from the pull request body first and then from every comment sorted
by `created_at`; the last answer for a number wins. "Body or final comment" is thus a subset of what
is read, which makes a re-run after the lane posts its report green without weakening the check.

An answer must contain `met` (not `not met`) and evidence. Evidence is a `file:line` token or a test
name: `test_something`, `SomeTests.test_something`, or a dotted name ending in `Tests.<method>`.
A `not met` answer fails with a named message: the pull request is not ready, even though it is
honest. A `met` answer without evidence fails as well, because the card requires evidence per check.
This is deliberately stricter than "answered": the job's green state means the pull request satisfies
its acceptance, not that it merely mentioned it.

### The script is testable without the network

`scripts/pre-ready.py` takes the git inputs as `--paths-file`, `--head-paths-file`,
`--head-tree-file` and `--base-tree-file`, and the pull-request inputs as `--pr-json`,
`--comments-json` and `--issues-dir`. In CI the workflow writes those from `gh api`; the tests pass
fixtures in the same shape. The script itself never calls the network, so `tests/scripts/test_pre_ready.py`
runs under `verify.sh` with no credentials, and every case is deterministic. When a git input is
omitted, the script runs the corresponding `git` command itself, so a developer can run it locally.

## Risks / Trade-offs

- **The final report is a comment posted after the last push.** The check will be red until a run
  happens with the comment present. `ready_for_review` and `edited` triggers cover the lane's usual
  flow; if a pull request is already ready when the report lands, the maintainer or reviewer re-runs
  the job once. The failure message says so. This is acceptable because the job is advisory in this
  change and the card's enabling step is a separate maintainer action.
- **A body or comment with a number that looks like an answer.** A line such as `1. met the bar`
  in prose could satisfy check 1. The evidence requirement makes an accidental pass unlikely, and
  the check is a process aid, not a security boundary.
- **Two linked issues with the same acceptance numbering.** The same answer can satisfy both. The
  lane links one issue per card; the failure message names the issue and number if one is missing.
- **A `file:line` that no longer exists.** The check verifies shape, not that the path is current;
  `ci-ok` and the test suite verify the code. A line number is evidence of where to look, not a
  second build.
- **A fork pull request.** The workflow checks out `github.event.pull_request.head.sha` from this
  repository. The lane uses in-repository branches; a fork would need the head repository added to
  the checkout. Recorded as an assumption, not a goal.
- **The date in the archive name.** The check accepts the `<YYYY-MM-DD>-<name>` shape, not a
  particular date; `openspec archive` writes the date, and a wrong date is cosmetic, not a lost
  archive.

## Migration Plan

1. Land the workflow, the script, the tests, the `verify.sh` entries and the documentation in this
   pull request. The job runs red or green on this pull request itself; it is not required, so it
   cannot block the merge.
2. After the pull request has run green once on a real pull request, a maintainer adds `pre-ready`
   to the repository ruleset's required checks. That step is outside this change.
3. The separate change that retires `~/.hermes/scripts/lunate-pr-ready.py` follows once the check is
   required. Rolling back is deleting the workflow and the script; nothing else depends on them.

## Open Questions

None. The advisory-to-required transition and the lane-script retirement are explicitly outside this
change (the card and the acceptance name them as separate steps). No in-force ADR is contradicted;
ADR-0011 (repository quality tooling) is the form this follows, and no new durable ADR is needed.
