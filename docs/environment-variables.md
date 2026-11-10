# Environment variables

This page answers which environment variables Lunate reads and what each one changes, and the short
answer is: the state root, one override per settings key, one credential variable per provider, and
the terminal facts it detects. Everything else is a settings key — the generated
[settings reference](configuration.md) owns those.

Lunate reads the **process environment only**. No `.env` file, settings file, `AGENTS.md` or project
file can supply any variable on this page.

## Point Lunate at a state root

| Variable | Effect |
| --- | --- |
| `LUNATE_HOME` | The state root — sessions, extensions, cache, the trust store, credential store and recordings. Must be an absolute path; a root inside the workspace is refused, because such a repository could write the trust records that govern it. |
| `HOME` | The profile the default `~/.lunate` is resolved against on POSIX. Windows resolves the profile through the shell, so `HOME` alone does not move the state root there. |

`lunate --version` and the welcome header both print the active state root when `LUNATE_HOME` is set,
so a redirected run is never invisible.

<!-- run: offline -->

```bash
lunate --version
```

## Tell a command it runs inside a session

| Variable | Effect |
| --- | --- |
| `LUNATE_SESSION` | Set to `1` in every child the bash tool spawns. A command that sees it knows a Lunate session is its parent: `lunate install` refuses without `--yes` from there, and `lunate trust --yes` refuses because approval is the user's answer. It is a visibility guard, not a lock — `env -u LUNATE_SESSION …` removes it. |

## Override a setting for one process

These win over the settings file and are overridden by the matching launch flag. A project settings
file cannot set them, and a key with no row here has no environment override.

| Variable | Overrides | Values |
| --- | --- | --- |
| `LUNATE_APPROVAL` | `approval` | `ask`, `auto-edit` |
| `LUNATE_MODEL` | `model` | a model id |
| `LUNATE_TOOL_DETAILS` | `toolDetails` | `collapsed`, `expanded` |
| `LUNATE_TOOL_OUTPUT_LIMIT` | `output.toolResultLimit` | characters |
| `LUNATE_TOOL_SPILL_THRESHOLD` | `output.spillThresholdTokens` | estimated tokens |
| `LUNATE_EXTRA_TOOLS` | `tools.extra` | `grep`, `find`, `ls` |
| `LUNATE_STARTUP_HEADER` | `startupHeader` | on/off |
| `LUNATE_UPDATE_CHECK` | `updateCheck` | on/off |
| `LUNATE_SUBAGENTS_CONCURRENCY` | `subagents.concurrency` | a count, or `unlimited` |
| `LUNATE_SUBAGENTS_TOKEN_BUDGET` | `subagents.tokenBudget` | tokens, or `unlimited` |
| `LUNATE_SUBAGENTS_TREE_TOKEN_BUDGET` | `subagents.treeTokenBudget` | tokens, or `unlimited` |
| `LUNATE_PROJECTION_BATCH_TURNS` | `projection.batchTurns` | model turns |
| `LUNATE_PROJECTION_MASK_TURNS` | `projection.maskTurns` | model turns |
| `LUNATE_PROJECTION_MASK_ERROR_TURNS` | `projection.maskErrorTurns` | model turns |

## Supply a credential

Each built-in provider preset names one variable, and a set variable always wins over the key stored
in `~/.lunate/auth.json`:

| Preset | Variable |
| --- | --- |
| OpenAI | `OPENAI_API_KEY` |
| Anthropic | `ANTHROPIC_API_KEY` |
| OpenRouter | `OPENROUTER_API_KEY` |
| DeepSeek | `DEEPSEEK_API_KEY` |
| Moonshot AI | `MOONSHOT_API_KEY` |
| Z.AI | `ZHIPU_API_KEY` |
| OpenCode | `OPENCODE_API_KEY` |
| OpenCode Go | `OPENCODE_API_KEY`, or `OPENCODE_GO_API_KEY` |
| Groq | `GROQ_API_KEY` |
| Together AI | `TOGETHER_API_KEY` |
| xAI | `XAI_API_KEY` |
| Google | `GOOGLE_API_KEY`, or `GEMINI_API_KEY` |

A provider entry in `~/.lunate/providers.json` may instead reference any variable with an
`env:<VARIABLE>` credential. `lunate auth list` shows which source is in use for each entry;
[Models and providers](providers.md) owns the entry format.

These variables are also the credential values withheld from tool children: the bash tool and an
MCP server do not inherit them, and an MCP server's own `env` map or `toolEnvironment.passthrough`
has to name a variable to admit it.

## Let Lunate detect your terminal

| Variable | Effect |
| --- | --- |
| `NO_COLOR` | Set and non-empty: no colour at all. A theme can only lower the colour depth, so this wins over `tui.theme`. |
| `COLORTERM` | Read for `truecolor` or `24bit` to select the true-colour palette. |
| `TERM` | `dumb` turns Unicode off, keeps the setup wizard closed, and makes a bare `lunate` exit `2` with a hint to print mode. |
| `LUNATE_ASCII` | Set and non-empty: ASCII output, even where a locale would allow Unicode. |
| `LUNATE_UNICODE` | Set and non-empty: Unicode output. `LUNATE_ASCII` wins when both are set. |
| `WT_SESSION` | Windows Terminal: makes Unicode the default on Windows. |
| `LC_ALL`, `LC_CTYPE`, `LANG` | On POSIX, an unset locale or one containing `utf8`/`UTF-8` means Unicode; any other locale means ASCII. |
| `CI` | Set to anything but `0`/`false`: the update check stays off and the setup wizard does not open. |

## Record, replay and export telemetry

| Variable | Effect |
| --- | --- |
| `LUNATE_RECORD` | `1` or `true` records every model stream update to a recording. |
| `LUNATE_RECORD_PATH` | The file a recording is written to; absent means the fixture path under the state root. |
| `LUNATE_REPLAY` | Run from a recording instead of calling a provider — no credential is resolved and no client is constructed. A recording request wins when both are set. |
| `LUNATE_DATE` | `yyyy-MM-dd`: pins the date the system prompt is built with, so a replayed request stays byte-stable. |
| `LUNATE_OTEL` | `1` or `true` turns the OpenTelemetry exporter on. Off is the default and there is no telemetry otherwise. |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Where those spans and metrics are exported. |
| `LUNATE_LIVE` | `1` makes the repository's contract tests run against real endpoints instead of recordings. A contributor variable; the product never reads it. |

## Diagnose a terminal that drops keys

| Variable | Effect |
| --- | --- |
| `LUNATE_INPUT_TRACE` | `1` traces decoded key events to `diagnostics/input-trace.log` under the state root. Any other value must name a path inside that root. It records key kinds, modifiers and counts — never characters — so typing a credential into a prompt leaves nothing in the file. |

## Related

- [Settings reference](configuration.md) — every settings key these variables override, generated
  from the schema
- [Models and providers](providers.md) — provider entries and the credential references they carry
- [CI and unattended runs](ci.md) — the variable a CI run sets first
- [Print mode](print.md) — the run that reads a credential and never prompts
- [Execution environments](execution-environments.md) — what a tool container does and does not
  inherit
