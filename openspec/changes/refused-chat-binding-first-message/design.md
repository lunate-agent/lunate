## Context

The interactive session composes a placeholder model when it has no chat answer, or when `usages.chat`
was refused by the shared `systemone` family rule (`UsageProfileValidation`). In both cases the
placeholder's client is a `DeferredChatClient` whose factory throws, so the first request cannot be
sent. Today that factory always throws `SetupRequiredException()` carrying `SetupRefusal.Text`, the
configure-it sentence for a machine with no chat model. For a refused binding the sentence is false and
omits `/setup`.

The startup path already computes the exact binding message in `ConfiguredChatRefusal()` and stores it
in `_invalidChatBinding`; it is the same text the session's one warning shows.

## Goals / Non-Goals

**Goals:**

- The first message on a refused chat binding refuses with the binding's own message, including the
  profile, model, family, `/setup` and `lunate setup`.
- The no-chat-answer placeholder keeps the configure-it text.
- Each message still has exactly one definition.
- No request is sent in either case.

**Non-Goals:**

- Changing when the placeholder is used, the startup warning, or the wizard trigger.
- Changing the binding's wording in `UsageProfileValidation`.
- The setup wizard summary line (issue #717) or `docs/first-run.md`.

## Decisions

- **Carry the binding text, do not reformat it.** `SetupRequiredException` gains a text-carrying
  constructor and keeps an argument-less one defaulting to `SetupRefusal.Text`. The binding text already
  names both repair commands, so copying it into a second wording would let the startup warning and the
  first-message refusal drift.
- **Choose the refusal in `SetupRefusal`.** A helper, `ForPlaceholder(string? invalidChatBinding)`,
  returns the binding's exception when the binding was refused and the default exception otherwise. The
  session calls the helper from `CreateSessionClient`; the choice and both texts stay in one file.
- **Keep the deferral.** The thrown exception stays inside the `DeferredChatClient` factory, exactly as
  today, so the structural no-request invariant is unchanged.

Lightweight dynamic view of the first message (ASCII, because the change is local and the diagram only
needs to show the refusal source):

```text
InteractiveSession.Run
  -> AgentHarness (client = DeferredChatClient)
    -> DeferredChatClient.GetStreamingResponseAsync
      -> factory throws SetupRequiredException
         SetupRefusal.ForPlaceholder(_invalidChatBinding)
           refused binding -> binding message (profile/model/family, /setup, lunate setup)
           no chat answer  -> SetupRefusal.Text (no chat model is configured, lunate setup)
  -> session renders "Run failed: <message>"
```

## Risks / Trade-offs

- [The binding message may be long for the run-failed line] -> It is already the startup warning's
  text and the session wraps it; no truncation is introduced by this change.
- [A second exception shape could leak to machine modes] -> Only the placeholder factory uses it; print
  and ACP keep catching `SetupRequiredException` as before, and the binding refusal path in those modes
  exits 1 with the same binding text (#714).

## Migration Plan

None: in-process refusal text only, no file or wire format change.

## Open Questions

None.
