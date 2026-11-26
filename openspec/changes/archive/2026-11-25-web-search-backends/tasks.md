## 1. Fixtures and failing tests first

- [x] 1.1 Add recorded-shape fixtures under `tests/fixtures/web/`: `searxng-results.html`,
  `searxng-no-results.html`, `brave-results.json`, `brave-empty-results.json`,
  `brave-no-web-object.json`, `brave-api-error.json`, `brave-results-not-an-array.json`,
  `tavily-results.json`, `tavily-no-results.json`, `tavily-api-error.json`.
- [x] 1.2 Extend `tests/Lunate.Coding.Tests/Web/StubHttpHandler.cs` (`StubRequest`) to record the
  request headers and body so a test can assert the credential's one placement.
- [x] 1.3 Add `WebSearchBackendTests` (or extend `WebSearchToolTests`) covering: DuckDuckGo as the
  no-configuration default; SearXNG by URL without and with a bearer credential; Brave's request URI,
  `X-Subscription-Token` and JSON result mapping; Tavily's POST body, bearer header and JSON result
  mapping; an empty JSON answer as a non-error no-results; a non-200 API answer as a blocked error
  naming that endpoint.
- [x] 1.4 Add `WebSearchCredentialTests`: an `auth:` reference resolves from the named-key source, an
  `env:` reference from the environment lookup, an unset reference names `tools.webSearch.credential`
  and sends no request, and a `helper:` reference is refused at use with no request.
- [x] 1.5 Add settings tests: `tools.webSearch` defaults resolve to DuckDuckGo; a valid SearXNG,
  Brave and Tavily section resolves; an unknown backend, a missing `url`, a missing `credential`, an
  irrelevant `url`/`credential` and a literal secret are each refused by key; a project settings file
  carrying `tools.webSearch` is refused.
- [x] 1.6 Add the registered `env:` reference case to `CredentialVariablesTests`: composing the web
  search configuration registers the name in the strip list.
- [x] 1.7 Run the new and edited test classes and confirm the new cases fail before implementation.
- [x] 1.8 Add the rework cases the review of PR #1034 asked for: a 200 API answer that carries no
  `results` array — a missing container, an API error object, a non-array field — is a blocked page,
  and only a present, empty result array is a genuine no-results answer.

## 2. Implementation

- [x] 2.1 Add `src/Lunate.Coding/Web/WebSearchBackend.cs`: the seam (`WebSearchBackend`), the shared
  `SearchResult`/`SearchPage` model, the backend table and the factory from `WebSearchSettings`.
- [x] 2.2 Move DuckDuckGo's request, anchor/`uddg` parsing and no-results classification into
  `WebSearchBackend.DuckDuckGo.cs` unchanged in behaviour.
- [x] 2.3 Add `WebSearchBackend.SearXng.cs` (instance `/search`, optional bearer, `article.result`
  HTML parsing, shared no-results marker rule), `WebSearchBackend.Brave.cs` and
  `WebSearchBackend.Tavily.cs` (JSON answers, their own credential headers).
- [x] 2.4 Add `src/Lunate.Coding/Web/WebSearchCredential.cs`: reference validation and send-time
  resolution over the auth-store key source and the environment lookup, with the actionable errors.
- [x] 2.5 Add `src/Lunate.Coding/Web/WebSearchConfiguration.cs`: the settings, the lookups, and the
  `env:` registration with `CredentialVariables`.
- [x] 2.6 Reduce `src/Lunate.Coding/Web/WebSearchTool.cs` to argument validation, execution, timeout
  and error classification, and rendering over a backend; its description names the configured
  backend.
- [x] 2.7 Add the `tools.webSearch` section: `SettingsStore.WebSearch.cs` (record, enum, table,
  reader, validator), the `ToolsSettings.WebSearch` property, the parser-scan prefix entry and the
  three entries in `settings-schema.json`.
- [x] 2.8 Thread one `WebSearchConfiguration` from `PrintMode`, `AcpMode` and `InteractiveSession`
  into `HarnessFactory.CreateTools`/`CreateWorkspaceTools` and so into every `WebSearchTool`.
- [x] 2.9 Run the focused test classes again until green.
- [x] 2.10 Make `SearchJson.Read` classify a JSON answer strictly: only an empty `results` array is
  a no-results answer, and every other shape returns `null` so the tool takes its blocked path.

## 3. Documentation and release note

- [x] 3.1 Name the backend setting, its reference rule and the approval trade-off in
  `docs/usage.md`, `docs/configuration.md` and `docs/security.md`; update the `web_search` row in
  `docs/dev/guide.md`.
- [x] 3.2 Regenerate `docs/settings.md` with `scripts/docs-sync`.
- [x] 3.3 Add the release-note fragment `changes/<pull-request-number>.md` under the release's
  headings.
- [x] 3.4 Run `dotnet csharpier format .`, the focused Release builds and tests, and
  `openspec validate --all --strict`.

## 4. Finish

- [x] 4.1 Sync the delta specs into `openspec/specs/`, then archive as the last commit:
  `openspec archive web-search-backends --yes`.
