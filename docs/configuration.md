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
are described in [Decisions](decisions.md); the generated [settings reference](settings.md) lists
the exact keys, defaults and scopes.

## Custom provider entries

The `providers.<id>` settings block describes one user-owned custom endpoint. [The provider
reference](providers.md#custom-provider-entries) owns its shape, the API families it may serve, the
endpoint precedence and the credential and header rules; `lunate auth login --custom` is the way to
write one ([models and providers](models-and-providers.md#a-custom-openai-compatible-endpoint)).

## The web search backend

`web_search` runs on DuckDuckGo's HTML endpoint with no configuration. One user-scoped section
switches it, and a backend is a tool setting, not a model usage — it never appears in `/cost`:

```json
{
  "tools": {
    "webSearch": {
      "backend": "searxng",
      "url": "https://search.example.test/",
      "credential": "env:SEARXNG_TOKEN"
    }
  }
}
```

`backend` is `duckduckgo` (the default), `searxng`, `brave` or `tavily`. `url` is a SearXNG
instance's base URL (https, or http on a loopback host) and belongs to that backend only. `credential`
is a reference in the provider form — `auth:<name>` for a key in `auth.json`, `env:<VARIABLE>` for
an environment variable, `helper:<name>` for a helper this host cannot resolve — and never a literal
key. It is required by `brave` and `tavily` and optional for a SearXNG instance behind an
authenticating proxy. The reference is resolved when a search is sent, from the same auth store and
environment provider credentials use; an `env:` name it resolves is stripped from tool child
processes, like a provider's. A `tools.webSearch` section in a project settings file is refused:
only you choose an endpoint that spends your key.

A misconfigured section fails at start, naming the key: an unknown backend, a missing `url` or
`credential`, an irrelevant one for the chosen backend, or a credential value that is not a
reference. The approval prompt is the same for every backend (`web_search` is `Execute` risk, on the
`bash` tier), so a declined call sends no request and spends no key.

## Provider-native search

Some providers run the search inside the chat request instead of returning a tool call. That search
cannot be approved per call, so it is a separate, explicit opt-in:

```json
{
  "tools": {
    "nativeSearch": true
  }
}
```

The key is user-scoped and defaults to `false`; a project settings file may not carry it, and there
is no environment-variable override. It only takes effect when the chat model declares the
`web_search` capability feature. Declare it through the profile (keeping `tools`, because a profile's
`capabilities.features` replaces the whole set) or in a `models.json` entry:

```json
{
  "profiles": {
    "chat": {
      "provider": "openai",
      "model": "gpt-5",
      "capabilities": { "features": ["tools", "web_search"] }
    }
  }
}
```

With both the opt-in and the declared feature, the request carries the provider's built-in search
and does not declare the local `web_search` tool; `--tools` still governs it, so a selection that
does not name `web_search` declares neither. Without either half, nothing changes: the local tool is
offered and the `tools.webSearch` backend settings keep applying. The search is part of the chat
call, so it is billed and attributed to that call — there is no separate usage, cost lane or session
entry. [ADR-0046](../adr/0046-web-search-layers.md) records the approval trade-off behind the
opt-in.

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
  [models-and-providers.md](models-and-providers.md). The provider-entry and catalog reference is
  [providers.md](providers.md). The first-run walk is in [quickstart.md](quickstart.md).
- Environment-variable overrides and `/settings`: [environment-variables.md](environment-variables.md).
  The key remaps the `tui.keys` entries persist: [keybindings.md](keybindings.md).
- Project instructions: [agents-md.md](agents-md.md). Skills: [skills.md](skills.md). MCP server
  files: [mcp-servers.md](mcp-servers.md). The theme: [themes.md](themes.md).
- Why the parser is deny-by-default and every problem fails the load as one error:
  [`openspec/specs/agent-config/`](../openspec/specs/agent-config/spec.md) owns that behaviour.
