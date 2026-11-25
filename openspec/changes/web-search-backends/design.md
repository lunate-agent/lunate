## Context

`web_search` is a core tool in `Lunate.Coding` (`src/Lunate.Coding/Web/WebSearchTool.cs`): one
unauthenticated `GET` to `https://html.duckduckgo.com/html/?q=<query>`, DuckDuckGo's anchors turned
into ranked title/URL/snippet results, bounded in count and UTF-8 output bytes, declaring
`ToolRisk.Execute` so the approval path is the same as `bash`. The endpoint, the parser and the
blocked/empty classification are all in that one file, so a session whose network refuses
DuckDuckGo has no alternative inside the tool.

Two facts constrain the shape of the change:

- Settings are validated at load, fail closed and are user-scoped where a value can spend a secret
  or choose an endpoint: a project file may not define a provider, and it may not define a tool
  environment pass-through either. The generated `docs/settings.md` table is rendered from
  `settings-schema.json`, and tests pin that file to the parser's own literals in both directions.
- Provider credentials are never stored resolved. A provider entry holds a reference
  (`auth:`, `env:`, `helper:`) validated at load and resolved at send time; settings never carry the
  value. `helper:` is accepted by the schema and refused at send time because this host cannot
  resolve one.

The maintainer decision on #1024 keeps the local tool and gives it a backend seam; provider-native
search is a separate layer (card B). This change is the seam and the record of that decision.

## Goals / Non-Goals

**Goals:**

- DuckDuckGo stays the backend of a session that configures nothing: same endpoint, same parser,
  same approval tier, same result bytes.
- SearXNG (a user-run instance), Brave and Tavily are selectable through one user-scoped
  `tools.webSearch` setting, and each backend's credential is a reference resolved the way provider
  credentials are.
- The tool's argument contract, approval tier, count cap and byte cap are one implementation, not
  four: a backend supplies the request and the answer, nothing else.
- Every backend is testable offline from a committed recorded fixture.

**Non-Goals:**

- No provider-native search (a search inside the provider request): that is card B and the ADR
  records the split.
- No search usage/attribution value, no session-format change, no per-backend tool names.
- No backend-specific knobs beyond the three settings keys: no result-language, region, safesearch,
  category or timeout tuning.
- No retry, caching, failover between backends, or mid-session backend switch.

## Decisions

### 1. One tool, one backend seam

`WebSearchTool` keeps the argument validation, the HTTP execution, the timeout and error
classification, and the rendering. A `WebSearchBackend` supplies what differs:

| Backend | Request | Credential placement | Answer |
| --- | --- | --- | --- |
| `duckduckgo` | `GET html.duckduckgo.com/html/?q=` | none | HTML anchors (`result__a`, `result__snippet`) |
| `searxng` | `GET <url>/search?q=` | optional `Authorization: Bearer` | HTML `<article class="result">` blocks |
| `brave` | `GET api.search.brave.com/res/v1/web/search?q=&count=` | `X-Subscription-Token` | JSON `web.results[]` |
| `tavily` | `POST api.tavily.com/search` | `Authorization: Bearer` | JSON `results[]` |

One file per backend beside the seam (`WebSearchBackend.DuckDuckGo.cs`, `.SearXng.cs`, `.Brave.cs`,
`.Tavily.cs`), structurally symmetric: each builds a URI, states its header/body, and reads a body
into a shared `SearchPage` (`Results`, `NoResults`). DuckDuckGo's existing anchor/`uddg`/no-results
code moves into its file unchanged; `LooksLikeNoResults` becomes the shared HTML rule both HTML
backends use.

Alternatives rejected: a backend enum switched inside one method (one file grows past every sibling
and the shapes stop being symmetric); a backend per tool name (`web_search_brave`) — it multiplies
the approval and `--tools` surface for one setting; a generic HTTP tool — it removes the ranked
result shape the model reads.

### 2. The backend is a tool setting, not a usage

`tools.webSearch` sits beside `tools.extra`:

```json
{
  "tools": {
    "webSearch": {
      "backend": "searxng",
      "url": "https://search.example.test/",
      "credential": "env:SEARXNG_TOKEN"
    }
  }
}
```

`backend` defaults to `duckduckgo`; `url` belongs to `searxng`; `credential` is a provider-form
reference and is required by `brave` and `tavily`, optional for a `searxng` instance behind an
authenticating proxy, and refused for `duckduckgo`. The reader validates the cross-field rules at
load, so a session never starts on a backend it cannot use: an unknown backend names the four
accepted values, a missing `url`/`credential` names the key, and an irrelevant `url`/`credential` is
refused rather than silently ignored — the same fail-closed style as a provider entry.

A backend is not a model usage: no `AgentUsage`, no spending attribution and no session entry
changes. The card states this explicitly, and a search that costs an API call is the backend's
billing, not a model usage Lunate prices.

Alternatives rejected: a per-backend usage/attribution value (the card forbids it, and `/cost` would
gain a lane nothing prices); a separate `webSearch` top-level section (a backend is a tool setting,
and `tools.` is where tool settings live); env-var overrides (`tools.extra`'s
`LUNATE_EXTRA_TOOLS` aside, endpoint and credential selection is not something a shell environment
should redirect silently).

### 3. The credential is resolved at send time, like a provider's

The setting holds a reference. `WebSearchCredential` parses `auth:`, `env:` and `helper:` with
`ProviderEntry.IsCredentialReference` at load, and resolves at send time from the auth store's named
key source (`AuthStore.TryGet`) or the environment lookup the frontend already passes to the auth
store. An unresolved reference is an error result naming `tools.webSearch.credential`, the reference
and the fix, and no request is sent; a `helper:` reference is refused with the same wording a
provider header uses, because this host cannot resolve one. The value appears only in the one header
the backend names, never in the result text.

Creating the configuration also registers an `env:` reference with `CredentialVariables`, the strip
list the inherited child-environment policy removes, exactly as a configured provider's `env:`
reference is registered at store load. A search credential is a secret Lunate holds; a model-chosen
`bash` command must not be able to read the variable it came from.

```text
+--------------------------- lunate host ----------------------------------+
|                                                                          |
|  frontend (print / interactive / ACP)                                    |
|    settings + auth store + environment                                   |
|         |                                                                |
|         v                                                                |
|  WebSearchConfiguration ---- registers env: name -> CredentialVariables  |
|         |                                                                |
|         v                                                                |
|  HarnessFactory.CreateTools -> WebSearchTool (Execute risk)              |
|                                   |                                      |
|                                   +-- backend seam: duckduckgo | searxng  |
|                                       | brave | tavily                   |
+-----------------------------------|--------------------------------------+
                                    | one HTTP request, credential in the
                                    v backend's one placement
                        +--------------------------+
                        | search endpoint          |
                        | (DDG / SearXNG / API)    |
                        +--------------------------+
```

The approval path is untouched: the tool still declares `Execute`, so an interactive session asks
per call, ACP asks its client, and an unattended run declines unless the run explicitly allows
everything — whatever backend is selected. A declined call sends no request and therefore spends no
credential.

### 4. Blocked versus empty is per answer shape

Non-200 is a blocked/rate-limited error naming the endpoint, the status and the reason, for every
backend. For a genuine empty answer:

- JSON backends answer with an empty result array, which is an empty search by construction.
- HTML backends keep the strict rule the blocked-endpoint change introduced: no result links is only
  an empty search when the page carries DuckDuckGo's/SearXNG's own no-results element and marker;
  otherwise the page is not a results page and the endpoint is blocking or challenging the client.

### 5. Recorded fixtures, not network

Each backend's test reads a fixture under `tests/fixtures/web/` shaped like the real answer
(`searxng-results.html`, `searxng-no-results.html`, `brave-results.json`, `brave-no-results.json`,
`tavily-results.json`, `tavily-no-results.json`) through `StubHttpHandler`, which also records the
request headers and body so a test can assert the credential's single placement. No test resolves a
real reference from the process environment: every credential in a test is a `Func<string, string?>`
stub.

## Risks / Trade-offs

- **A SearXNG instance's HTML drifts** → the parser reads `<article class="result">` and its first
  anchor and paragraph, tolerating attribute order and extras, and reports a page with no result
  blocks as blocked rather than as an empty search, so drift is visible instead of silent.
- **Rejecting an irrelevant `url`/`credential` makes switching back to DuckDuckGo require deleting
  the old keys** → accepted: a contradictory file fails loudly at start instead of carrying a secret
  reference nothing reads.
- **An `env:` reference whose name is not credential-shaped could reach a tool child** → creating
  the configuration registers it in the same strip list providers use, and `auth:` is the form that
  keeps the value out of the environment entirely.
- **The Brave/Tavily request shapes are recorded by hand, not from a live call** → the fixture
  documents the fields read; an upstream shape change surfaces as a blocked error naming the
  endpoint (no results and no result array entry), which is actionable, and the ADR records that
  each backend's wire belongs to that backend's file.
- **A configured backend makes `web_search` spend money** → the approval tier is unchanged and
  `Execute`, so the call is approved before it is sent; the trade-off is one prompt per search.

## Migration Plan

No migration: a settings file without `tools.webSearch` resolves to DuckDuckGo, which is the
existing behaviour. Rolling back is removing the section or the release; nothing is persisted in a
new shape.

## Open Questions

None. The maintainer decision on #1024 already fixed both layers; the ADR records it rather than
proposing a new one.
