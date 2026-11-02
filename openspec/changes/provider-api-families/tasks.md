# Tasks

Slices are applied in this order; each is its own PR with its own tests.

## Slice 1 — model and presets

- [ ] `ProviderApiFamily` enum + parsing; `ProviderEntry.BaseUrls` map, `ModelsUrl`, `baseUrl`
      shorthand; unknown family refused by name.
- [ ] `ProviderPreset(s)` + embedded `providers.json` schema 2: per-family URLs, `modelsUrl`,
      headers; `opencode-go` complete (four URLs, discovery URL, session header).
- [ ] `ModelInfo.ApiFamily` + catalog `apiFamily`; annotate built-ins so no model is ambiguous.
- [ ] Family resolution (profile → discovery → catalog → single provider family) and the ambiguity
      error naming the families.
- [ ] `ChatClientFactory` selects by family; `ProviderAdapter` grows `OpenAiResponses` and `Google`.
- [ ] Project-scope refusal widened to `baseUrls`, `modelsUrl`, `headers`, `credentialPlacement`.
- [ ] Tests: loader, routing matrix, project scope.

## Slice 2 — headers and credential placement

- [ ] Header model: literal or template over `{session.id}`, `{run.id}`, `{lunate.version}`; validate
      name, value, placeholders at load; keep `auth:`/`env:`/`helper:` references.
- [ ] `ProviderHeadersHandler`: `User-Agent: lunate/<version>` on every request, provider headers,
      template substitution, missing session id fails loudly.
- [ ] `ProviderCredentialHandler`: bearer (default), `x-api-key`, named header, query parameter;
      removes SDK-added credential headers first; sentinel-key support for SDKs.
- [ ] `RequestContext(SessionId, RunId)` through `IChatClientFactory.Create`; session and child
      session pass their own id.
- [ ] `CredentialRedactor` learns header values and the placement.
- [ ] Tests: every placement, template cannot reference the credential, SDK header removed,
      parent/child session ids, discovery carries the same headers.

## Slice 3 — OpenAI Responses family

- [ ] `openai-responses` adapter via `GetResponsesClient().AsIChatClient()` (M.E.AI 10.10.1).
- [ ] Fixture: streamed text, tool call, tool result; live smoke skipped without a key.

## Slice 4 — Anthropic-compatible custom providers

- [ ] `CustomProvider` carries a family; `auth login --custom --api-family`,
      `--credential-placement`, `--models-url`; one settings writer for the wizard.
- [ ] Tests: writes `baseUrls` for the chosen family; refuses an unknown family; bearer vs `x-api-key`.

## Slice 5 — Google family

- [ ] Add `Google.GenAI` 1.28.0 to `Directory.Packages.props` (approved); record it in ADR-0029.
- [ ] `google` adapter from `Client(...).AsIChatClient(model)` with `HttpOptions.BaseUrl` and the
      headers/credential handlers via `ClientOptions.HttpClientFactory`.
- [ ] Acceptance tests: streaming; function calling incl. parallel calls; system instructions; image
      input gated by capability; thinking with thought signature preserved across a tool turn,
      **compaction** and a **session reload**; prompt caching where supported.
- [ ] Live smoke: Gemini through Google directly and through OpenCode Go's Google endpoint, skipped
      without a key.

## Cross-cutting

- [ ] Discovery client reads per-model `api` metadata: family, `base_url`, capabilities, context/output
      limits, reasoning efforts; custom providers constrained to their configured host.
- [ ] Docs: `docs/configuration.md` (entry v2, families, headers, placements, migration),
      `docs/guide.md` (`auth login --custom`), ADR-0029 accepted.
- [ ] `verify.sh` green locally and the PR CI on every slice.
