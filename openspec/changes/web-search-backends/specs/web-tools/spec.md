## MODIFIED Requirements

### Requirement: Web search

`web_search` SHALL accept a required non-empty `query` string and an optional `max_results`
integer (default 5, at least 1, at most 10), SHALL send one HTTP request to the endpoint of the
backend the `tools.webSearch` setting selects — DuckDuckGo by default — and SHALL return the ranked
results in rank order with a title, an absolute URL and a snippet for each result. When the backend
needs a credential, the request SHALL carry the value resolved from the configured credential
reference in the one place that backend's API expects it, and in no other header, query parameter or
body. The result SHALL name the query, SHALL number the results, and SHALL be bounded both in result
count (`max_results`) and in total UTF-8 output bytes (8 KiB) with a marker stating the cap; a cut
point SHALL NOT split a UTF-8 sequence or a UTF-16 surrogate pair. A search with no matches SHALL
return a readable "no results" result rather than an error only when the response is a genuine empty
answer: a JSON backend's empty result array, or an HTML page carrying a genuine no-results marker. A
200 HTML response with no result links and no such marker SHALL produce an error saying the endpoint
blocked or rate-limited the search. A non-200 response SHALL produce an error naming the endpoint,
the HTTP status and the reason and saying the endpoint blocked or rate-limited the search. An
unknown argument SHALL be refused with an error naming the accepted arguments.

#### Scenario: A query returns ranked title, URL and snippet

- **GIVEN** a stub search response with three ranked result links
- **WHEN** `web_search` runs with a query
- **THEN** the result lists the three results in rank order, each with its title, its absolute URL
  and its snippet, and the output names the query

#### Scenario: Result count is capped

- **GIVEN** a stub search response with more results than requested
- **WHEN** `web_search` runs with `max_results` set to the cap
- **THEN** the result carries exactly the capped number of results and no more

#### Scenario: Total output is capped in bytes

- **GIVEN** matching results whose rendered output exceeds 8 KiB
- **WHEN** `web_search` renders the result
- **THEN** the output stays within the byte cap, ends with a marker naming the cap, and splits no
  UTF-8 sequence or surrogate pair

#### Scenario: No matches is a result, not an error

- **GIVEN** a recorded genuine no-results page with a no-results marker and no result links
- **WHEN** `web_search` runs
- **THEN** it returns a non-error result saying no results were found for the query

#### Scenario: An empty JSON answer is a result, not an error

- **GIVEN** a recorded API answer whose result array is empty
- **WHEN** `web_search` runs against that API backend
- **THEN** it returns a non-error result saying no results were found for the query, and the error
  text about a blocked endpoint does not appear

#### Scenario: A non-200 response names the endpoint as blocked or rate-limited

- **GIVEN** a recorded blocked response with a non-200 status
- **WHEN** `web_search` runs
- **THEN** the error names the search endpoint and the status, and says the endpoint blocked or
  rate-limited the search

#### Scenario: A 200 challenge page is not a no-results result

- **GIVEN** a recorded blocked page returned with HTTP 200, carrying no result links and no
  genuine no-results marker
- **WHEN** `web_search` runs
- **THEN** the error names the search endpoint and says the endpoint blocked or rate-limited the
  search, and the result is not the no-results text

#### Scenario: Bad arguments are refused with the accepted set

- **GIVEN** a `web_search` call with an unknown argument or an empty query
- **WHEN** the tool validates it
- **THEN** it returns an error naming the accepted arguments or the empty query, and sends no
  request

## ADDED Requirements

### Requirement: Web search backends and their credentials

The local `web_search` SHALL have a backend seam. DuckDuckGo's HTML endpoint SHALL be the backend of
a session that configures none; SearXNG, Brave and Tavily SHALL be selectable by the `tools.webSearch`
setting. Each backend SHALL define how one query becomes one request and how one answer becomes
ranked results, so the tool's argument contract, approval tier, count cap and byte cap are the same
for every backend.

The SearXNG backend SHALL send the query to the configured instance's `/search` path, SHALL add no
credential unless one is configured, and SHALL carry the resolved credential as an
`Authorization: Bearer` header when one is. The Brave backend SHALL send a `GET` to its web-search
API with the query and the requested result count, and SHALL carry the resolved credential in the
`X-Subscription-Token` header. The Tavily backend SHALL send a `POST` whose JSON body carries the
query and the requested result count, and SHALL carry the resolved credential as an
`Authorization: Bearer` header.

A backend's credential SHALL be resolved from its configured reference when the call is sent, from
the same sources provider credentials resolve from — the auth store for `auth:<name>`, the
environment for `env:<VARIABLE>` — and a reference that does not resolve SHALL produce an error
result naming the setting, the reference and what to supply, without sending a request. A `helper:`
reference SHALL be accepted by the setting and refused when the call is sent, because this host
cannot resolve one, exactly as a provider header behaves.

#### Scenario: DuckDuckGo stays the zero-config default

- **GIVEN** a session with no `tools.webSearch` section
- **WHEN** the model calls `web_search`
- **THEN** the request goes to DuckDuckGo's HTML endpoint, carries no credential header, and the
  tool's description names DuckDuckGo

#### Scenario: SearXNG is selected by its instance URL

- **GIVEN** `tools.webSearch` selecting `searxng` with a URL
- **WHEN** the model calls `web_search` with a query
- **THEN** the request is a `GET` to that instance's `/search` path with the escaped query, and it
  carries no `Authorization` header

#### Scenario: A configured SearXNG credential is a bearer token

- **GIVEN** `tools.webSearch` selecting `searxng` with a URL and a resolvable credential reference
- **WHEN** the model calls `web_search`
- **THEN** the request carries `Authorization: Bearer <resolved value>` and nothing else the
  credential could leak into

#### Scenario: Brave carries its key in its own header

- **GIVEN** `tools.webSearch` selecting `brave` with a resolvable credential reference and a
  recorded Brave answer
- **WHEN** the model calls `web_search` with a query and a result count
- **THEN** the request names the query and the count, carries `X-Subscription-Token` with the
  resolved value, and the result lists the recorded ranked results

#### Scenario: Tavily carries its key as a bearer token over a POST

- **GIVEN** `tools.webSearch` selecting `tavily` with a resolvable credential reference and a
  recorded Tavily answer
- **WHEN** the model calls `web_search` with a query and a result count
- **THEN** the request is a `POST` whose JSON body carries the query and the count, carries
  `Authorization: Bearer` with the resolved value, and the result lists the recorded ranked results

#### Scenario: An unset credential reference sends no request

- **GIVEN** `tools.webSearch` selecting `brave` with `env:BRAVE_SEARCH_KEY` set to nothing
- **WHEN** the model calls `web_search`
- **THEN** the error result names `tools.webSearch.credential`, the reference and what to supply, and
  no request is sent

#### Scenario: A helper reference is refused when the call is sent

- **GIVEN** `tools.webSearch` selecting `tavily` with a `helper:` credential reference
- **WHEN** the model calls `web_search`
- **THEN** the error result says this host cannot resolve a helper reference and names the
  alternatives, and no request is sent

#### Scenario: A backend's blocked answer names its own endpoint

- **GIVEN** `tools.webSearch` selecting an API backend whose recorded answer is a non-200 response
- **WHEN** the model calls `web_search`
- **THEN** the error names that API's endpoint and status and says the endpoint blocked or
  rate-limited the search
