## Why

With `usages.chat` bound to a decision-only `systemone` profile, startup warns once and keeps the session alive, but the first message refuses with `SetupRefusal.Text` — "no chat model is configured" — although a chat model *is* configured; it is a decision-only profile. The first message therefore explains the wrong problem and never names `/setup`, the repair command reachable inside the session (issue #716).

## What Changes

- The session's placeholder client, which defers client creation to the first request, now throws a refusal carrying the refused binding's own message when the chat binding was refused. The binding message already names the profile, the model, the family, both repair commands (`/setup` in a session, `lunate setup` outside) and the `decisions.profile` alternative.
- A machine with no chat answer at all keeps the existing configure-it refusal text; there the "no chat model is configured" sentence is true.
- The no-request invariant is unchanged: the deferred client is still never created, so nothing is sent in either case.
- `SetupRequiredException` gains a text-carrying shape with the current text as its default, and a single `SetupRefusal` helper chooses the refusal for a placeholder, so each message still has one home.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `setup-wizard`: the wizard-trigger requirement now also covers a chat usage bound to a decision-only profile: the session warns once, stays alive, and the first message refuses with the binding's message naming `/setup` and `lunate setup`, while the genuine no-chat-answer case keeps the configure-it text.

## Impact

- `src/Lunate.Coding/InteractiveSession.cs` (placeholder client refusal)
- `src/Lunate.Coding/SetupRefusal.cs` (`SetupRequiredException`, placeholder helper)
- Tests under `tests/Lunate.Coding.Tests/` covering the refused-binding first message and the no-answer case
- No public API, dependency, or settings-format change.
