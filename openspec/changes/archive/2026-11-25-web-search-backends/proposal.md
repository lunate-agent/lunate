## Why

`web_search` has one hard-coded backend: DuckDuckGo's HTML endpoint. A user whose network blocks or
rate-limits that endpoint has no way to keep the tool, and a user who already pays for a search API
has no way to use it: both fall back to `bash` plus `curl`, which the approval panel sees as one
opaque command and the model reads as free text.

The maintainer decision on #1024 keeps the local tool — a search is a tool call the approval path
sees and the session can grant — and gives it a backend seam. Provider-native search (a search the
provider runs inside the chat request) is the separate second layer, card B.

## What Changes

- `web_search` gains a backend seam with four backends: DuckDuckGo, the zero-config default; SearXNG,
  a user-run instance named by URL; and the Brave and Tavily APIs.
- A new user-scoped `tools.webSearch` settings section selects the backend and names what it needs:
  `backend` (`duckduckgo`, `searxng`, `brave` or `tavily`), `url` for the SearXNG instance, and
  `credential` for the backend's credential reference.
- The credential value is a reference in the provider form (`auth:<name>`, `env:<VARIABLE>` or
  `helper:<name>`): validated when settings load, resolved when the call is sent from the same auth
  store and environment provider credentials resolve from, and never a literal secret in settings.
- The backend is a tool setting, not a usage: no attribution value is recorded, and the session
  format does not change.
- An ADR records both layers of the #1024 decision — the local tool with its backend seam, and
  provider-native search as a separate layer — including the approval trade-off between them.
- Tests drive every backend from committed recorded fixtures; no test reaches the network.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `web-tools`: `web_search` sends its one request to the configured backend, which may be an
  authenticated API endpoint, and the blocked-versus-empty distinction is defined per backend
  response shape.
- `agent-config`: the `tools.webSearch` settings section selects the search backend, its URL and its
  credential reference, validated at load and user-scoped.
- `child-environment`: a configured backend's `env:` credential reference joins the names the
  inherited policy strips from a tool child.

## Impact

- `src/Lunate.Coding/Web/`: `WebSearchTool` becomes a thin tool over a backend seam, with one file
  per backend, a shared result model and the credential resolver.
- `src/Lunate.Coding/Settings/SettingsStore*.cs` and `settings-schema.json`: the `tools.webSearch`
  section, its reader and its load-time validation.
- `src/Lunate.Coding/Sessions/HarnessFactory.cs`, `Print/PrintMode.cs`, `Acp/AcpMode.cs` and
  `Sessions/InteractiveSession.cs`: one `WebSearchConfiguration` handed to the tool registry.
- `tests/Lunate.Coding.Tests/Web/` and `tests/fixtures/web/`: backend tests and recorded fixtures;
  `StubHttpHandler` also records request headers and the request body.
- `docs/usage.md`, `docs/configuration.md`, `docs/security.md`, `docs/dev/guide.md`, the generated
  `docs/settings.md`, and a new durable ADR under `adr/`.
- No new NuGet package, no public contract assembly, no session-format change and no
  approval-matrix change.
