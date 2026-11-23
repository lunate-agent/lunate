## Context

`WebSearchTool` sends one GET to DuckDuckGo's HTML endpoint and parses `result__a` /
`result__snippet` anchors. It treats every 2xx as a usable page and renders an empty anchor list as
"No results found". DuckDuckGo answers a rate-limited or challenged client with a 202 (or a 200
challenge page) whose body has no result anchors, so the model sees a bad-query result for an
endpoint that is refusing it. The existing spec's "no matches is a result" rule and its "HTTP
failure names the endpoint" rule need to say which empty page is which. The in-force ADRs that
still constrain the tool are ADR-0013 (the agent loop owns tool execution), ADR-0026 (no new
generic dependency for this path) and ADR-0041 (`net11.0`), none of which this change revisits.

## Goals / Non-Goals

**Goals:**

- A non-200 response is an error naming the endpoint, the status/reason and that the endpoint is
  blocking or rate-limiting the client.
- A 200 page with no result anchors is the same error unless it carries a genuine no-results
  marker; a genuine empty result keeps the current non-error text.
- Both shapes are covered offline by committed recorded fixtures.

**Non-Goals:**

- No retry, backoff, alternate search backend, cookie jar or user-agent change.
- No new dependency, settings key, public API or session-format change.
- No change to result parsing, caps or approval behaviour.

## Decisions

### 1. One blocked/rate-limited error for status and shape failures

Both a non-200 response and an unrecognised 200 page return the same error family, naming the
endpoint and saying the endpoint is blocking or rate-limiting the client; the status path also
carries the HTTP status and reason phrase. The model's next step is "try again later", not
"rewrite the query". Alternatives rejected: a distinct message per status hides the shared cause,
and keeping the old "search failed … try again later" loses the blocked/rate-limited diagnosis the
card asks for.

### 2. A genuine no-results page is recognised by its own marker

A 200 page with no result anchors is a genuine empty result only when the body carries a
DuckDuckGo no-results marker (`no-results` / "No results found for"). Anything else — an empty
body, a challenge page, an unknown shape — is a blocked/rate-limited error. The marker is a private
constant in `WebSearchTool`, matching the existing tolerant parser style. Alternatives rejected:
trusting an empty anchor list is the defect; trusting HTTP status alone cannot see a 200 challenge;
marking every no-anchor page as "no results" keeps the wrong model instruction.

### 3. Recorded fixtures, no network in tests

The tests read a committed challenge/blocked page and a committed genuine no-results page through
the existing `StubHttpHandler`, using the same fixture helper as the ranked-results test. The
challenge fixture is served once as 202 and once as 200, so the status path and the shape path are
both pinned without a live endpoint.

## Risks / Trade-offs

- **DuckDuckGo changes its no-results markup** -> the marker set is small and central; an
  unrecognised genuine empty page becomes a blocked error, which is a visible, retryable failure
  rather than a misleading query verdict. The marker can be extended in a follow-up.
- **A block page carries a no-results-looking string** -> the markers are page-structure text
  (`no-results`, "No results found for"), not the word "blocked", and the recorded challenge page
  is a regression fixture.
- **The message invites waiting rather than query repair** -> that is the card's intent; a genuinely
  bad query on a working endpoint still returns the normal no-results result.

## Migration Plan

The change is internal to one tool. Rollback is a revert of the branch; no stored data or settings
change.

## Open Questions

None.
