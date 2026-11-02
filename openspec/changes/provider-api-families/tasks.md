# Tasks

## 1. Contract and loader

- [ ] `ProviderEntry`: `BaseUrls` map keyed by `ProviderApiFamily`, `ModelsUrl`; keep `baseUrl`
      shorthand; refuse unknown families by name.
- [ ] `ProviderApiFamily` enum + parsing/formatting; `ProviderAdapter` grows `OpenAiResponses` and
      `Google`.
- [ ] Header model: literal or template; validate name, value and placeholders at load; keep
      `auth:`/`env:`/`helper:` references working.
- [ ] `ProviderPreset` + embedded `providers.json` schema 2: per-family URLs, `modelsUrl`, headers;
      add `opencode-go`'s four URLs, `openrouter` attribution headers, `google` native URL.
- [ ] Project-scope refusal extended to the new fields (existing test widened, not replaced).

## 2. Resolution and adapters

- [ ] `ModelInfo.ApiFamily` + catalog `apiFamily`; annotate built-in entries for the presets.
- [ ] Family resolution (profile → discovery → catalog → single provider family) with the ambiguity
      error naming the families.
- [ ] `ChatClientFactory`: choose by family; OpenAI Responses via
      `GetResponsesClient().AsIChatClient()`; keep the chat path unchanged.
- [ ] Google `generateContent` chat client (messages, tools, SSE streaming) + recorded fixtures.

## 3. Headers and context

- [ ] `ProviderHeadersHandler` on every provider `HttpClient` including discovery; `User-Agent` always.
- [ ] `RequestContext(SessionId, RunId)` threaded through `IChatClientFactory.Create`; session and
      child-session call sites pass their own id.
- [ ] Missing `{session.id}` fails with an actionable error.
- [ ] `CredentialRedactor` learns header values so they never reach logs.

## 4. Custom providers

- [ ] `CustomProvider` carries a family; `auth login --custom --api-family` and `--models-url`;
      writes `baseUrls` for that family; prompt when the flag is absent.
- [ ] No second settings writer: the wizard (#475) will call the same path.

## 5. Discovery (hands the metadata to #473)

- [ ] Discovery client reads `api` per model: family, `base_url`, capabilities, context/output limits,
      reasoning effort list; produces catalog entries with family + endpoint.
- [ ] Custom-provider discovery rejects a `base_url` outside the configured host, naming the model.

## 6. Proof

- [ ] Loader tests: unknown family, unknown placeholder, CRLF, bad discovery host.
- [ ] Routing matrix: one model per family resolves to the expected client and URL; ambiguous fails.
- [ ] Header tests across all five request kinds (chat, responses, messages, google, discovery),
      including parent/child session ids.
- [ ] Google fixtures: tool call, streamed text, multi-turn.
- [ ] `verify.sh` green on macOS and the PR CI.

## 7. Docs

- [ ] `docs/configuration.md`: provider entry v2, families, headers, placeholders, migration.
- [ ] `docs/guide.md`: `auth login --custom --api-family`; what each preset sets.
- [ ] ADR-0029 accepted in `adr/`.

## 8. Sign-off gate

- [ ] Maintainer sign-off on the Google-client scope (implement in 0.3 vs defer to 0.4) before apply.
