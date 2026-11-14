## 1. One command list for the tree checks

- [ ] 1.1 Add `scripts/tree-checks.sh`: run ADR numbering, the spec-archive rule, the generated docs
  regions, the docs link/navigation check and the docs lint in order, name each check before it runs,
  stop at the first failure, and take `--base <rev>` (default `origin/main`) for the spec-archive rule.
- [ ] 1.2 Point `.github/workflows/tree-checks.yml`'s push-to-main job at the script, keeping the
  `github.event.before` range, the full-history/tags checkout, the concurrency and the read-only
  permissions.
- [ ] 1.3 Extend `tests/scripts/test_tree_checks.py`: pin the trigger, the checkout and the
  concurrency as before, and pin that the script is the one command list both workflows call, that the
  script runs every check, and that it cannot swallow a failure.

## 2. The merge queue runs them and `ci-ok` requires them

- [ ] 2.1 Add the `tree-checks` job to `.github/workflows/ci.yml`: `merge_group` only, full history and
  release tags, and `scripts/tree-checks.sh --base "${github.event.merge_group.base_sha}"`.
- [ ] 2.2 Add the job to `ci-ok`'s `needs` and environment, and extend `scripts/ci-ok.sh` with the
  per-event rule: success in a merge group, skipped everywhere else, with a named failure for each.
- [ ] 2.3 Teach `scripts/ci-ok-state` the new single job, defaulting to `skipped`, so a pull request's
  live verdict does not read it from the trigger payload.
- [ ] 2.4 Extend `scripts/ci-ok.sh --self-test`: the merge group requires the tree checks, a failed or
  skipped tree-check job reddens it, the other events require the skip, and the workflow invariants pin
  the job's trigger, its base, its script call and the aggregate's `needs`.

## 3. Docs

- [ ] 3.1 Update `CONTRIBUTING.md`: the merge queue paragraph names the tree checks and why the batch
  is the tree that needs them, and the push-to-main paragraph says both triggers call one script.
- [ ] 3.2 Update `docs/dev/README.md` so the generated-region bullet names the merge queue beside the
  push to `main`.
- [ ] 3.3 Write `changes/<pull-request-number>.md` with the queue's tree checks.

## 4. Verify and record

- [ ] 4.1 Run the fast gate on the touched checks: `bash scripts/ci-ok.sh --self-test`,
  `python3 tests/scripts/test_tree_checks.py`, `python3 tests/scripts/test_adr_numbers.py`,
  `python3 tests/scripts/test_spec_archive.py`, `dotnet csharpier check .`, and
  `openspec validate merge-queue-tree-checks --strict`.
- [ ] 4.2 Sync the delta into `openspec/specs/ci-pipeline/spec.md` and archive the change as the final
  commit.
