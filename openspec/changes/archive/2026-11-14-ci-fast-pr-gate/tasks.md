## 1. The event-sized matrix

- [x] 1.1 Change `plan` in `.github/workflows/ci.yml` to emit Windows and macOS verify legs only for a push, the
  nightly and a manual dispatch; a pull request and a merge group get the Ubuntu shards only.
- [x] 1.2 Add the merge-group exclusion to `changes`, `platform` and `verify-docs`; make `verify-powershell` a
  push/nightly/dispatch job and `perf` a push/nightly job.
- [x] 1.3 Remove the `pull_request` trigger from `.github/workflows/codeql.yml`, keeping the push to `main` and
  the weekly schedule.

## 2. The aggregate gate follows the event

- [x] 2.1 Extend `scripts/ci-ok.sh` with the per-event job-set table (pull request, merge group, push, nightly,
  dispatch) and the `needs.changes` input; keep the release gate out of it.
- [x] 2.2 Change `scripts/ci-ok-state` to require the Ubuntu legs only for a live pull-request head.
- [x] 2.3 Extend `scripts/ci-ok.sh --self-test` to cover the pull request, merge group and push/nightly events,
  plus the workflow invariants that keep the plan and the decision wired together; run it and record the
  failing-before/passing-after count.

## 3. Nightly failure reporting

- [x] 3.1 Add `scripts/nightly-issue.sh`: read the run's jobs, pick every red job, and file or comment on one
  `kind/bug` card per job in `0.4.0` with the run link; replace the PowerShell-only reporter in `ci.yml`.
- [x] 3.2 Add `scripts/nightly-issue.sh --self-test` with a stubbed `gh`: a red job creates with the job in the
  title, an existing open card is commented on instead, and a green run does nothing.

## 4. Tests and docs

- [x] 4.1 Add a `fast-gate-green` live-head fixture whose matrix is Ubuntu-only, and keep the existing
  rerun/draft/cancel fixtures (they now pin the tolerated dispatch-on-head case).
- [x] 4.2 Update `CONTRIBUTING.md` and the CI section that describes the gate to the per-event job set.
- [x] 4.3 Run the fast gate: the four script self-tests, `scripts/docs-check --self-test` and `--check`,
  `scripts/docs-sync --check`, markdownlint, `dotnet csharpier check .` and `openspec validate
  ci-fast-pr-gate --strict`.

## 5. Record and archive

- [x] 5.1 Write the changelog fragment `changes/<pull-request-number>.md` with the per-event job set; the
  measured wall times live in the pull request report.
- [ ] 5.2 Run `openspec validate --all --strict`, sync the delta into `openspec/specs/ci-pipeline/spec.md`, and
  `openspec archive ci-fast-pr-gate --yes` as the final commit.
