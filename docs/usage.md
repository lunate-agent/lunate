# Usage

This page answers what the interactive session does turn by turn: typing and completing a prompt,
what a turn commits to the scrollback, how tools and approvals appear, sessions and thinking. The
short answer: `lunate` in a repository — you type a task, writes and commands stop at an
approval, and each finished tool call commits one line to the scrollback. Choosing a model, setting
up the terminal, copying or exporting, the decision layer and configuration each have a page of
their own, linked under [Related](#related).

## Type, complete, send

The input is a box: a rule above it at every width, a second rule below from 60 columns, the `>`
marker and the `Ask anything, / for commands` placeholder in the theme's user colour while it is
empty, and a box that grows with the draft to eight lines before it scrolls. Typing `/` opens the
autocomplete list — the built-in commands, `/skill:<name>` and each loaded extension's commands,
with descriptions — and `@` lists matching workspace files from the lazy file index. Both lists
cap at eight rows above the input.

| Key | At the prompt |
| --- | --- |
| `/` | list commands, skills and extension commands |
| `@` | list workspace files |
| `↑` / `↓` | move the autocomplete selection; history when the list is closed |
| `Tab` / `Enter` | accept the selected item; Enter sends when the token is already exact |
| `Esc` | close the list first, otherwise cancel the running turn |
| `Alt+Enter` / `Ctrl+J` | insert a newline |

![The input box, the slash-command list and the file list](media/features/input.gif)

## Every turn opens with your message

The submitted prompt commits to the scrollback as the first block of its turn — the `>` prompt in
the theme's user colour, wrapped continuation lines indented two spaces, one blank line after — so the
question stays visible above the answer. While the turn runs, the activity line names the work
(`⠹ thinking… 3s`, `⠸ read src/Foo.cs · 4s`, `| working… 7s`), with the run's `↑in ↓out` deltas and
`esc to interrupt` on the wide bands; below 60 columns it keeps the spinner, the label and the
elapsed seconds.

![Two turns with the user echo, the tool block and the activity line](media/features/turns.gif)

## Edits you approve, with the diff

Writes, commands and web calls stop at a panel: a warning headline naming the tool and what it will
do, a bounded detail block with the command, the edit's diff or the URL, and
`Allow edit?  [y]es  [n]o  [a]lways this session`. Below 60 columns the detail shortens first and
every choice stays on screen. The result names the match tier it used (`exact`, `normalized` or
`indent`), and an ambiguous edit is refused instead of guessed.

![The approval panel showing an edit's diff and its choices](media/features/approval.gif)

## Tool calls commit as one line

A finished call collapses to one header line — `✓ read  src/Foo.cs (1-13)`,
`✗ bash  dotnet test · exit 1 · 12s`, `✓ edit  src/Foo.cs · +3 −0`,
`✓ web_fetch  example.com/page` (ASCII `+`/`x` without Unicode) —
with a per-tool excerpt and the diff for `edit`/`write` under it. What the excerpt elides is one
`ctrl+o` away: the full-screen transcript viewer shows every block with its complete output and
diff, scrolls with the arrows and PageUp/PageDown, and closes on `q` or `Esc`.

![Compact tool blocks and the Ctrl+O transcript viewer](media/features/tools.gif)

`web_search` and `web_fetch` ask like `bash`: one panel per call, with the query or URL in the
detail, and `[a]lways this session` covers that tool for the rest of the session. A search returns
ranked title, URL and snippet results; a fetch returns the page's readable text, its title and its
links as markdown. Both stop at their byte cap and a failure names the URL and the reason.

`web_search` runs on DuckDuckGo with no configuration. A `tools.webSearch` section in
`~/.lunate/settings.json` switches it to your own SearXNG instance, or to the Brave or Tavily API
with a credential reference; the approval prompt and the result shape stay the same whatever the
backend, so a search is still a tool call you see. [Configuration](configuration.md#the-web-search-backend)
owns the setting.

If your provider runs search natively and the chat model declares the `web_search` feature, setting
`tools.nativeSearch` to `true` moves the search inside the chat request: there is no per-call
approval prompt and the local tool is not offered. The search is billed and attributed to the chat
call. [Configuration](configuration.md#provider-native-search) owns that opt-in.

## Long commands keep running in the background

A build, a test suite or a dev server does not have to hold the turn. `bash` with `background: true`
starts it and returns a job id immediately; `bash_output` returns only what the job wrote since the
last poll and says whether it is still running or has ended with its exit code; `bash_kill` stops
the job and its whole process tree. The existing bash timeout still applies, unread output is
bounded with a truncation marker, and ending the session kills the jobs it still owns.

## The plan stays on screen

The model can keep a short todo list with the `todo` tool. Each call sends the whole list, with every
item `pending`, `in_progress` or `done`, and at most one item in progress. The live area shows the list
above the input box as up to five lines — the rest collapses to `… N more` — so it never pushes the
prompt off screen. An empty list, or a session that never calls the tool, renders no plan. The list is
part of the session's tool calls, so a resumed session shows the same plan it had before the restart.

## Facts survive in LUNATE.md

The model can carry a project fact into later sessions with the `remember` tool. It appends one
Markdown bullet to `LUNATE.md` at the repository root — never a global file — and leaves the rest of
the file alone. The next session reads it from the system prompt after your `AGENTS.md` rules, which
still win if a note disagrees. The file is capped at 8,000 characters: a longer file is read up to
the cap with a marker saying what was omitted, and an append that would pass the cap is refused
without touching the file. [AGENTS.md](agents-md.md) owns the whole rule.

## C# the compiler agrees with

In a C# workspace the model can ask `cs_diagnostics` for compiler errors and warnings — a compile
check after an edit without a full `dotnet build` — and navigate by symbol with `cs_find_symbol`,
`cs_find_references` and `cs_outline`. Roslyn loads on the first C# tool call, and restore, SDK and
partial-load problems come back as messages the model can act on.

![Running cs_diagnostics after an edit](media/features/diagnostics.gif)

## Repository code asks once

A project extension, a project `mcp.json` server and a project skill are code from the repository,
so each asks once per repository — and again when its files change:
`Load project extension 'acme-tools' 0.3.0 from /repo/.lunate/extensions/acme-tools?  [y]es  [n]o`.
Answers are recorded in `~/.lunate/trust.json`; `lunate trust` inspects and records them for
scripted runs.

![The trust prompt for a project extension](media/features/trust.gif)

## You can see what is loaded

`/extensions` lists every discovered extension with its scope, state, load time and what it
registered; `/mcp` lists the MCP servers with their status and source. `lunate extensions --json`
prints the same data for scripts.

![The /extensions listing in a session](media/features/extensions.gif)

## You can watch it think

With a reasoning model, `/thinking low|medium|high` (or `thinking` in `settings.json`) turns
reasoning on: it streams live in the live area in its own colour and collapses to a single
`✦ thought for Ns` line when the model is done. `thinkingFull` commits the full text instead.

![Reasoning streaming and collapsing to a thought line](media/features/thinking.gif)

## Fork a session without losing the tail

`/fork` writes a new session file that continues the current one under a new id; `/clone` starts a
copy from an earlier turn. A fork carries the session's images and redacts what the source redacted,
so it resumes instead of failing on the first request.

## Find the turn you mean

`/resume` lists the session directory newest first. Once the picker is open, typing searches the past
turns themselves — not the session ids — and the rows become the turns that contain the phrase, each
labelled with its timestamp, session id and the text around the hit. Confirming a row resumes that
session at the turn: the conversation continues from there and the rest of the file stays in `/tree`.
Esc clears the query before it dismisses the picker.

`lunate sessions search <query>` runs the same search outside the session and prints the path for
each match ([Command reference](cli.md#find-a-past-session)).

## Long sessions keep their place

When a tool result would flood the window, the text is spilled to a file and the model sees a short
stub it can page back through; observation masking trims old turns without losing the transcript.
`/context` reports what each saved: `saved tokens: spills 0, masking 0, read stubs 0`.

## Notify when a long turn ends

A turn can finish while you are looking at another window. Desktop notifications are off by
default; turn them on with `notify: true` in `settings.json` or for one session with `--notify`. A
notification is sent only when the turn is at least `notifyMinSeconds` long (10 seconds by default;
`0` removes the threshold), the turn is the session's own run rather than a subagent, and the
terminal reports that its window is not focused. The notification carries the `/name` title (the
session id when unnamed) and the outcome — finished, cancelled, failed, or stopped by a length,
step, token, cost or time limit — with the elapsed time.

Focus is read from the standard xterm focus-reporting sequence, which Lunate requests when it enters
raw mode. A terminal that does not implement it, a remote session that strips the sequence, or one
that never sends a focus report leaves the state unknown, so Lunate sends nothing rather than guess.
That is the documented limit: notifications are a best-effort convenience, not a delivery guarantee.

Delivery uses the host's own tool: `osascript` on macOS, `notify-send` on Linux and a PowerShell
notification through the Windows shell's notification area. Windows would need a Start-menu shortcut
with an application identity for a Windows Runtime toast; the notification-area balloon needs none,
and Windows 10 and 11 render it as a toast. A missing tool, a refused permission or a failing
command is silent and never changes the turn, its output or its exit code. Notifications are
interactive-only; print mode, ACP and child runs never send one.

## Run limits

A run is capped by its step limit — 50 model calls by default — and can also be capped by cost and
wall-clock time, interactively and in print mode. Set the step limit in `settings.json` with
`maxSteps` (a positive integer) or per invocation with `--max-steps <n>`. The cost and wall-clock
limits are off by default; set them with `maxCost` (US dollars) and `maxTime` (seconds), or per
invocation with `--max-cost <usd>` and `--max-time <duration>` (integer seconds or a `s`, `m`, `h`
suffix).

- The step limit counts model calls in one run. A higher `--max-steps` lets a long unattended run
  pass 50 calls; it still ends with `step_limit_reached` when the configured ceiling is reached.
- The cost limit is cumulative for the session: a resumed session keeps the spend its recorded
  usages already reached, so restarting does not reset the budget.
- The wall-clock limit is per run and uses one monotonic timer.
- Either cost or wall-clock limit ends the run when the reached value equals the limit, with a
  notice that names the limit, its value and the spend or time reached. The limit is recorded in the
  session.
- A cost limit over a model the catalog cannot price refuses to start, because an unknown price is
  not `$0`; pass `--allow-unpriced` only when the limit may cover priced usage alone.

[Print mode](print.md#cap-the-runs-steps-cost-and-time) owns the exit codes a limit produces and the
budget an unattended run should carry.

## Run commands in a container

`bash` normally runs on the host. `--env podman:<image>` or `--env docker:<image>`, or the
user-scoped `environment` setting with `--env` winning, runs the command half in one session
container while `read`/`write`/`edit` stay on the host. The workspace is mounted at the same
absolute path, so file tools, approvals and change tracking keep working; the state root, install
prefix and `auth.json` are not mounted. [Run tool calls in a container](execution-environments.md)
has the full flow, the measurements and the platform limits.

## Where the other two modes live

This page describes the interactive mode (`lunate`), the TUI session with approvals, the live area
and command history ([`openspec/specs/tui/spec.md`](../openspec/specs/tui/spec.md)).
[Print mode](print.md) owns one prompt to completion, the final answer as the only thing on stdout
and the exit codes scripts branch on; [the JSON event stream](json-events.md) owns the `--json`
lines; [ACP](acp.md) owns the editor integration. [Command and option reference](cli.md) lists the
flags the modes share.

## Related

- [Quickstart](quickstart.md) — install, the setup wizard and the first task
- [Run one prompt and exit](print.md) — print mode, its exit codes and its approvals
- [Read the `--json` event stream](json-events.md) — one line per event, for scripts
- [Run Lunate inside an editor over ACP](acp.md) — the editor-driven mode
- [Command and option reference](cli.md) — every command and flag
- [Models and providers](models-and-providers.md) — choosing a provider, credential, model and profile
- [Decisions](decisions.md) — the optional second model for approvals, routing and questions
- [Copy, export and debug](export-copy-debug.md) — taking an answer, the session or a bundle out
- [Set up your terminal](terminal-setup.md) — supported terminals, shells and the narrow layout
- [Configuration](configuration.md) — decision profiles, provider entries and the rest of the file
- [Settings reference](settings.md) — every settings key, generated from the schema
- [Keybindings reference](keybindings.md) — every prompt binding, generated from the catalog
- [AGENTS.md](agents-md.md) — project rules from the root down to the launch directory
- [Skills](skills.md) — `SKILL.md` bundles a project or the user ships
- [MCP servers](mcp-servers.md) — the servers a session starts from configuration
- [Themes and keys](themes-and-keys.md) — the colour depth and the key remaps `/settings` persists
- [Run tool calls in a container](execution-environments.md) — the container mode, its settings and
  what stays on the host
- [Export contract](dev/spec/export.md) — what `/copy` and `/export` guarantee
- [The Roslyn MCP server](mcp-roslyn.md) — the C# tools from another MCP host
- [Security and safety](security.md) — the trust model behind the prompt, and what each layer does
- [Security policy](../SECURITY.md) — how to report a vulnerability
- [Troubleshooting](troubleshooting.md) — what to do when a run or a command refuses
