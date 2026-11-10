## Why

The 0.3.0-rc.1 review (finding N-1, issue #581) found four leftovers that no code path
reaches: a second Ctrl+C quit-window type (`CtrlCQuitWindow`) that predates the shipped
`QuitWindow`, an unused generic `GetService<T>` extension on the decision client, a family
guard whose work the factory's `switch` default already does, and a `ProviderAdapter` comment
that still promises a "later slice of the provider-families change" — a change that has since
shipped and routes families, not adapters. Dead code that duplicates live behaviour is the
kind of thing a future change reads as current: the duplicate window is a trap for a fix that
lands in the type nothing calls, and the stale comment names a plan that no longer exists.

## What Changes

- Delete `src/Lunate.Tui/Interaction/CtrlCQuitWindow.cs` (`CtrlCQuitWindow` and `CtrlCAction`);
  `QuitWindow` is the one quit window the session uses.
- Delete `DecisionClientServiceExtensions.GetService<T>` from
  `src/Lunate.Ai.Decisions/DecisionClient.cs`; the non-generic `IDecisionClient.GetService(Type)`
  stays, because production uses it.
- Delete the pre-`switch` family guard in `ChatClientFactory.CreateProviderClient`; the
  `switch`'s `default` arm throws the same `NotImplementedFamily`. No behaviour change.
- Rewrite the `ProviderAdapter` doc comment to describe what the code does today: three
  declared adapter kinds, family-based client selection, and the two unused enum members named
  as having no preset.
- The `ai-layer` spec's Provider API families requirement gains one scenario pinning that a
  decision-only (`systemone`) family asked of the chat client factory is refused naming the
  family and the wired set — the behaviour the deleted guard shared with the `switch` default.

## Capabilities

### New Capabilities

- _none_

### Modified Capabilities

- `ai-layer`: the Provider API families requirement — a scenario states that the chat client
  factory refuses a family with no client rather than building one.

## Impact

- `src/Lunate.Tui/Interaction/CtrlCQuitWindow.cs` (deleted), `src/Lunate.Ai.Decisions/DecisionClient.cs`,
  `src/Lunate.Ai/ChatClientFactory.cs`, `src/Lunate.Ai/ProviderAdapter.cs`; no public API change
  (both removed types are internal, the comment is documentation) and no new package.
- Tests: `tests/Lunate.Coding.Tests/DeadCodeInventoryTests.cs` (new: the two removed types stay
  gone) and one production-path test in `tests/Lunate.Ai.Tests/ChatClientFactoryFamilyTests.cs`
  (the `switch` default still refuses an unwired family with the actionable message).
- `openspec/specs/ai-layer/spec.md` gains the scenario; `docs/` needs no change (nothing there
  names the removed types or the promise in the comment).
