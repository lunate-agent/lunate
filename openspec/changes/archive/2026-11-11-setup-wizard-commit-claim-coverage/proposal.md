## Why

PR #665 now makes the setup wizard's commit claim the settings and provider stores
before it seeds and replaces them, which closes the lost-update path in #668. That
behaviour is recorded in the `agent-config` and `provider-store` specs but not in
the `setup-wizard` capability whose commit it governs. The commit also lacks a
regression test for a refused claim, and for a provider entry written by the
ordinary store writer rather than a raw lock holder.

## What Changes

- Record the cross-process commit guarantee in the `setup-wizard` capability: the
  commit claims `settings.lock` and `providers.lock` before it reads and replaces
  the targets, merges an entry another process saved, and refuses without writing
  when a claim cannot be taken.
- Give the commit a bounded claim wait that tests can shorten, so the refusal path
  is covered without waiting out the store writers' ten-second default.
- Add real-child-process tests for a normal `ProviderStore.Save` landing before the
  commit and for an unclaimable settings target refusing with every file unchanged.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `setup-wizard`: the confirmed commit's cross-process claim, merge and bounded
  refusal are now part of the wizard capability, not only the store capabilities.

## Impact

- `src/Lunate.Coding/SetupWizardCommit.cs` and
  `src/Lunate.Coding/SetupWizardTransaction.cs`: an optional claim-timeout parameter
  forwarded to the existing store claim helpers; no default behaviour change.
- `tests/Lunate.Coding.Tests/SetupWizardCommitClaimTests.cs`: two child-process
  regression tests.
- The implementation itself is PR #665 (`wt/issue-584`); this branch is stacked on
  it so the wizard commit claims the stores the ordinary writers also claim.
- No public API change, no new package, no file-format change.
