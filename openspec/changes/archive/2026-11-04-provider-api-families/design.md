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
- `credentialPlacement` says **how the resolved credential is sent**: `{ "in": "header", "name":
  "Authorization", "scheme": "Bearer " }` (the default when omitted), `{ "in": "header", "name":
  "x-api-key" }`, `{ "in": "header", "name": "api-key" }` (Azure-style), or `{ "in": "query", "name":
  "key" }` (Google's native APIs). A provider whose API uses the SDK's own convention can omit it.
  The credential value is never available as a template variable.

### Validation at load

- Header names: RFC 7230 token, no control characters, no `:`.
- Header values: no CR/LF, printable ASCII, max 8 KiB. A value containing `{` that is not one of the
  three known placeholders is refused with the placeholder named.
- No environment access in templates. A template is text substitution over the three variables and
  nothing else.
- `credentialPlacement`: `in` is one of `header` or `query`; the name is an RFC 7230 token (header)
  or a URL-safe query name; a header `scheme` is printable ASCII without CR/LF and ends in a space if
  non-empty (`"Bearer "`); an unknown `in`, an empty name or a scheme containing a control character
  is refused at load, naming the field. One placement per provider.
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
| `google` | `Google.GenAI` SDK's `Microsoft.Extensions.AI.GoogleGenAIChatClient` (`client.AsIChatClient(model)`) |

`ProviderAdapter` grows `OpenAiResponses` and `Google`; the preset kind still names a provider, but
the adapter is selected from the **family**. The existing `openai` / `anthropic` natives keep their
adapter because their default family maps to it.

### The Google family

Use `Google.GenAI` 1.28.0 (approved; it depends on `Microsoft.Extensions.AI.Abstractions` and ships
`GoogleGenAIChatClient`). The SDK's `Client` takes a base URL and custom headers
(`Types.HttpOptions`), so the same client serves Google directly and OpenCode Go's Google endpoint;
`ClientOptions.HttpClientFactory` lets our header/credential handler sit in the transport.

Acceptance is the Gemini feature set, not just "a request returns":

- streaming (`GetStreamingResponseAsync`, SSE);
- function calling, including multiple calls per turn and function results back;
- system instructions (`SystemInstruction`), cache-stable prefix;
- **thinking with thought signatures preserved**: `TextReasoningContent.ProtectedData` carries the
  signature, the SDK re-attaches it to the next function call, and Lunate must keep that content
  through storage, compaction and reload — a signature dropped in the session file makes the next
  tool turn fail or silently degrade;
- prompt caching: implicit caching where the model supports it; explicit `CachedContent` only if the
  capability says so;
- image input via the provider-store capability checks (model `modalities.input`), refused by name
  for a model that does not accept images.

Fixtures cover each bullet; live smoke covers Gemini through Google directly and through OpenCode Go's
Google endpoint, skipped without a key.

**Risk to pin in tests:** the adapter stores the signature in `ProtectedData`, which is opaque to our
compaction. The compaction path must preserve it verbatim, and a reload test must prove a tool turn
after compaction still carries the signature.

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

### Credential placement

A second handler, `ProviderCredentialHandler`, owns the credential and is the only code that places
it: `Authorization: Bearer <key>` (default), `x-api-key: <key>`, a named header (`api-key` for Azure),
or a query parameter (`?key=` for Google's native APIs). It runs after the SDK has built the request
and **removes any credential header the SDK added for this provider** (`Authorization`, `x-api-key`,
`x-goog-api-key`) before applying the declared placement, so the key appears exactly once, in the
declared place.

SDKs that require a key at construction (Google.GenAI) receive a non-secret sentinel; the handler
substitutes the real value at send time. That keeps the secret out of SDK objects, logs and retries,
and makes "the key is never in a header template" checkable: the template resolver only knows
`{session.id}`, `{run.id}` and `{lunate.version}`, and a test asserts `{credential}`,
`{apiKey}` and `{key}` are refused by name.

Presets declare the placement: `anthropic` → `x-api-key`, `google` → query `key`, `opencode-go` and
the OpenAI-presets → bearer. A custom provider gets it from `auth login --custom
--credential-placement` (default bearer).

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
| `google` | `google` family URL, `credentialPlacement` query `key` | native API key placement |
| others | unchanged; audited against their docs before the PR | — |

Every preset's file records what it sets and the doc link in a comment, so the next audit is a diff,
not a research task.

## 7. Custom providers and `auth login --custom`

`--api-family <openai-chat|openai-responses|anthropic-messages|google>` (default `openai-chat`),
`--models-url`, and `--credential-placement <bearer|x-api-key|header:<name>|query:<name>>` (default
`bearer`). The family is required only when it cannot be inferred: it always can be from the flag or
the prompt, because a URL does not tell us its wire API. Writes `baseUrls` keyed by the family, one
entry. The wizard (#475) later reuses the same write path — no second writer.

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

- Loader: unknown family, unknown placeholder, CRLF in a header, bad models URL host, bad credential
  placement → named errors; `{credential}`, `{apiKey}`, `{key}` in a header are refused by name.
- Router: one model per family resolves to the expected client type and base URL; a two-family
  provider without a family fails naming both.
- Headers: a fake `HttpMessageHandler` asserts `User-Agent`, a literal header, `{session.id}` and
  `{lunate.version}` for chat, responses, messages, google and discovery.
- Credential placement: bearer, `x-api-key`, a named header and a query parameter each put the key in
  exactly one place; an SDK-native credential header is removed first; the key never appears in a
  template or a log line.
- Child session: header differs from the parent's (this is also #473's acceptance test).
- Project scope: a `providers` block in project settings is still refused.
- Google: fixture-recorded streaming, function calling (including parallel calls), system
  instructions, image input, and thinking with a thought signature that survives a tool turn,
  compaction and a session reload.
- Live smoke per family, skipped without a key (in #473, reusing this pipeline); Gemini live smoke
  runs through Google directly and through OpenCode Go's Google endpoint.
