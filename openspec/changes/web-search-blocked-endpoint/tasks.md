## 1. Fixtures and tests

- [x] 1.1 Commit `search-blocked.html`, a recorded DuckDuckGo challenge page with no result anchors.
- [x] 1.2 Commit `search-no-results.html`, a recorded genuine DuckDuckGo no-results page.
- [x] 1.3 Add `WebSearchToolTests` cases: a non-200 blocked response, a 200 challenge page, and a
      genuine no-results page, all from the fixtures.
- [x] 1.4 Replace the inline "Nothing here" no-results test with the genuine no-results fixture.

## 2. Blocked-endpoint detection

- [x] 2.1 Return one blocked/rate-limited error for every non-200 response, naming the endpoint, the
      status and the reason.
- [x] 2.2 Treat a 200 page with no result anchors as the same error unless it carries a genuine
      no-results marker, keeping the current non-error text for that case.
- [x] 2.3 Keep the ranked-result, count-cap, byte-cap, argument and timeout behaviour unchanged.

## 3. Documentation and release note

- [x] 3.1 Update the `web_search` error row in `docs/dev/guide.md`.
- [x] 3.2 Add `changes/<pull-request-number>.md` with the release-note line.

## 4. Verification

- [x] 4.1 Run CSharpier, build the touched project and test project, and run the named test classes.
- [x] 4.2 Run `openspec validate web-search-blocked-endpoint --type change --strict`.
- [ ] 4.3 Sync specs and archive with `openspec archive web-search-blocked-endpoint --yes` as the
      last commit.
