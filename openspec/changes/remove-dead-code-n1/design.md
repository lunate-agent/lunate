## Context

Finding N-1 of the 0.3.0-rc.1 review (`acfb9dace`) names four leftovers. Measured on the base
of this change (`origin/main` at `0da73e51`): `CtrlCQuitWindow` is declared at
`src/Lunate.Tui/Interaction/CtrlCQuitWindow.cs:18` and referenced nowhere in `src/` or `tests/`
(two grep hits, both its own declaration); `GetService<TService>` is declared at
`src/Lunate.Ai.Decisions/DecisionClient.cs:117` and has no call site (one grep hit, the
declaration); `ChatClientFactory.CreateProviderClient` throws `NotImplementedFamily` twice for one
input — a pre-`switch` guard at line 205 and the `switch` default at line 256; the
`ProviderAdapter` comment promises per-family routing "that a later slice of the provider-families
change wires up", but that change shipped and routes by `ProviderApiFamily`, so no preset ever
declares the `OpenAiResponses` or `Google` adapter and `ProviderPresets.ParseAdapter` accepts only
`openai`, `anthropic` and `openai-compatible`.

`QuitWindow` (`src/Lunate.Tui/Interaction/QuitWindow.cs`) carries both the Ctrl+C (2 s) and
Ctrl+D (800 ms) windows the session wires in `InteractiveSession.cs:331-332`; `QuitWindowTests`
and the interactive-session key tests already pin that behaviour. `CtrlCQuitWindow` is an earlier
single-purpose copy with its own `CtrlCAction` enum and a test-less lifetime.

## Goals / Non-Goals

**Goals:**

- Remove every unreachable declaration the finding names, so a future fix cannot land in a type
  nothing calls and a reader is not told a shipped change is still coming.
- Keep the refusal message and behaviour of the family guard, which the `switch` default already
  produces, and pin it with a production-path test.
- Leave the observation window for the removal: a regression test that fails while the duplicate
  types exist and passes once they are gone.

**Non-Goals:**

- Removing the public `ProviderAdapter.OpenAiResponses` and `ProviderAdapter.Google` enum members.
  They have no producer in `providers.json` and no branch in product code, but they are public
  surface: removing them is a public API change the finding did not ask for. They are named in
  the corrected comment instead and raised as a follow-up on the issue.
- Any behaviour change beyond the spec scenario that states the existing refusal.

## Decisions

- **Delete `CtrlCQuitWindow` outright rather than fold it into `QuitWindow`.** `QuitWindow`
  already contains the Ctrl+C contract (`Press`, 2 s window, `Hint`) and is the one the session
  uses; nothing needs migrating. Alternative considered: keep the type behind an alias — rejected,
  an alias is the same trap under another name.
- **Delete the generic `GetService<T>` but keep `GetService(Type)`.** `DecisionClientResolver`
  and the System One facade call the non-generic member; only the generic wrapper is unused, and
  it is internal, so no `PublicAPI` entry moves.
- **Delete the pre-`switch` guard, keep the `switch` default.** Both arms throw the identical
  `NotImplementedFamily(family, model, model.Provider)`; the guard merely duplicates work the
  dispatch must do anyway for a future fifth family. The comment that explained the guard moves to
  the `default` arm, where it now describes the only refusal.
- **Correct the `ProviderAdapter` comment to the present, and name the two unused members as
  unused.** The comment states the three declared kinds, that `ProviderApiFamily` selects the
  client, and that no preset declares `OpenAiResponses`/`Google`. It stops promising a future
  slice without pretending the members do not exist.
- **Regression tests in two places, matching what each can observe.** Reflection inventory tests
  in `tests/Lunate.Coding.Tests/DeadCodeInventoryTests.cs` assert the TUI assembly has exactly one
  `*QuitWindow` type and the decisions assembly has no `DecisionClientServiceExtensions`; they
  fail on the base commit and pass after the deletion. The production-path test in
  `tests/Lunate.Ai.Tests/ChatClientFactoryFamilyTests.cs` builds a `systemone` model through
  `ChatClientFactory.Create` and asserts the refusal message — it pins the arm that survives the
  guard's deletion.

## Risks / Trade-offs

- [A reader needs the deleted quit window's history] → the archive of `add-prompt-footer-bindings`
  and `key-parity` records the design that `QuitWindow` superseded; git history keeps the file.
- [The inventory test is a name check, not a reachability proof] → it is deliberately narrow: it
  pins the two names the review called out, the same shape as the `InternalsVisibleTo` inventory
  test from N-2 (#632), and the production-path test covers the behaviour that mattered.
- [A future change wants `OpenAiResponses`/`Google` as preset adapters] → the comment no longer
  claims they work; wiring them is a new change with its own spec delta.

## Migration Plan

None: no data, session, settings or wire-format change. `scripts/verify.sh` and the test matrix are
the rollback boundary — reverting the deletion commits restores the types.

## Open Questions

- None open. In lieu of an interviewer: (1) whether to delete the unused public enum members —
  answered no, it is a public API change outside this finding, raised on #581; (2) where the
  dead-code inventory test lives — answered `Lunate.Coding.Tests`, which already scans repository
  structure in `LayeringTests` and has internals access to both assemblies.
