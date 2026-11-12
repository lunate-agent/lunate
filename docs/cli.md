# Command and option reference

This page answers what you can type after `lunate`, and the short answer is: one invocation per
mode, then one flat set of options that the interactive and print modes both read. The options
below are checked against `lunate --help` by `DocsCliOptionsTests`, so this page and the binary
cannot drift apart.

<!-- run: help -->

```bash
lunate --help
```

## Pick a mode

A bare `lunate` starts the interactive session. Every other mode is a flag or a subcommand:

| Invocation | What it does | Page |
| --- | --- | --- |
| `lunate` | the interactive session on a terminal | [Usage](usage.md) |
| `lunate -p "<prompt>"` | one prompt to completion, the final answer on stdout | [Print mode](print.md) |
| `lunate -p --json "<prompt>"` | the same run as one JSON object per event | [JSON events](json-events.md) |
| `lunate --acp` | the Agent Client Protocol over stdio for an editor | [ACP](acp.md) |
| `lunate setup [flags]` | the setup wizard, scriptable end to end | [CI and unattended runs](ci.md) |
| `lunate auth <list\|login\|logout\|status>` | providers, credentials and which source each one uses | [Models and providers](models-and-providers.md) |
| `lunate trust [--yes [--digest <sha>]] [--json] [--clear]` | this repository's trust records and pending decisions | [CI and unattended runs](ci.md) |
| `lunate extensions [--json]` | discovered extensions with scope, state, load time and contributions | [Extension authoring](extensions/README.md) |
| `lunate extensions build <id\|path> [--json]` | build one single-file extension without a session | [Single-file extensions](extensions/single-file.md) |
| `lunate extensions reload <id\|path> [--json]` | rebuild and reload one extension | [Single-file extensions](extensions/single-file.md) |
| `lunate install <package-id>[@<version>]` | install a package's extensions and skills | [Installing packages](extensions/packages.md) |
| `lunate uninstall <package-id\|extension-id>` | delete exactly what an install's record lists | [Installing packages](extensions/packages.md) |
| `lunate packages [--json] [--verify]` | the installed packages this working directory can see | [Installing packages](extensions/packages.md) |
| `lunate cache prune [--json]` | remove the extension builds nothing declares any more | [Single-file extensions](extensions/single-file.md) |
| `lunate mcp roslyn` | the read-only C# tools over stdio, for another MCP host | [Roslyn MCP server](mcp-roslyn.md) |
| `lunate --discover <name-or-url>` | an endpoint's models, printed as a `models.json` draft | [Models and providers](providers.md) |
| `lunate --update-catalog` | refresh the managed model catalogue from models.dev | [Models and providers](providers.md) |
| `lunate --version` | the running build, and the state root when one is set | |
| `lunate --help` | this list | |

`lunate auth login --custom` takes the same endpoint answers the wizard asks for; its flag set is in
[Models and providers](models-and-providers.md#a-custom-openai-compatible-endpoint).

## Options

These are the flags every mode reads. `lunate -p` is the long form of `lunate --print`.

<!-- cli-help: start -->

| Option | What it does |
| --- | --- |
| `-p`, `--print` | run one prompt and print the final answer to stdout |
| `--json` | with `-p`, write every event as one JSON line instead of the answer; a usage error with `--acp` |
| `--yolo` | approve every tool call for this run; never a setting |
| `--acp` | serve the Agent Client Protocol over stdio |
| `--no-extensions` | start any mode without loading extensions; `mcp.json` servers still run |
| `--fullscreen` | start the interactive session in the full-screen workspace |
| `--offline` | skip the daily update check for this run |
| `--version` | print the version |
| `-h`, `--help` | print this reference |

Session selection — mutually exclusive with each other and with `--no-session`, and a usage error
under `--acp`, because the editor owns session creation:

| Option | What it does |
| --- | --- |
| `-c`, `--continue` | continue the newest session recorded for this working directory |
| `-r`, `--resume` | choose a session from the interactive picker; print mode cannot draw it |
| `--session <id\|path>` | load one session by id or file path |
| `--no-session` | run without writing a session file |

Per-run composition:

| Option | What it does |
| --- | --- |
| `--model <id>` | use this model for the run, which wins over settings |
| `--thinking <level>` | `off`, `low`, `medium` or `high` for the run |
| `--tools <list>` | expose only these comma-separated tool names |
| `--extra-tools <list>` | enable optional search tools (`grep`, `find`, `ls`) |
| `--no-tools` | expose no tools for the run |
| `--max-cost <usd>` | stop the run at this cumulative session spend |
| `--max-time <duration>` | stop the run at this wall-clock limit (`30s`, `5m`, `1h`) |
| `--allow-unpriced` | allow a cost limit over a model with no known price |
| `--system-prompt <text>` | replace the composed system prompt wholesale |
| `--append-system-prompt <text>` | append a section after the composed system prompt |

Subcommand and other-mode options:

| Option | Belongs to |
| --- | --- |
| `--yes` | `trust` (record exactly what it prints), `install` (run from inside a session) |
| `--digest <sha>` | `trust`, with `--yes`: the reviewed non-interactive approval |
| `--clear` | `trust`: remove this repository's records |
| `--provider`, `--endpoint`, `--api-family`, `--credential-placement`, `--models-url` | `setup` |
| `--credential`, `--key-stdin`, `--model`, `--profile`, `--thinking`, `--fast` | `setup`, `auth login` |
| `--as-decision`, `--no-test`, `--no-model-check`, `--force` | `setup` |
| `--custom` | `auth login`: add an OpenAI-compatible endpoint |
| `--project`, `--dry-run` | `install` |
| `--offline` | `install`: resolve from the local package cache |
| `--verify` | `packages`: re-check each install record against disk |
| `--discover` | list an endpoint's models and print a `models.json` draft |
| `--update-catalog`, `--output <path>`, `--source <path-or-url>` | refresh the managed model catalogue |

<!-- cli-help: end -->

A value may also be given as `--flag=value`, which is how a value that begins with `-` is passed.

## Read the exit code

Every mode shares three codes, and two commands add their own:

| Code | Meaning |
| --- | --- |
| `0` | the command did what it was asked |
| `1` | the command failed, or a run ended in an error before a clean finish |
| `2` | usage error: unknown flag, missing or conflicting argument, no terminal for a prompt |
| `3` | `trust`: pending decisions were found; `-p`: the run reached its cost limit |
| `4` | `trust --digest`: the digest did not match; `-p`: the run reached its time limit |
| `130` | the run was cancelled |

[Print mode](print.md#read-the-exit-code) owns what each print-mode code means for a script.

## Related

- [Print mode](print.md) — one prompt to completion, and the answer on stdout
- [JSON events](json-events.md) — the `--json` stream a script parses
- [ACP](acp.md) — the editor integration
- [CI and unattended runs](ci.md) — the scripted path through `setup`, `trust` and `-p`
- [Models and providers](providers.md) — what `--model`, `--discover` and `--update-catalog` act on
- [Environment variables](environment-variables.md) — the overrides that are not flags
