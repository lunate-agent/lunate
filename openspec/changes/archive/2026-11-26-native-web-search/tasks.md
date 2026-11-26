## 1. Failing tests first

- [x] 1.1 Add `NativeWebSearchTests` under `tests/Lunate.Agent.Tests/Loop/`: opt-in plus a model
  whose capability features include `web_search` declares `HostedWebSearchTool` and no local
  `web_search` function; without the opt-in the local function is declared and no hosted tool is;
  with the opt-in but a model that does not declare the feature the local function is declared; a
  tool selection that omits `web_search` declares neither.
- [x] 1.2 Add the attribution case: a chat usage model that declares `web_search`, the opt-in on,
  and a usage report still produces one `chat` attribution with no separate native-search usage.
- [x] 1.3 Add settings tests to `WebSearchSettingsTests` and `SettingsSchemaTests`: the default is
  off, `true` resolves, a non-boolean is refused naming `tools.nativeSearch`, and the schema probe
  carries the key.
- [x] 1.4 Add a print-mode test that writes `tools.nativeSearch: true` and a catalog model declaring
  `web_search`, then asserts the request carries the hosted tool and no local `web_search`.
- [x] 1.5 Run the new and edited test classes in Release and confirm the new cases fail before the
  implementation.

## 2. Implementation

- [x] 2.1 Add `ToolsSettings.NativeSearch`, the `tools.nativeSearch` reader in
  `SettingsStore.cs`, and the `settings-schema.json` entry; regenerate the settings reference.
- [x] 2.2 Add `AgentHarnessOptions.NativeWebSearch`, its public API entries, and the per-request
  decision in `AgentHarness.BuildOptions`: when the opt-in is on, the request model declares
  `web_search`, and the active tool selection offers `web_search`, drop the local declaration and
  append `HostedWebSearchTool`.
- [x] 2.3 Pass `settings.Tools.NativeSearch` into `AgentHarnessOptions` from `PrintMode`,
  `InteractiveSession` and `AcpMode`.
- [x] 2.4 Run the focused test classes again until green.

## 3. Documentation and release note

- [x] 3.1 Document the opt-in, the model feature condition, the `--tools` rule and the attribution
  behaviour in `docs/configuration.md`, `docs/usage.md` and `docs/security.md`; update the
  `web_search` row in `docs/dev/guide.md`.
- [x] 3.2 Regenerate `docs/settings.md` with `scripts/docs-sync`.
- [x] 3.3 Add the release-note fragment `changes/<pull-request-number>.md` under the release's
  headings.
- [x] 3.4 Run `dotnet csharpier format .`, the focused Release builds and tests, and
  `openspec validate --all --strict`.

## 4. Finish

- [x] 4.1 Sync the delta specs into `openspec/specs/`, then archive as the last commit:
  `openspec archive native-web-search --yes`.
