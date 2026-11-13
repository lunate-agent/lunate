# Tasks — ci-ok judges the live head

## 1. Live-head decision

- [x] 1.1 `scripts/ci-ok-state`: group check runs by check suite, discard suites whose newest
      non-cancelled `plan` skipped or was cancelled, and take the newest non-cancelled attempt of
      each required job across the remaining suites.
- [x] 1.2 `scripts/ci-ok-state`: recover the existing decision inputs (profile, macOS, PowerShell
      path decision, changelog, docs/perf/platform results) and report queued or running jobs by
      name instead of inventing a verdict.
- [x] 1.3 `scripts/ci-ok.sh`: for `pull_request`, read the live pull request (draft and head SHA) and
      the live head's all-attempts check runs with `gh api`, then run the existing `decide` rules;
      keep every other event on the payload path.

## 2. Concurrency and job wiring

- [x] 2.1 `.github/workflows/ci.yml`: give draft and ready pull-request runs different concurrency
      groups while keeping `cancel-in-progress` for a pull request.
- [x] 2.2 `.github/workflows/ci.yml`: give the `ci-ok` job read-only `pull-requests` and `checks`
      permissions and `GH_TOKEN`; stop passing the payload `DRAFT`.
- [x] 2.3 `scripts/ci-ok.sh --self-test`: evaluate the draft and ready groups separately and require
      different groups.

## 3. Tests and fixtures

- [x] 3.1 `tests/fixtures/ci-ok/815-draft-rerun-green`: a draft-payload rerun beside a passed ready
      run, expecting green.
- [x] 3.2 `tests/fixtures/ci-ok/815-ready-failed-red`: the same shape with a failed ready job,
      expecting red.
- [x] 3.3 `tests/fixtures/ci-ok/816-cancelled-legs-green`: a cancelled ready run, a draft run and a
      later passed ready run, expecting green.
- [x] 3.4 `tests/fixtures/ci-ok/ready-still-running`: queued/in-progress jobs, expecting
      `gate still running` with their names.
- [x] 3.5 `tests/fixtures/ci-ok/live-draft-red`: a live draft on a head with an older green run,
      expecting red.
- [x] 3.6 `scripts/ci-ok.sh --self-test`: run the production live path over those fixtures and keep
      every existing env-only decision case and the `merge_group` cases.

## 4. Documentation and bookkeeping

- [x] 4.1 `openspec/changes/ci-ok-head-state/specs/ci-pipeline/spec.md`: update the aggregate merge
      gate and run-concurrency requirements with the live-head, pending, #815 and #816 scenarios.
- [x] 4.2 `changes/<pull-request-number>.md`: the release-note line.
- [x] 4.3 Run `bash scripts/ci-ok.sh --self-test`, `openspec validate ci-ok-head-state --strict`,
      `dotnet csharpier check .` (no C# change) and the docs lint.
- [x] 4.4 Sync the spec, then `openspec archive ci-ok-head-state --yes` as the final commit.
