## 1. The decision (test-first)

- [x] 1.1 Add the failing self-test cases to `scripts/nightly-issue-test.sh`: a run with a red leg that ran
      beside a red `ci-ok` files exactly one card and names `ci-ok` in the run log with the run link; a run
      whose only red is `ci-ok` still files both when no open card exists and as a comment when one does.
- [x] 1.2 Teach `scripts/ci-issue-triage --jobs-json` the rule: skip a derived job (`DERIVED_JOBS`) when
      the document holds at least one red job that ran and is neither derived nor never started, and name
      the skipped job and the run link on stderr; keep the derived card when it is the run's only red.
- [x] 1.3 Update the comments that describe the reporter (`scripts/nightly-issue.sh`'s header, the triage
      module docstring) and the red-run paragraph in `CONTRIBUTING.md`.

## 2. Verification

- [x] 2.1 Run `bash scripts/nightly-issue.sh --self-test`, `bash scripts/ci-ok.sh --self-test` and
      `python3 -m unittest` for `tests/scripts/test_ci_capacity_retry.py` green.
- [x] 2.2 Run the fast gate pieces this change touches: `dotnet csharpier check .`, the `docs-check` /
      `docs-sync` self-tests and `openspec validate ci-derived-job-no-card --type change --strict`.
- [x] 2.3 Write `changes/<pull-request>.md` under `### Fixed`.

## 3. Archive

- [ ] 3.1 Sync the `ci-pipeline` delta and run `openspec validate --all --strict`.
- [ ] 3.2 Archive the change as the branch's last commit.
