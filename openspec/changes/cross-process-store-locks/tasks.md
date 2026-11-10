## 1. Regression tests (red)

- [x] 1.1 Add a cross-process settings test: while this process holds the settings claim, a child
  process's `WriteProfile` does not write; after release it writes, and a change made while it
  waited survives.
- [x] 1.2 Add a cross-process provider-store test: while this process holds the provider-store
  claim, a child process's `ProviderStore.Save` does not write; after release it does.
- [x] 1.3 Run both tests against the unfixed store and record the failure that proves the symptom.

## 2. Implementation (green)

- [x] 2.1 Add the settings claim helper — the `<file>.lock` path, a bounded acquire, and an
  actionable refusal — and take it in every settings read-modify-write writer.
- [x] 2.2 Add the provider-store claim helper; take it in `Save`, and in the migration path of
  `Load` with a re-read under the claim; leave a read-only load lock-free.
- [x] 2.3 Confirm a read-only resolve/load creates no lock file.

## 3. Verification

- [x] 3.1 Run the focused settings, provider and race test classes; run CSharpier over the tree.
- [x] 3.2 Run `openspec validate cross-process-store-locks --strict` and
  `bash scripts/check-openspec-skills.sh`.

## 4. The wizard's commit (red)

- [x] 4.1 Add a cross-process test: while this process holds the settings claim, a child process's
  setup wizard commit writes nothing; after release it commits, and a change made while it waited
  survives.
- [x] 4.2 Add a cross-process test: while this process holds the provider-store claim, a child
  process's wizard commit writes nothing; after release it commits, and a provider entry saved
  while it waited survives.
- [x] 4.3 Add a test that the commit merges its provider entry into a store another process wrote
  before the commit ran.
- [x] 4.4 Run these against the unfixed commit and record the failure that proves the symptom.

## 5. The wizard's commit (green)

- [x] 5.1 Take the settings and provider-store claims before the seed and hold them until the last
  replace; write nothing before both are held, and refuse with the store's own error otherwise.
- [x] 5.2 Seed the staged provider store from the target under the claim and merge the walk's entry
  into it, instead of into a pre-walk reading supplied by the caller.
- [x] 5.3 Keep the transaction's all-or-nothing restore and its byte-equal skip unchanged.

## 6. Verification after the repair

- [x] 6.1 Run the wizard commit and claim classes, the store classes and the cross-process store
  class; run CSharpier over the tree.
- [ ] 6.2 Re-run `openspec validate --all --strict` and `bash scripts/check-openspec-skills.sh`.
- [ ] 6.3 Archive this change as the final commit of the branch, after the repair lands.
