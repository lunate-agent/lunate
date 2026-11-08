## 1. Prove the symptom and pin the rule

- [x] 1.1 Read the current `concurrency` blocks in `.github/workflows/ci.yml`, `docs-lint.yml` and
  `pack.yml` and record that push-to-main and the nightly resolve to the same group with
  cancellation on for the push.
- [x] 1.2 Extend `scripts/ci-ok.sh --self-test` with an expression evaluator for the forms in use and
  assert the effective group and cancellation decision per event for all three workflows.
- [x] 1.3 Run `bash scripts/ci-ok.sh --self-test` before the fix and capture the expected failure.

## 2. Apply the per-event concurrency rule

- [x] 2.1 Change `ci.yml` to group the nightly separately and cancel only `pull_request` runs.
- [x] 2.2 Change `docs-lint.yml` and `pack.yml` to cancel only `pull_request` runs.
- [x] 2.3 Run `bash scripts/ci-ok.sh --self-test` after the fix and confirm it passes.

## 3. Verify and record

- [ ] 3.1 Run the fast gate: `scripts/ci-ok.sh --self-test`, `openspec validate
  ci-concurrency-per-event --strict` and a Release build (no source change, so no test project run).
- [ ] 3.2 Update the pull request body with the per-event grouping table and the verification.
- [ ] 3.3 Sync the spec with `openspec sync`, then archive with `openspec archive
  ci-concurrency-per-event --yes` as the final commit.
- [ ] 3.4 Answer every acceptance check by number in a pull-request comment, with file:line or test
  evidence.
