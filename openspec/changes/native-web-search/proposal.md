## Why

Some chat providers run a web search inside the chat request, so no tool call, no approval
prompt and no local backend request happen in Lunate. ADR-0046 records that this is the second
web-search layer and that it is off unless a user opts in, because a native search cannot be
approved per call. This change implements that layer: a model that declares the `web_search`
feature and a user who opts in get the provider's built-in search; every other session behaves
exactly as it does today.

## What Changes

- Add the user-scoped `tools.nativeSearch` boolean setting, off by default. It is the explicit
  opt-in for provider-native search; a project settings file may not carry it because an
  unapprovable search must not be switchable from a repository.
- When the opt-in is set **and** the request's chat model declares the `web_search` capability
  feature, the request declares the provider's built-in search (`HostedWebSearchTool`) and does
  not declare the local `web_search` tool.
- Without the opt-in, or with a model that does not declare the feature, nothing changes: the
  local `web_search` tool is declared and no native search is requested.
- Billing and attribution stay on the chat call. No new usage value, cost lane or session entry
  is added; native search is part of the chat request.
- The provider adapters are unchanged: MEAI's hosted tool is what the OpenAI Chat and Responses
  adapters already map to the provider's own search option.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `agent-config`: accepts and validates `tools.nativeSearch` as a user-scoped boolean, default
  false, in the generated settings reference.
- `web-tools`: adds the provider-native search layer behind the opt-in, including the model
  feature condition, the request declaration and the attribution rule.

## Impact

- `src/Lunate.Coding/Settings/SettingsStore.cs` (`ToolsSettings` and the `tools` reader),
  `src/Lunate.Coding/settings-schema.json`, and the generated `docs/settings.md`.
- `src/Lunate.Agent/Loop/AgentHarnessOptions.cs` and
  `src/Lunate.Agent/Loop/AgentHarness.cs` (`BuildOptions`), plus `Lunate.Agent`'s public API
  surface.
- `src/Lunate.Coding/Print/PrintMode.cs`, `src/Lunate.Coding/Sessions/InteractiveSession.cs` and
  `src/Lunate.Coding/Acp/AcpMode.cs` pass the opt-in into the harness.
- Tests in `tests/Lunate.Agent.Tests` and `tests/Lunate.Coding.Tests`; no network is used.
- Documentation: `docs/configuration.md`, `docs/usage.md`, `docs/security.md` and the `web_search`
  row in `docs/dev/guide.md`; `docs/settings.md` is regenerated.
