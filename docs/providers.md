# Providers, presets and the model catalog

This page answers what a provider entry is, which presets ship and how a model id resolves to one, and
the short answer is: a preset or a named `providers.json` entry plus a credential reference, with the
endpoint picked by the model's API family and the model's facts coming from the catalog.

## The twelve presets

Each preset is a template: an adapter, one base URL per API family and one credential variable.
`lunate auth login <preset>` and the wizard store a key for it; a `providers.json` entry selects the
preset with `kind` and may override its endpoints.

| Preset id | Name | Adapter | Declared families | Credential variable |
| --- | --- | --- | --- | --- |
| `openai` | OpenAI | `openai` | `openai-chat` | `OPENAI_API_KEY` |
| `anthropic` | Anthropic | `anthropic` | `anthropic-messages` | `ANTHROPIC_API_KEY` |
| `openrouter` | OpenRouter | `openai-compatible` | `openai-chat`, `systemone` | `OPENROUTER_API_KEY` |
| `deepseek` | DeepSeek | `openai-compatible` | `openai-chat` | `DEEPSEEK_API_KEY` |
| `moonshotai` | Moonshot AI | `openai-compatible` | `openai-chat` | `MOONSHOT_API_KEY` |
| `zai` | Z.AI | `openai-compatible` | `openai-chat` | `ZHIPU_API_KEY` |
| `opencode` | OpenCode | `openai-compatible` | `openai-chat`, `systemone` | `OPENCODE_API_KEY` |
| `opencode-go` | OpenCode Go | `openai-compatible` | `openai-chat`, `openai-responses`, `anthropic-messages`, `google`, `systemone` | `OPENCODE_API_KEY`, alias `OPENCODE_GO_API_KEY` |
| `groq` | Groq | `openai-compatible` | `openai-chat` | `GROQ_API_KEY` |
| `togetherai` | Together AI | `openai-compatible` | `openai-chat` | `TOGETHER_API_KEY` |
| `xai` | xAI | `openai-compatible` | `openai-chat` | `XAI_API_KEY` |
| `google` | Google | `openai-compatible` | `openai-chat`, `google` | `GOOGLE_API_KEY`, alias `GEMINI_API_KEY` |

The preset's own table is [`providers.json`](../src/Lunate.Ai/providers.json): base URLs, headers and
per-family credential placement live there, and it is the file the wizard and `lunate auth` read.
`systemone` is the decision-only wire: a chat profile that names it is refused, and
[Decisions](decisions.md) owns the profiles that use it.

## The five API families

A model is served over one of five families, and the family — not the provider name or its kind —
selects the wire behaviour:

| Family | What it speaks |
| --- | --- |
| `openai-chat` | OpenAI-compatible Chat Completions |
| `openai-responses` | the OpenAI Responses API |
| `anthropic-messages` | the Anthropic Messages API, with the same prompt-cache breakpoints and turn merge as the native preset |
| `google` | the native Gemini API |
| `systemone` | the decision-only OpenRouter/OpenCode System One wire |

A model's family comes from a profile's `apiFamily` override, then provider discovery, then the
catalog, then the provider's only family. Discovery's own family keys map onto these names:
`chat-completions` to `openai-chat`, `responses` to `openai-responses`, `messages` to
`anthropic-messages`, and `generate-content` to `google`. Discovery also records input/output
modalities, tool and reasoning capabilities, reasoning efforts, context limits and output limits.

## Custom provider entries

The `providers.<id>` settings block describes one user-owned custom endpoint and serves exactly one
API family. New entries use `baseUrls` keyed by family; the accepted names are `openai-chat`,
`openai-responses`, `anthropic-messages` and `google`. The older `baseUrl` spelling remains readable
as the `openai-chat` family and loading it does not rewrite the file. New writes use `baseUrls`.

<!-- config: settings -->
```json settings
{
  "schemaVersion": 2,
  "providers": {
    "my-gemini": {
      "baseUrls": { "google": "https://gemini.example/v1beta" },
      "modelsUrl": "https://gemini.example/v1beta/models",
      "credentialPlacement": "query:key",
      "model": "gemini-example"
    }
  },
  "profiles": {
    "chat": { "provider": "my-gemini", "model": "my-gemini/gemini-example" }
  }
}
```

Endpoint precedence follows the same intent: a provider entry's declared `baseUrls` URL for the
resolved family (or its `baseUrl` for the default family) wins, otherwise the model's discovered
`endpoint` is kept, and only then does the preset's family URL apply. A custom provider's discovered
endpoint and redirects must stay on its configured host, scheme and port; a preset may use its
first-party per-family endpoints. See [ADR-0029](../adr/0029-provider-api-families.md) for the
accepted provider-family decision.

`~/.lunate/providers.json` holds named instances: a name, the preset `kind` it uses as a template, one
base URL per family, and a `credentialReference`. Credentials are references (`auth:` or `env:`),
never literal secrets, and header values may be literals or references too:

<!-- config: providers -->
```json providers
{
  "schemaVersion": 1,
  "providers": [
    {
      "name": "work-anthropic",
      "kind": "anthropic",
      "baseUrl": "https://api.anthropic.com",
      "credentialReference": "auth:work-anthropic",
      "region": "eu"
    }
  ]
}
```

Headers may be literal values or templates over `{session.id}`, `{run.id}` and `{lunate.version}`. A
run without a session (`--no-session`) still resolves `{session.id}` to an ephemeral per-process
conversation id, so a header-templated provider works fileless; a call that carries no run context at
all still fails instead of sending an empty value. The credential is separate: `credentialPlacement`
selects the default bearer header, a named header such as `x-api-key`, or a query parameter such as
Google's `key`. Templates cannot read credentials; invalid placeholders and placements are rejected
when the provider loads. Placements are `bearer`, `x-api-key`, `header:<name>`,
`header:<name>;scheme=<scheme>` and `query:<name>`.

`region` (`eu`, `us`, or `local`) is descriptive metadata only: it does not choose an endpoint or
guarantee data residency.

Environment credentials continue to take precedence over stored keys, including in CI, and
the reachability `lunate auth list` reports is a bounded unauthenticated probe that sends neither
credentials nor optional headers.

On load, Lunate idempotently migrates legacy stored keys and custom endpoints into matching provider
entries. An explicit provider entry wins, and migration never copies the secret out of `auth.json`.
Project settings may refer to user-defined providers and profiles or lower user limits, but they
cannot define a provider, endpoint, credential reference or header secret, so a repository cannot
redirect a user's credentials to its own server.

## How a model id resolves

The catalog is the embedded `models.json` (schema 2: id, provider, qualified wire id, context and
max-output windows, tool and vision support, extensible API/modality/feature capability sets,
reasoning style and per-million-token pricing), merged in order with the managed cache
`~/.lunate/models.catalog.json` and the user file `~/.lunate/models.json`; the user file wins by id. A
schema-1 override keeps the embedded entry's pricing, maximum output, vision support and wire id,
because schema 1 cannot express them; a schema-2 override replaces the entry completely. Adding a
model never needs a code change.

Capability sets are open strings for APIs, input/output modalities and features, and the host checks
only existing-usage requirements: chat needs text and tools, decision needs structured output, and an
attached image is rejected before sending only when image input is known to be unsupported. Unknown
image support is attempted, and a provider rejection becomes a model-named
switch-profile-or-remove-image notice.

Nothing refreshes at startup or in the background. `lunate --update-catalog` fetches
<https://models.dev/api.json>, maps it through the one catalog mapper and atomically writes the
managed cache; `--output` writes the snapshot elsewhere and `--source <path-or-url>` reads a local
snapshot, so an offline or malformed fetch leaves the old cache untouched. `lunate --discover
<name-or-url>` drafts a `models.json` entry for an endpoint the catalog does not know, and
`scripts/update-models.sh` regenerates the embedded snapshot and supports `--check`.

## Related

- [Models and providers](models-and-providers.md) — choosing, signing in, profiles and your own
  catalog entries
- [Decisions](decisions.md) — the profiles that use the `systemone` family
- [Configuration](configuration.md) — the settings file a provider entry sits in
- [Command and option reference](cli.md) — `--update-catalog`, `--discover` and `auth`
- [Environment variables](environment-variables.md) — every credential variable
- [Settings reference](settings.md) — the keys with their types and defaults
