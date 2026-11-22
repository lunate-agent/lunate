## Why

A plain Lunate install has no way to look up a library's current API, a changelog or an error
message: the core tool set stops at the workspace, and web access is reachable only through an MCP
server. The fallback, `bash` plus `curl`, is unapproved for its real effect, unstructured and
untrusted. Two core tools — an approved search and an approved fetch — make the web reachable and
keep the same approval boundary as a command.

## What Changes

- Add `web_search` to `Lunate.Coding`: a query returns ranked results with a title, URL and
  snippet per result, capped in count and total bytes.
- Add `web_fetch` to `Lunate.Coding`: a URL returns readable text with the page title, HTML reduced
  to text and links preserved as markdown, capped in bytes; a non-2xx response, a redirect loop or
  a timeout names the URL and the reason.
- Register both beside `read`, `write`, `edit` and `bash` as core tools, so `--tools` can select
  them and every frontend gets them without a settings key or an MCP server.
- Give both `ToolRisk.Execute`, the same risk as `bash`: the interactive panel asks once per call,
  ACP asks its client, and an unattended run declines them unless `--yolo` is set.
- No new NuGet package: the HTML-to-text step is a bounded in-repo converter, and the search
  transport is an HTTP request against an unauthenticated HTML endpoint.

## Capabilities

### New Capabilities

- `web-tools`: the `web_search` and `web_fetch` tools — their arguments, result shapes, byte and
  count caps, HTML-to-markdown reduction, URL validation, redirect handling and failure messages.

### Modified Capabilities

- `agent-tools`: the core tool set includes `web_search` and `web_fetch`, declared like the other
  core tools and selectable by the run's tool filter.

## Impact

- Code: new `src/Lunate.Coding/Web/` (both tools, the HTML-to-text converter and the shared HTTP
  bounds); `src/Lunate.Coding/Sessions/HarnessFactory.cs` registers them.
- Tests: new `tests/Lunate.Coding.Tests/Web/` with a stub `HttpMessageHandler`; the core-tool
  expectation in `OptionalToolRegistrationTests` and the approver evidence change with the set.
- Docs: `docs/usage.md`, `docs/how-lunate-works.md`, `docs/security.md` and `docs/print.md` name the
  web tools and their approval behaviour.
- Dependencies: none. No settings key, no public contract assembly change, no session-format change.
