## 1. Base and claim coverage

- [ ] 1.1 Confirm the branch base (#665) has the commit claim both targets before
  seeding and release them after the last replace, and record the dependency in
  the pull request body.
- [ ] 1.2 Forward an optional claim timeout from `SetupWizardCommit.Commit` to
  `SetupWizardTransaction.ClaimTargets`, keeping the stores' default when it is
  null.
- [ ] 1.3 Add a child-process test where the ordinary `ProviderStore.Save` adds
  an entry before the commit; assert the committed store keeps that entry, the
  walk's entry and the seed.
- [ ] 1.4 Add a child-process test where `settings.lock` is held beyond a short
  claim wait; assert the refusal names the file and lock and every target stays
  byte-identical.

## 2. Verify and archive

- [ ] 2.1 Run `SetupWizardCommitClaimTests`, `SetupWizardCommitTests`,
  `StoreCrossProcessLockTests`, `ProviderStore*` and every class referencing the
  changed types.
- [ ] 2.2 Run the fast gate: Release build, CSharpier,
  `openspec validate --all --strict` and `bash scripts/check-openspec-skills.sh`.
- [ ] 2.3 Sync the delta spec into `openspec/specs/setup-wizard/spec.md` and archive
  the change as the final commit on the branch.
