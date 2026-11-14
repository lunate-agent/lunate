# Tasks: specs-name-current-docs-paths

## 1. Planning artifacts

- [x] 1.1 Proposal names the two stale requirements and the two capabilities (`ci-pipeline`, `tui`),
      and states that the S3/S4 paths are already repaired.
- [x] 1.2 Delta specs copy each requirement and every scenario in full; the only changes are
      `docs/guide.md` → `docs/dev/guide.md` in `ci-pipeline` and the guide's table → the
      `docs/keybindings.md` reference in `tui`. `tui` pairs a RENAMED entry with a MODIFIED block so
      the new title wins over the old one. (See 7.2: the `ci-pipeline` block is now #855's, not
      `main`'s.)
- [x] 1.3 Design records the two-requirement scope, the `tui` title rename, the records that stay
      untouched and why no test changes.
- [x] 1.4 ADR review records that the change introduces no durable architectural decision.

## 2. Verification

- [x] 2.1 `openspec validate specs-name-current-docs-paths --strict` passes.
- [x] 2.2 A scratch archive of the change leaves the acceptance grep
      `git grep -n -E 'docs/(guide|README|architecture|distribution)\.md|docs/spec/|docs/eval/' -- openspec/specs`
      empty; the real grep is rerun after the archive.
- [x] 2.3 The old `tui` requirement title appears nowhere in the scratch archive's specs.
- [x] 2.4 `openspec validate --all --strict` passes before the archive (30 items, 0 failed).
- [x] 2.5 `./scripts/docs-check --check` and the default `npx --yes markdownlint-cli2@0.23.3`
      pass on the tree (openspec/ is tool-managed and outside markdownlint's globs).

## 3. Changelog and archive

- [x] 3.1 `changes/848.md` carries the change's line under `### Fixed` and is committed before the
      archive.
- [x] 3.2 `openspec archive specs-name-current-docs-paths --yes` is the final commit of the pull
      request; the acceptance grep is rerun after it and reported in the session record.

## 4. Rework after review

- [x] 4.1 Merged `origin/main` into the branch (no `openspec/specs/**` conflict) and recorded that
      #849 replaces the proposal-on-`main` rule with one pull request per card; the branch already
      matches it.
- [x] 4.2 Re-copied the `ci-pipeline` requirement from the merged spec, so the delta carries the
      fast-gate and merge-group scenarios `main` added; the only difference from the current block
      is `docs/guide.md` → `docs/dev/guide.md`.
- [x] 4.3 Reverted the earlier archive commit, re-ran `openspec validate --all --strict`, and redid
      `openspec archive specs-name-current-docs-paths --yes` as the last commit again.

## 5. Second rework after review

- [x] 5.1 Removed the `adr/0036` → `adr/0037` CI-gate renumber, which was unrelated to this card's
      documentation-path requirements; the duplicate ADR-0036 number on `main` needs its own change.
- [x] 5.2 Confirmed the branch already contains `origin/main` (`7dd17817`) as its merge base, so no
      merge commit was needed; nothing under `openspec/specs/**` conflicts.
- [x] 5.3 Reverted the archive commit, re-ran `openspec validate --all --strict`, the acceptance
      grep, `scripts/docs-check --check`, `scripts/check-spec-archive.py` and markdownlint, then
      redid `openspec archive specs-name-current-docs-paths --yes` as the last commit.

## 6. Third rework after review

- [x] 6.1 The maintainer's 2026-11-14 decision overrides 5.1: the CI-gate renumber to `0037` stays
      in this pull request, because it fixes the duplicate ADR-0036 that `main` carries and the red
      `verify-docs` profile this branch inherits from it; the finding behind 5.1 predates #849.
      Design item 8 records the reversal and 5.1 stays as the history of that rework.
- [x] 6.2 Confirmed `origin/main` (`7dd17817`) is still the merge base, so there was nothing to
      merge; nothing under `openspec/specs/**` conflicts.
- [x] 6.3 Reverted the archive commit so the change is live again, updated the design, the ADR
      review and this task list, and re-ran `openspec validate --all --strict`,
      `scripts/check-adr-numbers.py`, the acceptance grep, `scripts/docs-check --check`,
      `scripts/check-spec-archive.py` and markdownlint.
- [x] 6.4 Redid `openspec archive specs-name-current-docs-paths --yes` as the last commit of the
      pull request.

## 7. Fourth rework after review: reconcile the sibling merge-queue delta

- [x] 7.1 Reverted the archive commit so the change is live again, and confirmed `origin/main`
      (`7dd17817`) is still its merge base, so there was nothing to merge and nothing under
      `openspec/specs/**` conflicts.
- [x] 7.2 Rebuilt `openspec/changes/specs-name-current-docs-paths/specs/ci-pipeline/spec.md` from
      the open `merge-queue-tree-checks` delta (#855), which modifies the same two requirements, so
      `openspec/specs/ci-pipeline/spec.md` keeps the merge-group tree checks whichever of the two
      cards lands second. The only differing line is the "Prose beside code still runs the full
      matrix" GIVEN path; both files were diffed to prove that.
- [x] 7.3 Recorded the reconciliation in the proposal, the design (item 9 and its trade-off) and this
      task list, and kept the `tui` delta as it was.
- [x] 7.4 Re-ran `openspec validate --all --strict`, the acceptance grep, `scripts/check-adr-numbers.py`,
      `scripts/check-spec-archive.py`, `scripts/docs-check --check` and markdownlint.
- [ ] 7.5 Redid `openspec archive specs-name-current-docs-paths --yes` as the last commit of the
      pull request.
