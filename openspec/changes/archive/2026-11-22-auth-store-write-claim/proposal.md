## Why

`settings.json` and `providers.json` take a cross-process claim for their whole read-modify-write,
but `auth.json` has none: two processes that each load the store and each save can drop one side's
key, and the setup wizard's commit replaces `auth.json` without a claim of its own, so an
`auth login` in another process can still lose the key it saved (review finding N-4, follow-up to
#584 and #668, issue #791).

## What Changes

- `AuthStore.Save` becomes a real read-modify-write: it takes an owner-only `<file>.lock` claim
  beside the auth file, re-reads the document under the claim, applies the keys this store set and
  removed since its load, and replaces the file while the claim is held.
- A save with nothing changed writes nothing and takes no claim.
- A save that cannot take the claim within the store's bounded wait refuses with an error naming
  the auth file and the lock file, and leaves the file unchanged; a malformed file found under the
  claim refuses the same way `Load` does, naming the file and never a secret value.
- The setup wizard's commit takes the auth claim as well, before it seeds its staged bodies and
  until its last replace, so a wizard commit and an `auth login` (or the TUI's auth picker) in
  another process serialize instead of one replacing the other's document.
- A read-only `AuthStore.Load` stays claim-free and creates no lock file.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `agent-config`: the auth store gains a cross-process write claim with a re-read under it, a
  bounded refusal, and a claim-free read.
- `setup-wizard`: the confirmed commit takes the auth store's claim beside the settings and
  provider-store claims, and an auth key another process saved during the walk survives the commit.

## Impact

- Code: `src/Lunate.Coding/Providers/AuthStore.cs`, `src/Lunate.Coding/Setup/SetupWizardCommit.cs`,
  `src/Lunate.Coding/Setup/SetupWizardTransaction.cs`.
- Tests: `tests/Lunate.Coding.Tests/Settings/StoreCrossProcessLockTests.cs` gains auth cases and
  `tests/Lunate.Coding.Tests/Setup/SetupWizardCommitClaimTests.cs` gains the auth-claim case, each
  contending with a real child process; `tests/Lunate.Coding.Tests/Providers/AuthStoreTests.cs`
  gains the in-process merge and no-op cases.
- Dependencies: none; reuses the internal `Lunate.Ai.FileLock` the settings and provider stores
  already use.
- Data: a new owner-only `auth.lock` sidecar appears beside `auth.json` after its first write; the
  auth file's shape and schema are unchanged.
