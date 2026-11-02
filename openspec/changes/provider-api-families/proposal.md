# Provider model: one base URL per API family, a family on every model, headers that are actually sent

## Problem

Today a provider entry is one kind, **one** base URL, one credential, and headers that are stored
but never applied. That cannot express a provider like OpenCode Go, whose 28 models are served by
**four different native APIs** behind three base URLs:

- `chat-completions` (17 models) — `https://opencode.ai/inference/go/openai/v1`
- `responses` (5) — the same `openai/v1` base, a different wire API
- `messages` (5) — `https://opencode.ai/inference/go/anthropic/v1`
- `generate-content` (1: Gemini 4 Argon) — `https://opencode.ai/inference/go/google/v1beta`

The metadata comes from `GET https://opencode.ai/inference/go/v1/models`, which returns each model's
`api` object with its family, `base_url` and capabilities. The current model cannot represent that:
`ProviderEntry` has one `BaseUrl` and a `Kind` (`ProviderEntry.cs:9`), the adapter is chosen from the
kind (`ChatClientFactory.cs:115-129`), and `ProviderAdapter` knows only OpenAI, Anthropic and
OpenAI-compatible (`ProviderAdapter.cs`). Custom providers are hard-wired to the OpenAI-compatible
shape (`CustomProvider.cs:10`, `AuthCommand.Login.cs:170-181`).

Two further gaps make the failures visible:

- **Headers are dead data.** `ProviderEntry.Headers` is parsed and stored (`ProviderEntry.cs:54-77`,
  `ProviderStore.cs:38`) and no request path reads it. The session header OpenCode Go asks for cannot
  be sent today at all.
- **No client identifies itself.** Nothing sets `User-Agent` on provider requests; only the update
  check does (`UpdateCheck.cs:176`).

## What changes

One contract change, four parts:

1. **Provider entry v2** — credential plus one base URL per API family it offers (`openai-chat`,
   `openai-responses`, `anthropic-messages`, `google`, extensible), plus an optional `modelsUrl`.
   A single `baseUrl` stays accepted as the shorthand for the default family, so no existing file
   needs rewriting.
2. **Family on the model** — resolved from the profile override, then discovery, then the catalog,
   then the provider's only family; two silent families are an error, not a guess. The family picks
   the adapter; the provider kind no longer does.
3. **Headers that are sent** — static values and templated values from a fixed set
   (`{session.id}`, `{run.id}`, `{lunate.version}`), validated at load (unknown family or placeholder
   is refused by name), applied by one HTTP pipeline to chat, responses, messages, Google and
   discovery. `User-Agent: lunate/<version>` is always set, for every provider.
4. **Presets and custom providers** — presets gain per-family URLs, a models URL and headers;
   OpenCode Go is the first complete one. `auth login --custom` gains an API-family answer so an
   Anthropic-compatible or OpenAI-Responses endpoint can be configured, not only chat.

The wizard (#475) and the OpenCode Go bug (#473) both build on this; neither is in this change.

## Impact

- Affected specs: `provider-store` (user-scoped entries), `ai-layer` (presets and routing; a new
  provider-families requirement).
- ADR: `adr/0029-provider-api-families.md`.
- Affected code: `ProviderEntry`, `ProviderPreset(s)`, `ProviderAdapter`, `ModelInfo` / catalog,
  `ChatClientFactory`, `CustomProvider`, `AuthCommand.Login`, `ProviderStore`, `DiscoverCommand`.
- New code: a Google `generateContent` chat client (no new package — the Google SDK is not in the
  build and a new NuGet needs approval).
- No new dependency; settings stay user-scope only.
