# Print mode

This page answers how to run Lunate from a script, and the short answer is `lunate -p "<prompt>"`: one
prompt to completion, the final answer as the only thing on stdout, diagnostics on stderr, and an exit
code a shell can branch on. Nothing prompts, because nobody is there to answer.

## Run one prompt to completion

<!-- run: no a real run needs a provider credential and a paid model call -->

```bash
lunate -p "why does the build fail in src/Lunate.Agent?"
```

The run gets the same tools, approvals-as-policy, session file and decision layer as the interactive
session; it just has no input line. Exactly the final answer — the text of the last assistant message
with content — is written to stdout, once, at the end. Everything else goes to stderr, including the
skills-index notice and the description-truncation warning (`lunate: skills: …`), so
`lunate -p "…" > answer.txt` captures a clean answer.

What does not exist in print mode: the `/` commands, the `!` bash line, the message queue, `--resume`
(the interactive picker; use `--session <id|path>` or `--continue`), and any prompt — credential,
setup wizard, trust or approval.

## Read the exit code

| Code | Meaning |
| --- | --- |
| `0` | the run finished with the `stop` reason |
| `1` | a run error, or a failure before a clean finish |
| `2` | the run ended at its step limit or the model's length limit — or the invocation was a usage error (unknown flag, missing prompt, unresolvable session) |
| `3` | the run reached its cost limit |
| `4` | the run reached its wall-clock limit |
| `130` | the run was cancelled |

A usage error writes its message to stderr and nothing to stdout. An unpriced model under a cost limit
is a launch refusal (`2`) naming the model and `--allow-unpriced`, before any model request.

## Approvals without a panel

There is no panel to answer, so the resolved `approval` policy is applied to tool risk, and the run
never approves beyond it:

| Policy | Read-only tools | Writes and edits | Commands |
| --- | --- | --- | --- |
| `ask` (default) | allowed | declined | declined |
| `auto-edit` | allowed | allowed | declined |
| `--yolo` | allowed | allowed | allowed |

A policy never allows a command without a panel — not the present one and not a future one — and a
call that changes trust or writes the state root is declined by any policy. `--yolo` is the explicit
per-run exception that removes the boundary altogether; the run says so at startup, and it can never
be stored as a setting. A declined call becomes an error result the model can read, plus one stderr
line naming the tool and the policy.

The interactive tracked-file refinement of `ask` does not apply here; this is the approximation print
mode documents.

## Nothing waits for a user

When the model asks a question, print mode returns `unanswered: no user present` immediately and tells
the model to state an assumption and continue. An MCP server's form elicitation is cancelled at once
and the same unanswered outcome is recorded in the enclosing tool result. No run reads stdin.

A missing credential is not a prompt either: the run exits `1` with the client factory's message,
which names the environment variable and the missing key, for example `The OPENAI_API_KEY environment
variable is not set and no 'openai' key is stored. Set OPENAI_API_KEY or add the key to auth.json.`

## Cap the run

Print mode is usually unattended, so set a budget; `lunate -p --max-cost 2 "…"` is a run that cannot
exceed two dollars without saying so.

- `--max-cost <usd>` caps the session's cumulative spend. A resumed session keeps the spend its
  recorded usages already reached, so restarting does not reset the budget.
- `--max-time <duration>` caps one run's wall-clock time — integer seconds, or a `s`, `m` or `h`
  suffix.
- Either limit ends the run when the reached value equals the limit, records the limit in the
  session, and exits `3` (cost) or `4` (time) with a stderr line naming the limit and the value
  reached.
- A cost limit over a model the catalog cannot price refuses to start, because an unknown price is
  not `$0`; `--allow-unpriced` permits it when the limit may cover priced usage alone.

`maxCost` and `maxTime` in `settings.json` set the same limits for every run; the flags win for one
invocation. [Settings reference](settings.md) has the keys.

## Take the event stream instead of the answer

`--json` replaces the answer on stdout with one JSON object per event, in arrival order, for scripts
and CI that need the tool calls, usage and stop reason rather than the prose.
[JSON events](json-events.md) owns that format.

## Related

- [JSON events](json-events.md) — the `--json` stream
- [CI and unattended runs](ci.md) — the scripted setup and trust that a print run needs first
- [ACP](acp.md) — the other surface that is not a terminal
- [CLI reference](cli.md) — every flag, and the exit codes all commands share
- [Usage](usage.md) — the interactive session this one leaves out
