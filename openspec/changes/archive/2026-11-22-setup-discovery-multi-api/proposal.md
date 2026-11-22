## Why

The OpenCode Go listing now returns `gemini-4-argon` with two API types (`generate-content` and
`interactions`, recommending `interactions`). Discovery's reader requires exactly one API family per
entry, so that one model fails the whole listing, and the wizard then reports the parse failure as a
network failure without the parser's message. A valid key and network therefore read as a broken
connection, and the model step gets no listing at all.

## What Changes

- A listing entry that declares several API types is read through the entry's `recommended_api` when
  that names a supported API family present in the entry; otherwise the entry is skipped. It never
  fails the whole listing.
- Discovery failures caused by the listing body (invalid JSON, a missing `data` array or id, or
  unreadable per-model metadata) are classified by the wizard as an undecipherable response, not as
  network. Only a transport failure stays network.
- The connection-test failure notice carries the underlying discovery message after the failure
  shape, with the resolved credential redacted.
- Tests and fixtures cover all three offline; the live OpenCode Go fixture records the multi-API
  entry that triggered the failure.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `agent-config`: model discovery treats a multi-API listing entry through its recommendation or
  skips it, instead of failing the listing.
- `setup-wizard`: the connection test distinguishes an undecipherable listing from a network failure
  and shows the underlying message, credential redacted.

## Impact

`src/Lunate.Coding/Models/DiscoveryModelMetadata.cs`, `src/Lunate.Coding/Commands/DiscoverCommand.cs`,
`src/Lunate.Coding/Setup/WizardDiscovery.cs`,
`src/Lunate.Coding/Sessions/InteractiveSession.SetupWizard.cs`, the discovery tests and fixtures under
`tests/Lunate.Coding.Tests/Commands`, `tests/Lunate.Coding.Tests/Setup` and `tests/fixtures/discovery`.
No public API change, no new dependency, no session-format change.
