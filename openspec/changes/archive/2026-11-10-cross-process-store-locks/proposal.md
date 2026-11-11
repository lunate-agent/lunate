## Why

Two Lunate processes that both read-modify-write the user's settings or provider store can lose
one side's change: the in-process lock serializes threads only, so each process reads the same
document and the later replace drops the earlier change (review finding N-4, issue #584).

## What Changes

- Every settings read-modify-write (favourites, the `providers` section, profiles, usages, the
  wizard's decisions keys, the `tui` section and a profile's reasoning level) takes an OS-level
  claim on a lock file beside the settings file for the whole read-modify-write.
- The provider store takes the analogous claim for every save and for its migration
  read-modify-write; a migration re-reads under the claim before writing.
- A write that cannot take the claim within the store's bounded wait refuses with an error naming
  the file and the lock instead of writing anyway.
- The setup wizard's commit takes the same claims on the settings file and the provider store
  before it seeds its staged bodies, so its read-modify-write merges with another process's change
  instead of replacing it, and merges its provider entry into the store it reads under the claim.
- Read-only settings resolution and provider loading stay claim-free and never create a lock file
  just to read; each read still sees a complete document because replacement is atomic.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `agent-config`: settings writes gain a cross-process serialization guarantee beside the existing
  read-modify-write and atomic-replace rules, and the setup wizard's multi-file commit takes it.
- `provider-store`: provider-store writes and the auth migration gain the same cross-process
  serialization guarantee; the wizard's commit merges its entry under the claim.

## Impact

- Code: `src/Lunate.Coding/SettingsStore.cs` and its partials (`Providers`, `ProfileWrite`,
  `Wizard`, `Tui`), `src/Lunate.Coding/ProviderStore.cs`,
  `src/Lunate.Coding/SetupWizardCommit.cs` and `src/Lunate.Coding/SetupWizardTransaction.cs`.
- Tests: `tests/Lunate.Coding.Tests` gains cross-process regression tests that spawn a real child
  process and contend on the lock file.
- Dependencies: none; reuses the internal `Lunate.Ai.FileLock` already used by the extension trust
  store and session writer claims.
- Data: a new owner-only `<store>.lock` sidecar (`settings.lock`, `providers.lock`) appears beside a
  store after its first write; no existing file changes shape or schema.
