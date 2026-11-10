# Models and providers

This page answers where a model comes from and how to point Lunate at a different one, and the short
answer is: pick one of twelve presets, or define a provider entry with its own endpoint, then name the
model in a profile. Credentials, profiles and the first-run wizard are covered where they change
behaviour — [Environment variables](environment-variables.md) owns the credential variables and
[First run](first-run.md) owns the wizard.

## Start from a preset

Twelve presets ship in [`providers.json`](../src/Lunate.Ai/providers.json) order, and the `/auth`
picker, `lunate auth login` and `lunate setup --provider` all offer them in that order:

| Preset id | Provider |
| --- | --- |
| `openai` | OpenAI |
| `anthropic` | Anthropic |
| `openrouter` | OpenRouter |
| `deepseek` | DeepSeek |
| `moonshotai` | Moonshot AI |
| `zai` | Z.AI |
| `opencode` | OpenCode |
| `opencode-go` | OpenCode Go |
| `groq` | Groq |
| `togetherai` | Together AI |
| `xai` | xAI |
| `google` | Google |

A preset declares its adapter, its base URL per API family, its credential placement and the
environment variable its key comes from. [Environment variables](environment-variables.md#supply-a-credential)
lists the variable for each one; a set variable always wins over a stored key.

## Describe an endpoint in settings

The `providers.<id>` settings block describes one user-owned custom endpoint and serves exactly one
API family. New entries use `baseUrls` keyed by family; the accepted names are `openai-chat`,
`openai-responses`, `anthropic-messages` and `google`. The older `baseUrl` spelling remains readable
as the `openai-chat` family and loading it does not rewrite the file. New writes use `baseUrls`. An
entry whose resolved family is `anthropic-messages` gets the same prompt-cache breakpoints and turn
merge as the native Anthropic preset; the resolved family selects the wire behavior, not the provider
name or its kind.

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

For built-in provider presets, each model's discovery `api` object selects its family and its
`base_url` supplies that model's endpoint. The discovery family keys map as follows:
`chat-completions` to `openai-chat`, `responses` to `openai-responses`, `messages` to
`anthropic-messages`, and `generate-content` to `google`. Discovery also records input/output
modalities, tool and reasoning capabilities, reasoning efforts, context limits, and output limits. A
profile's `apiFamily` override wins over discovery metadata and the catalog. Endpoint precedence
follows the same intent: a provider entry's declared `baseUrls` URL for the resolved family (or its
`baseUrl` for the default family) wins, otherwise the model's discovered `endpoint` is kept, and only
then does the preset's family URL apply. A custom provider's discovered endpoint and redirects must
stay on its configured host, scheme and port; a preset may use its first-party per-family endpoints.
See [ADR-0029](../adr/0029-provider-api-families.md) for the accepted provider-family decision.

Headers may be literal values or templates over `{session.id}`, `{run.id}`, and `{lunate.version}`. A
run without a session (`--no-session`) still resolves `{session.id}` to an ephemeral per-process
conversation id, so a header-templated provider works fileless; a call that carries no run context at
all still fails instead of sending an empty value. The credential is separate: `credentialPlacement`
selects the default bearer header, a named header such as `x-api-key`, or a query parameter such as
Google's `key`. Templates cannot read credentials; invalid placeholders and placements are rejected
when the provider loads.

Every key of that block — `baseUrl`, `baseUrls`, `catalogName`, `credentialPlacement`, `headers`,
`model`, `modelsUrl`, `region` — is in the generated [settings reference](configuration.md) with its
type, default and scope.

## Add a named provider entry

The twelve built-in provider kinds are templates for adapters and defaults. A user may define
multiple named provider entries of one kind in `~/.lunate/providers.json`:

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

`credentialReference` and optional header values are references, never secret values. Supported
sources are `auth:<key>` and `env:<VARIABLE>`. The credential itself stays in `auth.json` or the
environment. `region` (`eu`, `us`, or `local`) is descriptive metadata only: it does not choose an
endpoint or guarantee data residency.

On load, Lunate idempotently migrates legacy stored keys and custom endpoints into matching provider
entries. An explicit provider entry wins, and migration never copies the secret out of `auth.json`.
Environment credentials continue to take precedence over stored keys, including in CI. Reachability
shown by `auth list` is a bounded unauthenticated probe; it sends neither credentials nor optional
headers.

Project settings may refer to user-defined providers and profiles or lower user limits. They cannot
define a provider, endpoint, credential reference, or header secret. This prevents a repository from
redirecting a user's credentials to its own server.

## Point at a custom OpenAI-compatible endpoint

For Ollama, vLLM, LM Studio or any other OpenAI-compatible server, [First run](first-run.md#a-custom-openai-compatible-endpoint)
has the `lunate auth login --custom` flow and the flags a script passes instead. The entry it writes
is the `providers.<id>` block above: `baseUrls` keyed by family, `credentialPlacement`, an optional
`modelsUrl`, and the key in `auth.json` rather than in `providers.json`.

## Add a model to your own catalog

`~/.lunate/models.json` is your catalog of model entries; a user entry overrides a built-in one with
the same id, and adding a model never needs a code change. An entry carries `id`, `provider`
(`openai` for any OpenAI-compatible endpoint, `anthropic`), optional `endpoint`, `contextWindow`,
`supportsTools` and an `auth` reference naming a key in `lunate auth login`'s `auth.json`. An
optional `reasoning` declares the model's reasoning style:

- `effort` — OpenAI-compatible reasoning effort.
- `budget` — Anthropic extended thinking.
- `inline` — the model embeds thinking markers in its text stream.

Absent means no reasoning support. A profile in `settings.json` may narrow this further per use with
`reasoningLevel` and `reasoningBudget`; every profile key is in the generated
[configuration reference](configuration.md).

Three catalogues merge in this order, and the user file wins by id: the embedded snapshot, the
managed cache `~/.lunate/models.catalog.json`, and your own `~/.lunate/models.json`. A schema-1
override keeps the embedded entry's pricing, maximum output, vision support and wire id, because
schema 1 cannot express them; a schema-2 override replaces the entry completely.

`lunate --update-catalog` fetches <https://models.dev/api.json>, maps it through the one catalogue
mapper and atomically writes the managed cache; `--output <path>` writes the snapshot elsewhere and
`--source <path-or-url>` reads a local snapshot for offline use. Nothing refreshes at startup or in
the background, and an offline or malformed fetch leaves the old cache untouched.

For an endpoint that is not in the catalog at all, `lunate --discover <name-or-url>` prints a
`models.json`-shaped draft of the model's entry to stdout — you review it and copy it into the file;
nothing is written for you.

## Where the credential lives

Stored keys live in `~/.lunate/auth.json` under `keys`, schema 1, with owner-only permissions where
the platform has file modes. `providers.json` and `settings.json` contain only provider/profile
configuration and credential references, never resolved secret values.

Every preset also has a provider environment variable — `OPENAI_API_KEY`, `ANTHROPIC_API_KEY`,
`GROQ_API_KEY` and so on; Google also accepts `GEMINI_API_KEY`. When that variable is set it wins
over the key stored in the file. `lunate auth list` shows which source is in use, and
`lunate auth status` answers whether the resolved default provider has a credential at all.

## Related

- [First run](first-run.md) — the wizard, `/auth`, profiles and the usages that bind them
- [Environment variables](environment-variables.md) — the credential variable for each preset
- [Settings reference](configuration.md) — every provider and profile key, generated from the schema
- [Decisions](configuration.md#system-one-decision-profiles) — the optional second model, its
  System One transport and the presets that serve it
- [CLI reference](cli.md) — `--model`, `--discover` and `--update-catalog`
