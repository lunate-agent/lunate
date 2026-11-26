# ADR-0046: Web search has two layers — the local tool with a backend seam, and provider-native search

- Status: accepted — 2026-11-25 (records the maintainer decision on #1024, per issue #1029)
- Date: 2026-11-25
- Refs: #1024 (the decision), #1029 (this record and the seam)

## Context

`web_search` shipped with one hard-coded backend: an unauthenticated request to DuckDuckGo's HTML
endpoint. That gives a plain install search with no key, but leaves two users without the tool: one
whose network blocks or rate-limits DuckDuckGo, and one who already pays for a search API. Both fall
back to `bash` plus `curl`, which is a single opaque command to the approval panel and free text to
the model.

Some providers also offer search natively: the provider runs the search inside the chat request, so
no tool call, no result rendering and no approval prompt happen in Lunate. That is a cheaper and
faster path, and it is a different one: the search leaves Lunate's tool loop entirely.

The maintainer settled the direction on #1024: keep the local tool and give it a backend seam;
provider-native search is a separate layer, tracked as its own card.

## Decision

1. **The local `web_search` tool stays, with a backend seam.** The tool keeps its argument contract,
   its `ToolRisk.Execute` approval tier, its result shape and its count and byte caps. A backend
   supplies the one request and the one answer shape for a provider: DuckDuckGo (the zero-config
   default), SearXNG (a user-run instance named by URL), Brave and Tavily (API backends).
2. **The backend is a user-scoped tool setting, not a usage.** `tools.webSearch` carries `backend`,
   `url` and `credential`. It is not an attribution value: no model usage, no spending lane and no
   session-format change. It is user scope because a project file could otherwise point the search at
   an endpoint of its choosing and spend the user's credential there — the same reason a project may
   not define a provider.
3. **A backend's credential is a reference, resolved like a provider credential.** The value is
   `auth:<name>`, `env:<VARIABLE>` or `helper:<name>`, validated when settings load and resolved when
   the call is sent, from the auth store or the environment. Settings never hold the value; a
   `helper:` reference is accepted at load and refused at use, because this host cannot resolve one.
   A configured `env:` reference joins the names the inherited child-environment policy strips, so a
   model-chosen command cannot read the variable the credential came from.
4. **Provider-native search is the second layer, and it stays separate.** It is off unless a user
   opts in, it runs inside the provider request rather than through the loop, and it is delivered by
   its own card. The local tool is not re-implemented on top of it, and it is not removed once it
   exists: the two layers answer different questions (Lunate-visible search versus
   provider-side search).
5. **The approval trade-off is explicit and deliberate.** Every local search is an `Execute`-risk
   tool call: an interactive session asks per call (or once per session with `[a]lways`), ACP asks
   its client, and an unattended run declines unless the run allows everything. That prompt is the
   cost. What it buys is that the query, the backend, the result and the credential spend are all in
   front of the approval path: a declined call sends no request and spends no key, and selecting a
   paid backend does not change who approves. Provider-native search trades that visibility for
   fewer prompts and lower latency, which is exactly why it is a separate, opt-in layer and why this
   ADR does not fold the two together.

## Consequences

- A session that configures nothing behaves as before: DuckDuckGo, no credential, the same result
  bytes and the same approval prompt.
- Selecting SearXNG, Brave or Tavily is one settings object; the tool contract, the approval tier and
  the caps do not vary by backend, so `--tools`, the approval panel and the result rendering need no
  per-backend case.
- A configured backend can spend money on an approved call. The user chose it, and the call is still
  approved before it is sent; `/cost` does not price it because it is not a model usage.
- The credential-referencing model now covers search backends as well as providers, including the
  tool-child strip and the secret-redaction sources' existing coverage. A future third-party API a
  tool calls should follow this shape rather than introducing a literal key setting.
- Each backend's wire (URL shape, credential placement, result fields) is owned by that backend's
  file. An upstream change is a one-file change plus a fixture; it does not move the tool, the
  setting or the approval path.
- Provider-native search, when it lands, is additive: it does not change the local tool's setting or
  the meaning of `web_search`.

## Alternatives rejected

- **Keep the hard-coded DuckDuckGo endpoint.** It is what the tool has today, and it leaves the
  blocked-network and paid-API users with `bash`+`curl` — the situation the decision exists to fix.
- **Replace the local tool with provider-native search.** It removes the search from the loop and
  from the approval path, so an unattended or editor session would search without a decision point,
  and a provider without native search would lose search entirely.
- **One tool per backend (`web_search_brave`, …).** It multiplies the model-facing tool list, the
  `--tools` surface and the approval grants for what is a setting.
- **A backend as a model usage with attribution.** The card forbids it, and it would add a `/cost`
  lane nothing prices and a session entry nothing resumes.
- **A literal API key in settings.** It contradicts the repository's credential rule; a secret in
  `settings.json` is readable by every process and project that can read the file.
- **A `webSearch` top-level settings section.** A backend is a tool setting; `tools.` is where tool
  settings live, beside `tools.extra`.
