# Configuration

This page answers how to configure a Lunate session, and the short answer is `settings.json`: decision
profiles and their consumers, custom provider entries, where thinking resolves, the welcome header and
update check, and recording and replay. What a project or the user owns beyond the settings file has a
page of its own — [AGENTS.md](agents-md.md), [skills](skills.md), [MCP servers](mcp-servers.md) and
[themes and keys](themes-and-keys.md).

Every accepted key, with its type, default, scope and allowed values, is in the generated
[settings reference](settings.md); the table cannot list a key the store refuses.

## Decisions

The decision profile, the approval, routing and question consumers, and the System One transport
are described in [Decisions](decisions.md); the generated table above lists the exact keys,
defaults and scopes.

## Custom provider entry v2

The `providers.<id>` settings block describes one user-owned custom endpoint and serves exactly one
API family. New entries use `baseUrls` keyed by family; the accepted names are `openai-chat`,
`openai-responses`, `anthropic-messages` and `google`. The older `baseUrl` spelling remains readable
as the `openai-chat` family and loading it does not rewrite the file. New writes use `baseUrls`.
An entry whose resolved family is `anthropic-messages` gets the same prompt-cache breakpoints and
turn merge as the native Anthropic preset; the resolved family selects the wire behavior, not the
provider name or its kind.

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
`base_url` supplies that model's endpoint. The discovery family keys map as follows: `chat-completions`
to `openai-chat`, `responses` to `openai-responses`, `messages` to `anthropic-messages`, and
`generate-content` to `google`. Discovery also records input/output modalities, tool and reasoning
capabilities, reasoning efforts, context limits, and output limits. A profile's `apiFamily` override
wins over discovery metadata and the catalog. Endpoint precedence follows the same intent: a
provider entry's declared `baseUrls` URL for the resolved family (or its `baseUrl` for the default
family) wins, otherwise the model's discovered `endpoint` is kept, and only then does the preset's
family URL apply. A custom provider's discovered endpoint and redirects must stay on its configured
host, scheme and port; a preset may use its first-party per-family endpoints. See
[ADR-0029](../adr/0029-provider-api-families.md) for the accepted provider-family decision.

Headers may be literal values or templates over `{session.id}`, `{run.id}`, and `{lunate.version}`.
A run without a session (`--no-session`) still resolves `{session.id}` to an ephemeral per-process
conversation id, so a header-templated provider works fileless; a call that carries no run context
at all still fails instead of sending an empty value. The credential is separate:
`credentialPlacement` selects the default bearer header, a named header such as `x-api-key`, or a
query parameter such as Google's `key`. Templates cannot read credentials; invalid placeholders and
placements are rejected when the provider loads.

## Where thinking resolves from

The session's thinking level resolves once at start, from the most specific fact available: the
`--thinking` launch flag wins over the chat profile's `profiles.<chat>.reasoningLevel`, which wins
over the file's global `thinking` key, and without any of them the level is `off` — a profile
without a reasoning setting falls back to `thinking` exactly as before. `/thinking <level>` changes
the running session only: the settings file and the profile stay as they are. Switching to another
model or profile re-seeds the level: the newly selected profile's `reasoningLevel` (or the resolved
`thinking` default when it has none) replaces the previous level before the harness is rebuilt, so
the next request, the footer and `/thinking` agree. `/settings thinking
<level>` is what persists — it writes the chat profile's `reasoningLevel` (`off` removes the key,
because a profile has no `off`), and a chat selection that is not a configured profile in the user
settings file is refused. A model whose catalog entry declares no reasoning style refuses a level by
name in both commands, naming the model and the setting. A profile `reasoningBudget` is a validated
configuration fact, not a session default. The setup wizard edits the same profile key (#475).

## The welcome header and the update check

The welcome header names the running build: `lunate v0.2.1+1b045e5`. The seven characters after `+`
are the commit it was built from, so two installs can be told apart without running
`lunate --version`; a working-tree build with no commit metadata shows the plain version.

The interactive session checks GitHub Releases at most once a day, in the background after the
welcome header, and names a newer release as one dim linked line: `v0.2.0 available · changelog`.
The answer is cached under `~/.lunate/update-check.json` and reused for 24 hours without a request.
There is no telemetry: the only header sent is `User-Agent: lunate/<version>`. Turn it off with
`updateCheck: false` or `LUNATE_UPDATE_CHECK=0`; `--offline`, print mode, ACP and CI never check.
The current stable release always comes from the release tags — the README's version line is
generated by [`scripts/docs-sync`](../scripts/docs-sync) — never hand-written here.

`yolo` is not a setting: it exists only as the per-run flag `lunate -p --yolo`.

## Recording and replay

`LUNATE_RECORD=1` records a run's model stream to a session recording (`LUNATE_RECORD_PATH` names
the file), `LUNATE_REPLAY=<file>` runs a session from that recording instead of calling a provider,
and `LUNATE_DATE=yyyy-MM-dd` pins the date the system prompt is built with so a replayed request
stays byte-stable. Recording wins when both are set.

## Related

- Provider entries, credentials, the user's own model catalog and profiles:
  [models-and-providers.md](models-and-providers.md). The first-run walk is in
  [quickstart.md](quickstart.md).
- Environment-variable overrides and `/settings`: [guide.md](guide.md). The key remaps the
  `tui.keys` entries persist: [keybindings.md](keybindings.md).
- Project instructions: [agents-md.md](agents-md.md). Skills: [skills.md](skills.md). MCP server
  files: [mcp-servers.md](mcp-servers.md). The theme: [themes-and-keys.md](themes-and-keys.md).
- Why the parser is deny-by-default and every problem fails the load as one error:
  [`openspec/specs/agent-config/`](../openspec/specs/agent-config/spec.md) owns that behaviour.
