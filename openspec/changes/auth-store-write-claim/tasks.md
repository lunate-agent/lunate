## 1. Regression tests (red)

- [ ] 1.1 Add a cross-process auth test: while this process holds `auth.lock`, a child process's
  `AuthStore.Save` writes nothing; after release it saves, and a key this process wrote while the
  child waited survives the child's save.
- [ ] 1.2 Add a cross-process test that a key the saving store removed stays removed after a merge.
- [ ] 1.3 Add a test that a save which cannot take the claim refuses, naming the auth file and
  `auth.lock`, and leaves the file unchanged.
- [ ] 1.4 Add a test that loading an auth file creates no `auth.lock`, and that a save with nothing
  changed writes nothing and creates no lock file.
- [ ] 1.5 Add a cross-process wizard-commit test: while this process holds `auth.lock`, the commit
  writes nothing; after release it commits, and an auth key another process saved while it waited
  survives the commit.
- [ ] 1.6 Run these against the unfixed store and record the failure that proves the lost update.

## 2. Implementation (green)

- [ ] 2.1 Add the auth store's claim helper — the `auth.lock` path beside the auth file, a bounded
  acquire, and an actionable refusal naming both files.
- [ ] 2.2 Track the keys `Set` and `Remove` changed since the load; a save with no change takes no
  claim and writes nothing.
- [ ] 2.3 Make `Save` a read-modify-write under the claim: re-read the document, apply the tracked
  changes, write it atomically and adopt it as the store's document; keep a failed write's old file
  intact and report an unparseable document with the store's own error.
- [ ] 2.4 Serialize this process's saves with an in-process gate, and leave `Load` claim-free.

## 3. The wizard's commit (green)

- [ ] 3.1 Claim `auth.json` beside the settings file and the provider store, before any seed, and
  hold it until the last replace; keep the claim order fixed.
- [ ] 3.2 Confirm a commit with no credential still leaves `auth.json` byte-identical and a commit
  with a credential keeps a key another process saved during the walk.

## 4. Documentation

- [ ] 4.1 Document the auth store's write claim in `docs/models-and-providers.md` and the guide's
  config section, and add the change's line to `changes/<pr>.md`.

## 5. Verification

- [ ] 5.1 Run the auth, wizard-commit and cross-process store test classes plus the auth command
  and TUI auth tests; run `dotnet csharpier format .`.
- [ ] 5.2 Run `openspec validate auth-store-write-claim --type change --strict` and
  `openspec validate --all --strict`.
- [ ] 5.3 Archive this change as the final commit of the branch.
