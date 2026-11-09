## 1. Pin the refusal behaviour with tests

- [x] 1.1 Add a regression test in `InteractiveSessionUsageProfileTests`: a `chat` usage bound to a `systemone` profile, after the first message, refuses with the binding's message (profile, model, family, `/setup`, `lunate setup`) and creates no chat client.
- [x] 1.2 Add a regression test for the genuine no-chat-answer session: the first message still refuses with "no chat model is configured" and `lunate setup`, and creates no chat client.
- [x] 1.3 Confirm 1.1 fails on the current code with "no chat model is configured" in the run-failed notice before the fix.

## 2. Carry the binding message through the placeholder

- [x] 2.1 Give `SetupRequiredException` a text-carrying shape while the argument-less shape keeps `SetupRefusal.Text`.
- [x] 2.2 Add one `SetupRefusal` helper that returns the binding's refusal when the chat binding was refused and the default refusal otherwise, keeping each message in one place.
- [x] 2.3 In `InteractiveSession.CreateSessionClient`, throw the placeholder refusal from that helper driven by `_invalidChatBinding`; the client is still never created.

## 3. Verify the change

- [x] 3.1 Run `dotnet build Lunate.slnx -c Release`, `dotnet csharpier format .`, `dotnet csharpier check .`, and the filtered test classes (`SetupRefusalTests`, `InteractiveSessionUsageProfileTests`, `DeferredChatClientTests`, and any session class referencing `SetupRequiredException`).
- [x] 3.2 Run `openspec validate refused-chat-binding-first-message --strict` and `openspec validate --all --strict`.
- [x] 3.3 Confirm no documentation change is needed: the binding wording and both commands already exist; only the refusal source changes.
