## 1. The decision (test-first)

- [x] 1.1 Add the failing self-test cases to `scripts/nightly-issue-test.sh`: a run whose red jobs are
      `cancelled` with `runner_id: 0` and an empty `steps` list files nothing and says so; a `cancelled`
      job with steps still files; a `cancelled` job with an empty step list but a runner still files; a
      missing `steps` or `runner_id` field still files; a real failure beside an unstarted job files
      exactly one card naming the failure and logs the run link for the skipped job.
- [x] 1.2 Teach `scripts/ci-issue-triage --jobs-json --run-url` the rule: a job is red unless its
      conclusion is `cancelled`, its `runner_id` is `0` and its `steps` list is present and empty; print
      the skipped job with the run link on stderr.
- [x] 1.3 Have `scripts/nightly-issue.sh` pass the run link to the decision, so every skipped-job line
      carries it, and name the run and the rerun remedy when nothing is left to file.
- [x] 1.4 Run `bash scripts/nightly-issue.sh --self-test` and `bash scripts/ci-ok.sh --self-test`
      green.

## 2. Documentation

- [x] 2.1 Extend the red-run paragraph in `CONTRIBUTING.md` with the unstarted-job rule and where the
      run names the skipped leg.
- [x] 2.2 Write `changes/925.md` under `### Fixed`.
- [x] 2.3 Reconcile the `ci-pipeline` delta with #923's open capacity-retry delta in the proposal and
      design: one predicate, job level versus run level, and the merge-order rule.

## 3. Verification and archive

- [x] 3.1 `dotnet csharpier check .` and the fast gate pieces the change touches (`scripts/verify.sh`
      for the shell checks that run without a publish) stay green.
- [x] 3.2 `openspec validate red-run-unstarted-jobs --type change --strict` and
      `openspec validate --all --strict` pass.
- [x] 3.3 Sync the delta spec and archive the change as the branch's last commit.
