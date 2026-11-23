## MODIFIED Requirements

### Requirement: Web search

`web_search` SHALL accept a required non-empty `query` string and an optional `max_results`
integer (default 5, at least 1, at most 10), SHALL send one unauthenticated HTTP request to the
search endpoint, and SHALL return the ranked results in rank order with a title, an absolute URL and
a snippet for each result. The result SHALL name the query, SHALL number the results, and SHALL be
bounded both in result count (`max_results`) and in total UTF-8 output bytes (8 KiB) with a marker
stating the cap; a cut point SHALL NOT split a UTF-8 sequence or a UTF-16 surrogate pair. A search
with no matches SHALL return a readable "no results" result rather than an error only when the
response carries a genuine no-results marker; a 200 response with no result links and no such
marker SHALL produce an error saying the endpoint blocked or rate-limited the search. A non-200
response SHALL produce an error naming the endpoint, the HTTP status and the reason and saying the
endpoint blocked or rate-limited the search. An unknown argument SHALL be refused with an error
naming the accepted arguments.

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
