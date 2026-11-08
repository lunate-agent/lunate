## 1. Regression tests (red)

- [ ] 1.1 Add a cross-process settings test: while this process holds the settings claim, a child
  process's `WriteProfile` does not write; after release it writes, and a change made while it
  waited survives.
- [ ] 1.2 Add a cross-process provider-store test: while this process holds the provider-store
  claim, a child process's `ProviderStore.Save` does not write; after release it does.
- [ ] 1.3 Run both tests against the unfixed store and record the failure that proves the symptom.

## 2. Implementation (green)

- [ ] 2.1 Add the settings claim helper — the `<file>.lock` path, a bounded acquire, and an
  actionable refusal — and take it in every settings read-modify-write writer.
- [ ] 2.2 Add the provider-store claim helper; take it in `Save`, and in the migration path of
  `Load` with a re-read under the claim; leave a read-only load lock-free.
- [ ] 2.3 Confirm a read-only resolve/load creates no lock file.

## 3. Verification

- [ ] 3.1 Run the focused settings, provider and race test classes; run CSharpier over the tree.
- [ ] 3.2 Run `openspec validate cross-process-store-locks --strict` and
  `bash scripts/check-openspec-skills.sh`.
