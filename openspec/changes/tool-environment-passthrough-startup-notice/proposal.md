## Why

`toolEnvironment.passthrough` admits named variables into every tool process, but a session speaks
only when a listed name collides with a Lunate credential name. With no collision — the normal case
— the admission is invisible: the user cannot see at startup which of their variables the model can
now read from a command it runs. The reference docs describe the admission mechanism but never state
that consequence either.

Follow-up to the sign-off check on #642 (PR #659), conditions (c) and (d).

## What Changes

- Every session resolves the pass-through list at startup and, when the resolved list is non-empty,
  emits one line naming the exact variables admitted into tool processes. The interactive session,
  print mode and ACP mode all behave the same way where they resolve settings.
- An empty list stays silent, so a default install gains no startup noise.
- The existing collision warning is unchanged and still follows the admission line when a listed
  name is also a Lunate credential name.
- `docs/configuration.md` and `docs/guide.md` say plainly that a variable listed in
  `toolEnvironment.passthrough` is reachable by the model through tool processes — the model can
  read it from a command it runs. The existing sentences about user scope, exact names and the
  collision warning stay.
- A PrintMode test drives the resolved-settings path and asserts the startup line names the
  configured variables; a focused unit test pins the notice text. The interactive and ACP call sites
  are covered where their resolved settings feed the same helper.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `agent-config`: the pass-through setting gains a stated startup consequence — an admitted variable
  is named at session start, and it is reachable by the model through tool processes.

## Impact

- `src/Lunate.Coding/ToolEnvironmentPassthrough.cs`: the admission notice beside the collision
  warning.
- `src/Lunate.Coding/InteractiveSession.ToolEnvironment.cs`, `PrintMode.cs`, `AcpMode.cs`: emit the
  notice from the same resolved settings that configure the list.
- `tests/Lunate.Coding.Tests/ToolEnvironmentPrintModeTests.cs`,
  `tests/Lunate.Coding.Tests/ToolEnvironmentPassthroughTests.cs`: the resolved-settings proof and the
  text pin.
- `docs/configuration.md`, `docs/guide.md`: the consequence sentence.
- No public API change, no new package, no change to the strip, the exact-name matching, the
  collision warning, or the user-scope rule.
