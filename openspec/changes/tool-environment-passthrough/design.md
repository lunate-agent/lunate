## Context

The tool child (`bash`) runs with the `Inherited` environment policy: the caller's whole environment
minus every name Lunate resolves as a provider credential (`CredentialVariables` — each preset's key
variable and alias, plus each configured custom provider's `env:` reference). One builder,
`EnvironmentScrubber.Build`, holds both policies; `ChildProcess.BuildEnvironment` is the one place
the tool child's policy is named, and every tool spawn reaches it through `ProcessRunner` and the
platform `ChildProcess` halves. The extension hosts' `Scrubbed` policy never inherits, so the strip
and any pass-through are irrelevant there.

Today the strip is absolute. A user who deliberately wants one named variable in a tool child — a
project token, a build-machine marker, a proxy variable whose name collides with a credential name —
has no setting for it. The prior art is Codex CLI's shell environment policy: the policy that
decides what a child inherits is separate from an allow-list of named variables the user opts back
in, with the credential deny still the default.

```
InteractiveSession / PrintMode / AcpMode
        │  settings resolve, provider store registers custom env: names
        ▼
ToolEnvironmentPassthrough.Configure(names)      ← process-wide, like CredentialVariables
        │  Names (exact)            ⚠ one startup warning for a credential-name collision
        ▼
ChildProcess.BuildEnvironment(request)           ← the one policy-build site
        │  EnvironmentScrubber.Build(Inherited, passthrough: Names, strip: CredentialVariables.All)
        ▼
EnvironmentScrubber                              ← inherit → strip → re-admit exact pass-through → set
        ▼
ChildProcess.Windows / ChildProcess.Unix → child
```

## Goals / Non-Goals

**Goals:**

- A user-scope `toolEnvironment.passthrough` list of exact variable names, applied after the
  credential strip, reaching the tool child on POSIX and Windows.
- Exactly the listed names are re-admitted: no prefixes, no patterns, nothing the parent does not
  hold, and a credential name the user did not list stays stripped.
- One startup warning when a listed name is also a Lunate credential name; never a silent allow or
  silent refuse.
- The pass-through is read where the tool policy is built, not at a spawn site.
- `Scrubbed` is untouched.

**Non-Goals:**

- No pattern matching, no glob, no prefix support — a pattern is how a credential name gets
  reintroduced by accident.
- No environment setting per tool or per project; the key is user scope only.
- No public API change: `ProcessRunRequest`, `EnvironmentScrubber.Build`'s signature and the tool
  contracts stay as they are.
- No change to the `Scrubbed` base names, the strip list, or the extension/MCP child policy.

## Decisions

- **The list is applied inside `EnvironmentScrubber.Build`, not after it.** `Inherited` now:
  inherit the source set, capture the values of the listed names from that source, strip the
  credential names, then re-add the captured values, then apply declared values. Applying it in a
  second pass outside the builder would be a second place that decides a child's environment and
  would bypass the one-builder guard. Alternative rejected: sending the list to the spawn sites as
  a new `ProcessRunRequest` member — a public API change, and the acceptance is explicit that the
  list is read where the policy is built, not at a spawn site.
- **The strip stays a deny for declared values.** The existing rule that `ExtraEnvironment` cannot
  re-introduce a stripped name is kept. The pass-through re-admits only the *inherited* value of an
  exact name; it does not turn the strip into an allow for arbitrary declared values. The bash tool's
  own `LUNATE_SESSION` marker is unaffected.
- **`ChildProcess.BuildEnvironment` reads `ToolEnvironmentPassthrough.Names`.** That method is the
  one file that names the tool child's policy; it passes the list to the shared builder next to the
  strip list. A new guard test pins that this file reads it and no spawn site does.
- **The configured list is process-wide, mirroring `CredentialVariables`.** The list is user-scope
  configuration, resolved once before the first tool runs, and the builder is static; a process-wide
  holder is the same shape as the existing strip list, with a `ResetForTests` seam. Alternative
  rejected: configuring it from `SettingsStore.Resolve` — a hidden side effect in a parser, and a
  parallel test that resolves settings would mutate another test's child environment.
- **Each frontend configures it after the provider store loads.** `InteractiveSession`, `PrintMode`
  and `AcpMode` resolve settings and load the provider store before the first tool child can run;
  configuring there means the collision check sees custom providers' registered `env:` names. The
  interactive session re-applies it when a wizard commit or a login re-resolves settings.
- **The collision warning is one line, emitted once per startup.** `Configure` returns the listed
  names that are also credential names (compared with the platform's environment-name comparer, so
  Windows case-insensitivity is honest); the frontend renders one `NoticeLevel.Warning`. The text
  names the variables and says they reach tool processes because the user asked for them by name.
- **Codex CLI's shell environment policy is the model** for separating the inherited policy from a
  named allow-list, as the issue requires; the strip stays the default deny.

## Risks / Trade-offs

- [The process-wide list leaks between tests] -> `ResetForTests`, the spawn tests and the
  configuration tests share one xUnit collection (sequential), and the spawn tests pass the parent
  set through `ProcessRunRequest.Environment` instead of mutating the test process's environment.
- [Windows names are case-insensitive, POSIX names are not] -> the builder's existing dictionary
  comparer (`OrdinalIgnoreCase` on Windows, `Ordinal` elsewhere) decides both the strip and the
  re-admit; the collision warning uses the same comparer. The settings parser keeps the list in the
  order and spelling the user wrote.
- [A user lists a credential name by accident] -> one startup warning names it; the variable is
  still allowed, because an explicit name is an explicit act. Silently refusing would make the
  setting lie about what it does.
- [A listed name is absent from the parent] -> it stays absent, never set to an empty value, so a
  missing variable cannot be mistaken for an empty one in the child.
- [A wildcard in the list does nothing] -> intentional: `OPENAI_*` is an exact name that matches
  nothing, pinned by a test so a future pattern implementation cannot sneak in.

## Migration Plan

Additive behaviour and an optional settings key; a missing key means an empty list, so existing
configurations behave exactly as before. No session-format change, no state migration, no public
API. Rollback is reverting the commit; a settings file carrying the key then fails resolution on the
older build, which is the normal unknown-key refusal.

## Open Questions

None. The issue fixes the shape (exact names, user scope, after the strip, warn on a credential-name
collision) and no in-force ADR contradicts it; ADR-0024's guard and the `Inherited` policy's leak
boundary are preserved.
