## 1. Tests first

- [ ] 1.1 Add `tests/Lunate.Coding.Tests/Web/StubHttpHandler.cs`: a handler that records requests and returns queued responses, redirects, transport exceptions or a response that ignores cancellation.
- [ ] 1.2 Add `HtmlToTextTests`: the title, visible paragraphs, list items, skipped script/style bodies, absolute markdown links from relative targets, entity decoding, and malformed markup without an exception.
- [ ] 1.3 Add `WebSearchToolTests`: ranked title/URL/snippet output, the default and maximum result caps, the 8 KiB output cap with its marker, a no-results result, unknown arguments, and an HTTP failure.
- [ ] 1.4 Add `WebFetchToolTests`: title and readable text, relative links resolved to absolute markdown targets, the 20 KiB output cap and 2 MiB source cap, a `text/plain` response, a non-text content type, invalid URL and scheme refusals, a non-2xx status naming the URL and reason, a redirect loop, a timeout and a transport failure.
- [ ] 1.5 Add `WebToolApprovalTests`: both tools declare `Execute` risk, print mode with `ask` or `auto-edit` declines the call before any request and returns the `Denied:` instruction, and `--yolo` allows it.
- [ ] 1.6 Update `OptionalToolRegistrationTests` for the six-tool core set, a `--tools` filter keeping a web tool, and an optional entry naming a web tool being refused; add the two web names to the approver evidence that `Execute` is never allowed by a policy.
- [ ] 1.7 Run the new and edited test classes and confirm the new cases fail before implementation.

## 2. Implementation

- [ ] 2.1 Add `src/Lunate.Coding/Web/HtmlToText.cs`: a bounded, quote-aware HTML scanner that returns the title and readable text, skips script/style/noscript/svg/template, renders block structure, lists and headings, decodes entities and renders http(s) anchors as markdown resolved against the page URL.
- [ ] 2.2 Add `src/Lunate.Coding/Web/WebSearchTool.cs`: `web_search` with a required query, `max_results` 5 default / 10 cap, DuckDuckGo HTML parsing, `uddg` unwrapping, an 8 KiB UTF-8-safe output cap, hand-written schema and `Execute` risk.
- [ ] 2.3 Add `src/Lunate.Coding/Web/WebFetchTool.cs`: `web_fetch` with http/https validation, manual redirects (10-hop and visited-URL limits), a 20 s timeout, a 2 MiB source read, a 20 KiB UTF-8-safe text cap, page title, markdown links and the named failure messages, hand-written schema and `Execute` risk.
- [ ] 2.4 Register both in `HarnessFactory.CreateTools` after `bash`, add them to `OptionalTools.CoreNames`, and keep the optional list unable to shadow them.
- [ ] 2.5 Run the focused test classes again until green.

## 3. Documentation and release note

- [ ] 3.1 Name the web tools and their approval behaviour in `docs/usage.md`, `docs/how-lunate-works.md`, `docs/security.md` and `docs/print.md`.
- [ ] 3.2 Add the release-note fragment `changes/<pull-request-number>.md` under `### Added`.
- [ ] 3.3 Run `dotnet csharpier format .`, the focused Release builds and tests, and `openspec validate --all --strict`.

## 4. Finish

- [ ] 4.1 Archive as the last commit: `openspec archive add-web-tools --yes`.
