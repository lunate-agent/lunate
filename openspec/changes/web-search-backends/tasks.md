## 1. Fixtures and failing tests first

- [ ] 1.1 Add recorded-shape fixtures under `tests/fixtures/web/`: `searxng-results.html`,
  `searxng-no-results.html`, `brave-results.json`, `brave-no-results.json`, `tavily-results.json`,
  `tavily-no-results.json`.
- [ ] 1.2 Extend `tests/Lunate.Coding.Tests/Web/StubHttpHandler.cs` (`StubRequest`) to record the
  request headers and body so a test can assert the credential's one placement.
- [ ] 1.3 Add `WebSearchBackendTests` (or extend `WebSearchToolTests`) covering: DuckDuckGo as the
  no-configuration default; SearXNG by URL without and with a bearer credential; Brave's request URI,
  `X-Subscription-Token` and JSON result mapping; Tavily's POST body, bearer header and JSON result
  mapping; an empty JSON answer as a non-error no-results; a non-200 API answer as a blocked error
  naming that endpoint.
- [ ] 1.4 Add `WebSearchCredentialTests`: an `auth:` reference resolves from the named-key source, an
  `env:` reference from the environment lookup, an unset reference names `tools.webSearch.credential`
  and sends no request, and a `helper:` reference is refused at use with no request.
- [ ] 1.5 Add settings tests: `tools.webSearch` defaults resolve to DuckDuckGo; a valid SearXNG,
  Brave and Tavily section resolves; an unknown backend, a missing `url`, a missing `credential`, an
  irrelevant `url`/`credential` and a literal secret are each refused by key; a project settings file
  carrying `tools.webSearch` is refused.
- [ ] 1.6 Add the registered `env:` reference case to `CredentialVariablesTests`: composing the web
  search configuration registers the name in the strip list.
- [ ] 1.7 Run the new and edited test classes and confirm the new cases fail before implementation.

## 2. Implementation

- [ ] 2.1 Add `src/Lunate.Coding/Web/WebSearchBackend.cs`: the seam (`WebSearchBackend`), the shared
  `SearchResult`/`SearchPage` model, the backend table and the factory from `WebSearchSettings`.
- [ ] 2.2 Move DuckDuckGo's request, anchor/`uddg` parsing and no-results classification into
  `WebSearchBackend.DuckDuckGo.cs` unchanged in behaviour.
- [ ] 2.3 Add `WebSearchBackend.SearXng.cs` (instance `/search`, optional bearer, `article.result`
  HTML parsing, shared no-results marker rule), `WebSearchBackend.Brave.cs` and
  `WebSearchBackend.Tavily.cs` (JSON answers, their own credential headers).
- [ ] 2.4 Add `src/Lunate.Coding/Web/WebSearchCredential.cs`: reference validation and send-time
  resolution over the auth-store key source and the environment lookup, with the actionable errors.
- [ ] 2.5 Add `src/Lunate.Coding/Web/WebSearchConfiguration.cs`: the settings, the lookups, and the
  `env:` registration with `CredentialVariables`.
- [ ] 2.6 Reduce `src/Lunate.Coding/Web/WebSearchTool.cs` to argument validation, execution, timeout
  and error classification, and rendering over a backend; its description names the configured
  backend.
- [ ] 2.7 Add the `tools.webSearch` section: `SettingsStore.WebSearch.cs` (record, enum, table,
  reader, validator), the `ToolsSettings.WebSearch` property, the parser-scan prefix entry and the
  three entries in `settings-schema.json`.
- [ ] 2.8 Thread one `WebSearchConfiguration` from `PrintMode`, `AcpMode` and `InteractiveSession`
  into `HarnessFactory.CreateTools`/`CreateWorkspaceTools` and so into every `WebSearchTool`.
- [ ] 2.9 Run the focused test classes again until green.

## 3. Documentation and release note

- [ ] 3.1 Name the backend setting, its reference rule and the approval trade-off in
  `docs/usage.md`, `docs/configuration.md` and `docs/security.md`; update the `web_search` row in
  `docs/dev/guide.md`.
- [ ] 3.2 Regenerate `docs/settings.md` with `scripts/docs-sync`.
- [ ] 3.3 Add the release-note fragment `changes/<pull-request-number>.md` under the release's
  headings.
- [ ] 3.4 Run `dotnet csharpier format .`, the focused Release builds and tests, and
  `openspec validate --all --strict`.

## 4. Finish

- [ ] 4.1 Sync the delta specs into `openspec/specs/`, then archive as the last commit:
  `openspec archive web-search-backends --yes`.
