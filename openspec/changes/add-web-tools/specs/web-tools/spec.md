## Purpose

Two core tools give a plain install approved web access: `web_search` returns ranked title, URL and
snippet results, and `web_fetch` returns a page's readable text with its title and markdown links.
Both are bounded in bytes, both name the URL and the reason when they fail, and both use the same
`Execute` approval path as `bash`.

## ADDED Requirements

### Requirement: Web search

`web_search` SHALL accept a required non-empty `query` string and an optional `max_results`
integer (default 5, at least 1, at most 10), SHALL send one unauthenticated HTTP request to the
search endpoint, and SHALL return the ranked results in rank order with a title, an absolute URL and
a snippet for each result. The result SHALL name the query, SHALL number the results, and SHALL be
bounded both in result count (`max_results`) and in total UTF-8 output bytes (8 KiB) with a marker
stating the cap; a cut point SHALL NOT split a UTF-8 sequence or a UTF-16 surrogate pair. A search
with no matches SHALL return a readable "no results" result rather than an error. An unknown
argument SHALL be refused with an error naming the accepted arguments, and an HTTP failure SHALL
produce an error result naming the endpoint and the reason.

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

- **GIVEN** a stub search response with no result links
- **WHEN** `web_search` runs
- **THEN** it returns a non-error result saying no results were found for the query

#### Scenario: Bad arguments are refused with the accepted set

- **GIVEN** a `web_search` call with an unknown argument or an empty query
- **WHEN** the tool validates it
- **THEN** it returns an error naming the accepted arguments or the empty query, and sends no
  request

### Requirement: Web fetch

`web_fetch` SHALL accept a required absolute `url` string whose scheme is `http` or `https`, SHALL
follow at most 10 HTTP redirects, and SHALL return readable text carrying the page title, with HTML
reduced to text, block structure preserved as line breaks and every http(s) link preserved as
markdown (`[text](absolute-url)`). Relative link targets SHALL be resolved against the final
response URL. The returned text SHALL be capped at 20 KiB of UTF-8 output with a marker stating the
cap, and the response body read SHALL be capped at 2 MiB; cut points SHALL NOT split a UTF-8
sequence or a UTF-16 surrogate pair. A `text/plain` response SHALL be returned as its decoded text;
a non-text response SHALL produce an error naming the URL and the content type. A URL with an
unsupported scheme, a missing host or invalid syntax SHALL produce an error naming the URL and what
to pass instead.

#### Scenario: A page returns its title and readable text

- **GIVEN** a stub HTML response with a `<title>`, headings, paragraphs and a link
- **WHEN** `web_fetch` runs for its URL
- **THEN** the result carries the page title, the text without tags, and the link rendered as
  markdown with its absolute target

#### Scenario: Relative links become absolute

- **GIVEN** an HTML response whose link target is `/guide`
- **WHEN** `web_fetch` renders the text
- **THEN** the markdown target is the final URL's scheme and host plus `/guide`

#### Scenario: Output and source are capped

- **GIVEN** an HTML response larger than the text cap whose rendered text exceeds it
- **WHEN** `web_fetch` renders the result
- **THEN** the returned text stays within 20 KiB, carries a marker naming the cap, and splits no
  UTF-8 sequence or surrogate pair

#### Scenario: Script and style content is not text

- **GIVEN** HTML with `<script>` and `<style>` bodies and visible paragraphs
- **WHEN** `web_fetch` reduces it to text
- **THEN** the script and style bodies are absent and the paragraphs remain

#### Scenario: Only http and https are fetched

- **GIVEN** a `web_fetch` call for a `file:` or `ftp:` URL, or for a string that is not a URL
- **WHEN** the tool validates it
- **THEN** it returns an error naming the URL and the accepted schemes, and sends no request

### Requirement: Web fetch failures name the URL and the reason

A non-2xx response SHALL produce an error result naming the URL, the HTTP status code and the
reason phrase. A redirect that targets an already visited URL, or a chain longer than 10 redirects,
SHALL produce an error result naming the URL and stating that it is a redirect loop. A request that
exceeds the fetch timeout (20 s) SHALL produce an error result naming the URL and the timeout in
culture-invariant form. A transport failure SHALL produce an error result naming the URL and the
underlying reason, with the next step. Each error message SHALL tell the model to correct the URL
or try another page.

#### Scenario: A non-2xx response names the status

- **GIVEN** a stub response with status 404 and reason phrase `Not Found`
- **WHEN** `web_fetch` runs for its URL
- **THEN** the error result carries the URL, `404` and `Not Found`

#### Scenario: A redirect loop is detected

- **GIVEN** a stub transport that redirects every request back to the same URL
- **WHEN** `web_fetch` runs
- **THEN** the error result carries the URL and says it is a redirect loop, and the tool stops
  following

#### Scenario: A timeout names the URL and the duration

- **GIVEN** a stub transport that never answers within the configured short timeout
- **WHEN** `web_fetch` runs
- **THEN** the error result carries the URL and the timeout duration, and is not an unhandled
  exception

#### Scenario: A transport failure names the reason

- **GIVEN** a stub transport that throws an HTTP request exception
- **WHEN** `web_fetch` runs
- **THEN** the error result carries the URL, the underlying reason and the next step

### Requirement: Web tool approval

`web_search` and `web_fetch` SHALL each declare `ToolRisk.Execute`, so the approval path is the same
as `bash`: the interactive panel asks once per call (unless the session grant covers the tool), ACP
asks its client, and an unattended run declines the call unless the run explicitly allows
everything. A declined call SHALL produce an instructing error result the model can read, and the
tool SHALL NOT issue a request in that case.

#### Scenario: Unattended runs decline the web tools

- **GIVEN** print mode with the default `ask` policy, or `auto-edit`
- **WHEN** the model calls `web_search` or `web_fetch`
- **THEN** the call is declined before execution and the result tells the model the tool was not run

#### Scenario: An explicit yolo run allows the web tools

- **GIVEN** a run with the explicit `--yolo` flag
- **WHEN** the model calls a web tool
- **THEN** the approval path allows the call and the tool can run

#### Scenario: The interactive panel asks per call

- **GIVEN** an interactive session with no session grant for `web_fetch`
- **WHEN** the model calls `web_fetch`
- **THEN** the approval request names `web_fetch` and its URL before the request is sent
