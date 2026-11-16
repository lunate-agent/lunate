## 1. The pure decision (`scripts/ci-issue-triage`)

- [x] 1.1 Add `--capacity-json FILE`: print `capacity` when the run has at least one red job and every red
  job is *never acquired* (`conclusion == cancelled`, `runner_id == 0`, `steps` empty), print nothing
  otherwise, in any of the three jobs-endpoint shapes.
- [x] 1.2 Test-first: extend `scripts/nightly-issue-test.sh` with the decision cases and watch them fail
  before the change lands: two never-acquired legs beside a red `ci-ok`, an open card to comment on, the
  exhausted attempt, a red leg that ran, and the green run (72 checks; 53 failed before the change).

## 2. The reporter (`scripts/nightly-issue.sh`, `scripts/nightly-issue-test.sh`)

- [x] 2.1 Take `--attempt N` (default `GITHUB_RUN_ATTEMPT`, else 1) and read the capacity decision after the
  red-job decision.
- [x] 2.2 Deferral path: when the run is a capacity red and the attempt is below the budget, write the
  abandoned legs and what was seen into `GITHUB_STEP_SUMMARY`, comment on an already-open card for each
  such leg when one exists, open no card, and exit non-zero so the job is rerun with the run's failed jobs.
- [x] 2.3 Exhausted path: file or comment as today, with a body that names the capacity cause and says the
  red is not a code failure; keep the title, the dedupe and the milestone/area rules unchanged.
- [x] 2.4 Self-test the deferral path, the exhausted path, the no-open-card case and the unchanged filing
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
