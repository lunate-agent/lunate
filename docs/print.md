# Run one prompt and exit

This page answers how to run Lunate without a terminal, and the short answer is
`lunate -p "<prompt>"` plus `--json`: one prompt to completion, the final answer as the only thing on
stdout, diagnostics on stderr, and exit codes a script can branch on.

## Send one prompt

Print mode runs one prompt to completion without any interactive prompt. In default mode exactly the
final answer — the text of the last assistant message with content — is written to stdout, once at
the end; all diagnostics go to stderr, so redirecting stdout captures a clean answer.

<!-- run: no needs a model request -->
```bash
lunate -p "summarise what changed on this branch"
```

`--json` writes every event of the run as one JSON line instead of the answer;
[the JSON event stream](json-events.md) owns that shape. A missing prompt, an unknown flag, an
unknown `--model` or a print-mode session selection that does not resolve is a usage error: exit 2, a
message on stderr and nothing on stdout. `--resume` is refused for the same reason — the session
picker cannot be drawn — so name the session with `--session` or `--continue` instead.

Print mode is also where a non-interactive run that was started another way ends up: a run with piped
stdin never prompts, and `TERM=dumb` without a terminal is print mode only
([terminal setup](terminal-setup.md)).

## Read the exit code

| Code | Meaning |
| --- | --- |
| 0 | the run finished with the `stop` reason |
| 1 | a run error, or a failure before a clean finish |
| 2 | a `length` or `step_limit` finish, and every usage error |
| 3 | a `cost_limit` finish |
| 4 | a `time_limit` finish |
| 130 | the run was cancelled |

## Approve tool calls without a person

Nobody can answer the approval panel in print mode, so the resolved `approval` policy maps onto tool
risk: `ask` (the default) allows read-only tools and denies file writes and commands, `auto-edit` also
allows file writes and edits and denies commands, and the per-run `--yolo` flag allows everything. The
interactive tracked-file refinement of the `ask` level is a TUI rule; print mode's approximation is
the mapping in the table.

| Approval | Read-only tools | Writes and edits | Commands |
| --- | --- | --- | --- |
| `ask` (default) | allowed | denied | denied |
| `auto-edit` | allowed | allowed | denied |
| `--yolo` | allowed | allowed | allowed |

A denied call becomes an error tool result the model can read, the file or command is untouched, and
one diagnostic line on stderr names the tool and the policy. A call that changes trust or writes the
state root is never approved silently: a run with no panel declines it. `yolo` is not a setting —
only the per-run flag exists, and a settings file that carries it fails validation.

## Answer questions without a user

Print mode and unattended runs return `unanswered: no user present` from `ask_user` immediately when
no user-interaction mechanism is available, and MCP form elicitation in those runs is cancelled
immediately with the same unanswered outcome in the tool result. The result directs the model to
continue on an explicitly stated assumption; nothing waits on stdin and the stdout contract is
unchanged.

## Cap the run's cost and time

A run can be capped by cost and wall-clock time as well as by its step limit, and print mode is
usually unattended, so set a cost limit there: `lunate -p --max-cost 2 "…"` is a budget the run cannot
exceed without saying so. `--max-cost <usd>` and `--max-time <duration>` set the run's limits for that
invocation, overriding the corresponding settings; `--max-time` accepts integer seconds or a `s`, `m`,
`h` suffix. The limits themselves are on [Run limits](usage.md#run-limits).

- A cost limit over a model the catalog cannot price refuses to start, because an unknown price is
  not `$0`; pass `--allow-unpriced` only when the limit may cover priced usage alone.
- Either limit ends the run when the reached value equals the limit, with a notice that names the
  limit, its value and the spend or time reached. The limit is recorded in the session.
- Print mode exits `3` for a cost limit and `4` for a time limit.

## Related

- [The JSON event stream](json-events.md) — one line per event, for scripts
- [Run Lunate inside an editor over ACP](acp.md) — the editor-driven mode
- [Scripted setup, trust and unattended runs](ci.md) — the CI story around print mode
- [Run limits](usage.md#run-limits) — the same budget inside a session
- [Models and providers](models-and-providers.md) — which models carry a price
- [Troubleshooting](troubleshooting.md) — what a refused print run means
