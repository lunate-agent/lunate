# Design: provider API families

## 1. The settings shape

```jsonc
{
  "providers": {
    "opencode-go": {
      "kind": "opencode-go",
      "credentialReference": "auth:opencode-go",
      "baseUrls": {
        "openai-chat": "https://opencode.ai/inference/go/openai/v1",
        "openai-responses": "https://opencode.ai/inference/go/openai/v1",
        "anthropic-messages": "https://opencode.ai/inference/go/anthropic/v1",
        "google": "https://opencode.ai/inference/go/google/v1beta"
      },
      "modelsUrl": "https://opencode.ai/inference/go/v1/models",
      "headers": {
        "x-opencode-session": "{session.id}"
      }
    }
  }
}
```

- `baseUrls` is a map keyed by **API family**. The four families are the closed set for 0.3:
  `openai-chat`, `openai-responses`, `anthropic-messages`, `google`. An unknown key is a load error
  that names it and lists the supported set — the map is extensible by code, not by typo.
- Families may share a URL (the two OpenAI families do at OpenCode Go).
- `baseUrl` (string) stays accepted and means `baseUrls: { "<default family of the kind>": <value> }`
  — `openai-chat` for OpenAI-compatible kinds, `anthropic-messages` for `anthropic`. Reading never
  rewrites a user's file; writing new entries writes `baseUrls`.
- `modelsUrl` is optional. Absent means "discovery is not available for this provider"; the wizard
  and `auth login` then ask for a manual model id (#475 reuses this).
- `headers` maps a header name to a value that is **either a literal** (e.g. `"X-Title": "Lunate"`)
  or a template over a fixed variable set. Values are no longer credential references only; the
  existing `auth:`/`env:`/`helper:` forms stay valid for secrets, and a resolved secret is still
  never written to settings.

### Validation at load

- Header names: RFC 7230 token, no control characters, no `:`.
- Header values: no CR/LF, printable ASCII, max 8 KiB. A value containing `{` that is not one of the
  three known placeholders is refused with the placeholder named.
- No environment access in templates. A template is text substitution over the three variables and
  nothing else.
- `modelsUrl` and every family URL follow the existing endpoint rule: `https`, or `http` only for a
  loopback host. A `modelsUrl` on a different host than the family URLs is allowed only for a
  **preset** (first-party metadata); a custom provider's discovery URL must share the host of at
  least one of its family URLs, otherwise the credential could be sent to an arbitrary endpoint.

## 2. The family on the model

Precedence, highest first:

1. **Profile override** — `profiles.<name>.apiFamily` (new optional field). Explicit user intent wins.
2. **Discovery metadata** — for a discovered model, the `api` object's single key
   (`chat-completions` → `openai-chat`, `responses` → `openai-responses`,
   `messages` → `anthropic-messages`, `generate-content` → `google`). The `base_url` inside that
   object becomes the model's endpoint.
3. **Catalog** — `models.json` may carry `"apiFamily"`. Built-ins for the twelve presets are annotated
   once, at the same time as this change.
4. **Provider default** — when the provider offers exactly one family, that family. When it offers
   more than one and nothing above answered, the profile fails with an error naming the provider,
   the model and the families to choose from.

`ModelInfo.ApiFamily` is `string?` for compatibility; the resolver maps it to the enum and fails on an
unknown value by name.

## 3. Adapter per family, not per kind

| Family | Client |
|---|---|
| `openai-chat` | `OpenAIClient.GetChatClient(...).AsIChatClient()` (today's path) |
| `openai-responses` | `OpenAIClient.GetResponsesClient().AsIChatClient()` — verified present in `Microsoft.Extensions.AI.OpenAI` 10.10.1 (`OpenAIClientExtensions.AsIChatClient(ResponsesClient, string)`); **no new package** |
| `anthropic-messages` | existing Anthropic SDK adapter |
| `google` | new `GoogleGenerativeContentChatClient : IChatClient` over `HttpClient` (`:generateContent` and `:streamGenerateContent?alt=sse`) |

`ProviderAdapter` grows `OpenAiResponses` and `Google`; the preset kind still names a provider, but
the adapter is selected from the **family**. The existing `openai` / `anthropic` natives keep their
adapter because their default family maps to it.

### The Google client — the one real cost

Gemini 4 Argon is the only model on `google` today, but it is an agent model: tools, streaming and
multi-turn are required, not optional. The client is a translator in both directions (messages/tools →
`contents`/`functionDeclarations`; SSE chunks → `ChatResponseUpdate`), tested against recorded
fixtures. Estimate: ~600–900 lines with tests, and it is the largest single piece of this change.

**Recommendation:** implement it in 0.3, behind its own file and fixture set, because "use the
model's native API" is the whole point of the contract and a Gemini-via-OpenAI-shim would re-introduce
the single-API assumption. **If you prefer to cut it:** the `google` family stays defined and
validated, the preset keeps its OpenAI-compatible endpoint as a documented fallback, and native
support moves to 0.4 — say so and I will shape the change that way.

## 4. Headers pipeline

One `DelegatingHandler` (`ProviderHeadersHandler`) is installed on every `HttpClient` the AI layer
creates — OpenAI chat, OpenAI responses, Anthropic, Google, discovery — so there is exactly one place
that adds headers and it cannot be forgotten by a new adapter.

It resolves, per request:

- `User-Agent: lunate/<informational version>` always, overriding any SDK default.
- The provider's `headers`, in declaration order, each value substituted from `RequestContext`.
- Existing protocol headers the SDK sets (`anthropic-version`, `Authorization`) are untouched unless
  the provider declares the same name, in which case the provider wins.

`RequestContext` is `(SessionId, RunId)`; `{lunate.version}` comes from the assembly. Resolution
happens at request time, not client construction, so a client can outlive a run.

## 5. Where the session id comes from

`IChatClientFactory.Create` gains an optional `RequestContext`. The interactive session passes its
session id and run id; a subagent host passes the **child's** id — this is the rule #473's test pins.
`LUNATE_REPLAY` is unaffected.

A provider whose headers use `{session.id}` and a call with no session id **fails with an actionable
error** rather than sending an empty header (print mode will pass the session it creates; replay
fixtures do not need the header).

## 6. Presets and the audit

`providers.json` schema version becomes 2. Each preset keeps its default for compatibility and adds
what its provider documents:

| Preset | What it sets | Why |
|---|---|---|
| `opencode-go` | four family URLs, `modelsUrl`, `x-opencode-session` | native API per model; session routing |
| `openrouter` | `HTTP-Referer`, `X-Title: Lunate` | attribution headers the provider documents |
| `anthropic` | family `anthropic-messages`; SDK keeps `anthropic-version` | protocol version |
| `google` | `google` family URL, `x-goog-api-key` credential in the SDK's place | native API |
| others | unchanged; audited against their docs before the PR | — |

Every preset's file records what it sets and the doc link in a comment, so the next audit is a diff,
not a research task.

## 7. Custom providers and `auth login --custom`

`--api-family <openai-chat|openai-responses|anthropic-messages|google>` (default `openai-chat`),
plus `--models-url`. The family is required only when it cannot be inferred: it always can be from
the flag or the prompt, because a URL does not tell us its wire API. Writes `baseUrls` keyed by the
family, one entry. The wizard (#475) later reuses the same write path — no second writer.

## 8. Security

- Providers stay **user scope only**; project settings still cannot define a provider, an endpoint or
  a credential reference (existing test stays, extended with `baseUrls`, `modelsUrl`, `headers`).
- A project file may still reference a provider by name in a profile; it cannot alter its URLs or
  headers.
- Discovery-supplied `base_url` is accepted for presets; for custom providers it must share the
  configured host, or the model is dropped with a warning naming it.
- Header values are never logged; `CredentialRedactor` learns the header names it must redact.

## 9. Compatibility and migration

- Existing settings load unchanged (`baseUrl` shorthand).
- Models without `apiFamily` keep working: their preset's single family is the default; the
  two-family OpenCode Go requires discovery or a profile override, and its built-in catalog entries
  are annotated with the family, so nothing is ambiguous on first run.
- No schema version bump on `settings.json`; the provider file itself carries `schemaVersion` (2).

## 10. Tests (the gate)

- Loader: unknown family, unknown placeholder, CRLF in a header, bad models URL host → named errors.
- Router: one model per family resolves to the expected client type and base URL; a two-family
  provider without a family fails naming both.
- Headers: a fake `HttpMessageHandler` asserts `User-Agent`, a literal header, `{session.id}` and
  `{lunate.version}` for chat, responses, messages, google and discovery.
- Child session: header differs from the parent's (this is also #473's acceptance test).
- Project scope: a `providers` block in project settings is still refused.
- Google: fixture-recorded generateContent and SSE stream with a tool call.
- Live smoke per family, skipped without a key (in #473, reusing this pipeline).
