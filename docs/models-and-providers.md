# Choose a model and provider

This page answers how to pick the provider, credential, model and profile a session uses. The short
answer: the setup wizard does it on a fresh install, and `/auth`, `lunate auth`, `providers.json`,
`models.json` and `settings.json` are the ways to change it afterwards.

## The in-session picker

Start `lunate` in a repository. When the session has no usable credential and stdin and stdout are
terminals, the first run opens the auth picker once. It lists the twelve presets in
[`providers.json`](../src/Lunate.Ai/providers.json) order, followed by
`Custom OpenAI-compatible endpoint…`:

- `↑`/`↓` or `j`/`k` move the selection.
- `Enter` selects.
- `Esc` dismisses the picker without writing anything.

Selecting a preset opens the same masked prompt as `lunate auth login`. After a successful save the
notice names the provider and points at `/model`; it never contains the key. Lunate then offers an
optional verification: one minimal request, asked first, and a failure reports the reason while
keeping the stored key. Skip it if the network is metered or the machine is air-gapped.

`/auth` opens the picker again at any time. `/login <provider>` remains an alias that skips the
picker and opens the masked prompt directly; `/logout <provider>` removes a stored key. While a
turn is running, `/auth` and `/login` refuse rather than prompt.

Print mode, ACP and runs with piped stdin never prompt. A missing credential stays the explicit
error that names the key and the environment variable, and the empty-session banner names `/auth`
as the next step whenever nothing is configured.

## The manual `auth login` path

The CLI path below stays available for scripted bootstrap and for users who prefer one command at
a time.

From a terminal:

```bash
lunate auth login
```

With no provider argument the command prints the supported provider ids and names and exits —
0 on a terminal, and 2 with the same list on stderr when stdin is a pipe. It never waits for a key
nobody can type. Then choose one:

```bash
lunate auth login anthropic
```

On a terminal the key is read through a masked prompt. In a script, pipe one line to the command:

<!-- run: no sends a provider request -->
```bash
printf '%s\n' "$ANTHROPIC_API_KEY" | lunate auth login anthropic --key-stdin
```

Neither path echoes the value. A pipe without `--key-stdin` exits 2 with the hint instead of
guessing.

`lunate auth login` is one of four subcommands:

| Command | What it does |
| --- | --- |
| `lunate auth list` | One line per provider entry with credential presence and bounded reachability (`reachable`, `unreachable`, or `unknown`). Never prints a credential or header value. Exits 0 even when nothing is stored. |
| `lunate auth login <provider>` | Stores a key under the provider entry name. Use `--key-stdin` to read one line from a pipe. |
| `lunate auth logout <provider>` | Removes the stored entry, names a still-set environment variable without its value, and exits non-zero when nothing was stored. |
| `lunate auth status` | Reports the resolved default provider and whether a credential exists; exits 0 when it does and 1 when it does not, so a bootstrap script can branch on it. |

## Provider instances and credentials

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
environment. `region` (`eu`, `us`, or `local`) is
descriptive metadata only: it does not choose an endpoint or guarantee data residency.

On load, Lunate idempotently migrates legacy stored keys and custom endpoints into matching provider
entries. An explicit provider entry wins, and migration never copies the secret out of `auth.json`.
Environment credentials continue to take precedence over stored keys, including in CI. Reachability
shown by `auth list` is a bounded unauthenticated probe; it sends neither credentials nor optional
headers.

Project settings may refer to user-defined providers and profiles or lower user limits. They cannot
define a provider, endpoint, credential reference, or header secret. This prevents a repository from
redirecting a user's credentials to its own server.

## Profiles and usages

`~/.lunate/settings.json` binds a profile name to a provider entry and catalog model, then assigns
profiles to existing usages; every settings key with its type, default and scope is in the
generated [configuration reference](configuration.md). For example:

<!-- config: settings -->
```json settings
{
  "schemaVersion": 2,
  "profiles": {
    "chat-main": { "provider": "work-anthropic", "model": "claude-sonnet-5-5" },
    "reviewer": { "provider": "work-anthropic", "model": "claude-opus-5-5" },
    "quick": { "provider": "work-anthropic", "model": "claude-sonnet-5-5" }
  },
  "usages": { "chat": "chat-main" }
}
```

Profiles may set supported reasoning, output, temperature, context-window, and capability-override
fields. Unsupported settings are refused by field and profile name before a request. `/model`
changes only the chat profile; `provider/model` selects through a named provider, and a bare model
id must be unique. A profile's own `model` is read in its `provider`'s scope first, so a profile on
a custom endpoint may carry that endpoint's bare model id even when the catalogue holds the same id
under other providers. A custom endpoint is checked by the API family it serves rather than by the
adapter its kind names: a model the catalogue only knows under another provider resolves through
the endpoint, and that catalogue entry contributes the model's facts (context window, capabilities)
but never its pricing. Subagents and compaction default to chat when unassigned; compaction uses
`fast` only when explicitly selected.

The optional decision profile lives in the same block; [Decisions](decisions.md) owns what it does.

## Your own model catalog

`~/.lunate/models.json` is your catalog of model entries; a user entry overrides a built-in one with
the same id, and adding a model never needs a code change. An entry carries `id`, `provider`
(`openai` for any OpenAI-compatible endpoint, `anthropic`), optional `endpoint`, `contextWindow`,
`supportsTools` and an `auth` reference naming a key in `lunate auth login`'s `auth.json`. An
optional `reasoning` declares the model's reasoning style:

- `effort` — OpenAI-compatible reasoning effort.
- `budget` — Anthropic extended thinking.
- `inline` — the model embeds thinking markers in its text stream.

Absent means no reasoning support. A profile in `settings.json` may narrow this further per use
with `reasoningLevel` and `reasoningBudget`; every profile key is in the generated
[configuration reference](configuration.md).

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

## A custom OpenAI-compatible endpoint

For Ollama, vLLM, LM Studio or any other OpenAI-compatible server, choose the custom row at the
bottom of the `/auth` picker, or run:

```bash
lunate auth login --custom
```

The interactive flow asks for provider id, base URL, API family, credential placement, an optional
models URL, model id and key. The family defaults to `openai-chat`, because every existing custom
provider is OpenAI-compatible — an omitted flag never silently re-routes one. A script supplies the
values as flags and the key on stdin:

<!-- run: no contacts a custom endpoint -->
```bash
printf '%s\n' "$LOCAL_KEY" |
  lunate auth login --custom \
    --id local --base-url http://localhost:11434/v1 --model llama3 --key-stdin
```

An Anthropic-compatible endpoint names its family and placement:

<!-- run: no contacts a custom endpoint -->
```bash
printf '%s\n' "$ANTHROPIC_KEY" |
  lunate auth login --custom \
    --id my-anthropic --base-url https://api.example.com/v1 \
    --api-family anthropic-messages --credential-placement x-api-key \
    --models-url https://api.example.com/v1/models \
    --model claude-example --key-stdin
```

Placements are `bearer` (the default), `x-api-key`, `header:<name>` (for example Azure's `api-key`),
`header:<name>;scheme=<scheme>` (for example `header:Authorization;scheme=Token`) and `query:<name>`
(for example Google's `key`). The entry is written in the v2 shape — `baseUrls`
keyed by family, `credentialPlacement`, optional `modelsUrl`, headers — and the key stays in
`auth.json`, never in `providers.json`.

Saving validates the entry:

- The base URL must be absolute and `https`, or `http` only for a loopback host.
- The model id is required.
- The API family must be one the client wires; an unknown family is refused by name.
- Discovery through `--models-url` stays inside the entry's configured endpoint: a redirect that
  changes the host, scheme or port is refused before any request is sent there.
- The provider id must be non-empty, must not contain spaces or `/`, and must not shadow a built-in
  preset.

A refused entry is not echoed and writes nothing. `lunate auth login --custom` remains available for
legacy single-endpoint setup. Its endpoint is migrated into the named provider store while the key
remains in `auth.json`; new multi-provider setups should use `providers.json` and a profile in
`settings.json`. The model can then be selected in `/model` by its qualified provider/model id.

## Related

- [Quickstart](quickstart.md) — install, the setup wizard and the first task
- [Decisions](decisions.md) — the optional second model for approvals, routing and questions
- [Configuration reference](configuration.md) — every settings key, its scope and its default
- [Where thinking resolves from](configuration.md#where-thinking-resolves-from) — the thinking level
  a profile selects
