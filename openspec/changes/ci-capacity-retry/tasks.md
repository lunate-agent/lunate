## 1. The pure decision (`scripts/ci-issue-triage`)

- [x] 1.1 Add `--capacity-json FILE`: print `capacity` when the run has at least one red job and every red
  job is either *never acquired* (`conclusion == cancelled`, `runner_id == 0`, `steps` a present, empty
  list) or one of the two derived jobs (`ci-ok`, `red-main-issue`), print nothing otherwise, in any of the
  three jobs-endpoint shapes.
- [x] 1.2 Test-first: extend `scripts/nightly-issue-test.sh` with the decision cases and watch them fail
  before the change lands: two never-acquired legs beside a red `ci-ok`, an open card to comment on, the
  exhausted attempt, a red leg that ran, and the green run (72 checks; 53 failed before the change).
- [x] 1.3 Add `--abandoned-json FILE`: the never-acquired red jobs minus the two derived ones, which is the
  run summary's only source. `--jobs-json` prints no card-bound line for a never-acquired job and names it
  on stderr, so a leg that never started is never filed, alone or beside a red that ran.

## 2. The reporter (`scripts/nightly-issue.sh`, `scripts/nightly-issue-test.sh`)

- [x] 2.1 Take `--attempt N` (default `GITHUB_RUN_ATTEMPT`, else 1) and read the capacity decision before the
  red-job decision, because a capacity red is reported by the summary and never by a card.
- [x] 2.2 Deferral path: when the run is a capacity red and the attempt is below the budget, write the
  abandoned legs into `GITHUB_STEP_SUMMARY`, call no `gh` command at all, and exit non-zero so the job is
  rerun with the run's failed jobs.
- [x] 2.3 Budget path: at the retry budget, write the same summary with the rerun as the remedy and exit
  clean. No capacity red is ever filed or commented on, and neither derived job gets a card.
- [x] 2.4 Self-test the deferral path, the budget path, the no-open-card case and the unchanged filing
  path against the stubbed `gh`.

## 3. The retry workflow (`.github/workflows/ci-capacity-retry.yml`)

- [x] 3.1 New workflow on `workflow_run: {workflows: [CI], types: [completed]}` with `contents: read` and
  `actions: write`; act only on `conclusion == failure`, `event` push or schedule, `head_branch == main`
  and `run_attempt < 2`.
- [x] 3.2 Read the triggering run's jobs through the API, run the capacity decision, and issue
  `rerun-failed-jobs` for that run only when the decision says capacity; say what it decided either way.
- [x] 3.3 Pin the wiring in `tests/scripts/` (trigger, permissions, the attempt budget and the
  restricted events), the way the release workflow's invariants are pinned.

## 4. Docs, changelog and record

- [x] 4.1 Update `CONTRIBUTING.md`'s reporter paragraph with the capacity-retry rule.
- [x] 4.2 Write `changes/924.md` under the release's headings.
- [x] 4.3 Run `bash scripts/nightly-issue.sh --self-test`, `bash scripts/ci-ok.sh --self-test`,
  `python3 tests/scripts/test_ci_capacity_retry.py`, `dotnet csharpier check .` and
  `openspec validate ci-capacity-retry --strict`.
- [x] 4.4 Run `openspec validate --all --strict`, sync the delta and `openspec archive ci-capacity-retry
  --yes` as the final commit.

## 5. Rework: the union with #922 (#925) and the derived jobs

- [x] 5.1 Reconcile the delta with #922's open delta on the same requirement: a job that never started files
  no card and gets no comment, and the delta carries #922's scenarios, so the outcome is the union rather
  than two incompatible specs. Record the reconciliation in `proposal.md` under *Relationship to the
  unstarted-job rule*.
- [x] 5.2 Drop the filable capacity card: the budget's last attempt names the rerun in the summary and exits
  clean, and `capacity_note`/`capacity_comment` go with the card they served.
- [x] 5.3 Test-first: the abandoned-leg summary and comments name no derived job, the open card gets no
  capacity comment, the budget attempt files nothing and exits clean, and a leg that never started is not
  filed beside a red that ran (81 checks; 16 failed before the rework).
- [x] 5.4 `CONTRIBUTING.md`, `ci.yml`'s reporter comment and `ci-capacity-retry.yml`'s header describe the
  union, and `tests/scripts/test_ci_capacity_retry.py` pins `--abandoned-json` and the derived-job
  exclusion.
- [x] 5.5 Update `changes/924.md`, run `openspec validate --all --strict`, clear the archive and archive
  again as the last commit.

## 6. Rework: the abandoned leg in an ordinary red's summary

- [x] 6.1 Write the abandoned legs into `GITHUB_STEP_SUMMARY` with the run link on the ordinary path too,
  so a run that files a card for a job that ran still names the leg it did not file, as the requirement
  already said; `report` reads `--abandoned-json` before the red-job decision and the shared
  `abandoned_lines` renders both summaries.
- [x] 6.2 Test-first: the mixed-run case asserts the leg in the summary with the run link, the card for the
  leg that ran, and no derived job or retry line in the summary (86 checks; the two summary checks failed
  before the change).
- [x] 6.3 `CONTRIBUTING.md` and `changes/924.md` say the summary names the leg also when another job that
  ran is filed, and the delta gains the mixed-run scenario.
