# How Lunate works

This page answers what happens between your prompt and the answer, and the short answer is one loop:
the model answers, asks for tools, Lunate runs them after your approval, and the model answers again
until it needs no tool. It is the mental model only — each step has a page that owns the detail.

## One turn is a loop

You send a task. Lunate sends the conversation so far and the declarations of the tools this run may
use to the model, then streams the answer. When the answer asks for a tool — read this file, run this
command — Lunate runs it, adds the result to the conversation and asks the model again. The turn ends
when an answer needs no tool, or when the run reaches its step limit (50 by default). `Esc` cancels the
turn at any point.

The rule is the same for every tool: the model proposes, Lunate decides. A tool that fails reports
what failed and what to do next back to the model as text, not as a crash.

## The tools are the only way it acts

Four built-in tools do the work: `read`, `write`, `edit` and `bash`. In a C# workspace the model can
also call the Roslyn tools — `cs_diagnostics`, `cs_find_symbol`, `cs_find_references` and
`cs_outline` — which load on the first C# call rather than at startup. Extensions, MCP servers and
skills add tools of their own, and they go through the same approval policy as the built-ins.

Every write, edit and command stops at an approval prompt with the command or the edit's diff on
screen; a read does not. The `approval` setting decides what still asks, `--yolo` approves everything
for one run, and a run with no one to ask — print mode, CI — maps the same policy onto tool risk and
refuses what it may not approve. [Run Lunate safely](security.md) owns the trust model;
[Use the terminal](usage.md#edits-you-approve-with-the-diff) owns the panel itself.

## Context is what fits the window

The model sees the composed system prompt, your `AGENTS.md` chain, the session's messages and the
tool declarations — trimmed to fit the selected model's context window. Three mechanisms keep a long
session inside it without losing the transcript:

- A large tool result is spilled to a file, and the model gets a short stub with a pointer it can page
  back through.
- An older turn's tool result is masked behind a one-line stub, and text from a superseded read becomes
  a one-line stale notice; the session keeps the original text.
- When the estimated tokens pass 80% of the window, older turns are summarised before the request, so
  the model sees the goal, the decisions and the files touched instead of everything.

`/context` reports the tokens spills, masking and read stubs keep out of the window.
[Use the terminal](usage.md#long-sessions-keep-their-place) owns the session view of it.

## Sessions are files you can go back to

By default every run is recorded to a session file, so it survives a restart. `--continue` picks up
the newest session recorded for the working directory, `--resume` opens a picker, `--session` loads
one by id, and `--no-session` runs without writing one. Inside a session `/fork` and `/clone` branch
it without losing the tail. [Use the terminal](usage.md) owns the commands and
[the command and option reference](cli.md#session-and-per-run-options) the flags.

## Three ways to run it

| Mode | Start it with | What you get |
| --- | --- | --- |
| Interactive | `lunate` | The terminal session: prompt, streaming answer, approvals and slash commands |
| Print | `lunate -p "<task>"` | One prompt to completion, the final answer on stdout, exit codes scripts branch on; `--json` writes every event as one JSON line instead |
| Editor | `lunate --acp` | The Agent Client Protocol over stdio, driven by an editor such as Zed or JetBrains |

[Use the terminal](usage.md), [print mode](print.md), [the JSON event stream](json-events.md) and
[ACP](acp.md) each own their mode.

## Related

- [Quickstart](quickstart.md) — install, the setup wizard and the first task
- [Use the terminal](usage.md) — the interactive session, turn by turn
- [Run Lunate safely](security.md) — the trust model behind the approval prompt
- [Configuration](configuration.md) — the settings file, including the approval policy
- [Documentation home](index.md) — every task group and the page that owns it
