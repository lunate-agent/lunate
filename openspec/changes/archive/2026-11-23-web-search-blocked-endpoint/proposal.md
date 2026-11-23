## Why

DuckDuckGo answers a rate-limited or challenged `web_search` client with a page that is still a
2xx — commonly HTTP 202, with no `result__a` anchors. The tool calls that "No results found … Try a
different query", so the model rewrites a query the endpoint already refuses and retries against the
block. A blocked endpoint must read as a blocked endpoint, not as a bad query.

## What Changes

- A non-200 search response returns an error that names the search endpoint, the HTTP status and
  the reason, and says the endpoint is blocking or rate-limiting the client.
- A 200 response with no result anchors is an error with the same blocked/rate-limited wording
  unless the page carries a genuine no-results marker inside DuckDuckGo's own no-results element
  (`no-results` / "No results found for"); marker-looking text echoed elsewhere does not count.
- A genuine empty result keeps the current non-error text, "No results found for '<query>'. Try a
  different query."
- Tests drive both shapes from committed recorded fixtures — a DuckDuckGo challenge/blocked page
  and a genuine no-results page — so no test reaches the network.
- `docs/dev/guide.md` names the blocked/rate-limited error in the `web_search` row.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `web-tools`: `web_search` distinguishes a blocked or rate-limited endpoint (non-200, or a 200
  page without result anchors or a genuine no-results marker) from a genuine empty result, and
  names the endpoint in both failures.

## Impact

- `src/Lunate.Coding/Web/WebSearchTool.cs`: status handling and the no-anchor classification.
- `tests/Lunate.Coding.Tests/Web/WebSearchToolTests.cs` and new fixtures under
  `tests/fixtures/web/`.
- `docs/dev/guide.md`: the `web_search` error row.
- No public API, package, dependency, settings key or session-format change.
