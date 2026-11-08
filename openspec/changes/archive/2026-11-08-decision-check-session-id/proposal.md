## Why

Interactive setup with the `opencode-go` preset fails its decision check before the first
byte: the check builds its decision client without a request identity, so the preset's
`x-opencode-session: {session.id}` header cannot render. The model check already carries the
session's id (or a fresh ephemeral probe id) and passes; the decision check must use the same
identity mechanism. This blocks the v0.3.0 release.

## What Changes

- The decision-profile check takes the same request identity the model check takes: the
  current session's id when the wizard runs inside a session (including `/setup`), and a fresh
  probe id minted for the check when it runs standalone through `lunate setup`.
- The identity travels through the production path (staged profile → provider entry → API
  family → client), not through a header patched at the call site.
- A provider whose profile declares no session header keeps sending exactly the request it
  sends today.
- The setup-wizard spec gains the decision-check identity scenarios; the release-blocking
  `opencode-go` combination is covered at the wire.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `setup-wizard`: the decision-profile model check carries a request identity — the session's
  id inside a session, a fresh probe id for standalone `lunate setup` — so a preset whose
  headers template `{session.id}` can complete the check.

## Impact

- `src/Lunate.Coding/WizardModelCheck.cs`: `RunDecisionAsync` accepts a `RequestContext?` and
  resolves `RequestContext.Ephemeral()` when none is supplied, mirroring `RunAsync`.
- `src/Lunate.Coding/InteractiveSession.SetupWizard.cs`: the in-session decision check passes
  `SessionRequestContext()`.
- `src/Lunate.Coding/SetupCommand.Walk.cs`: the flag path passes `context: null`.
- Tests in `tests/Lunate.Coding.Tests/SetupWizardDecisionCheckTests.cs`,
  `WizardModelCheckWireTests.cs` and the setup-wizard driver tests.
- No public API change, no new dependency, no session-format change.
