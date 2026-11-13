# Troubleshooting

This page answers what to do when a run will not start, a request fails or a command does not run,
and the short answer is: read the message first — it names the cause and the next step — then find
it in the table below.

## Start with the message

| What you see | Cause | Next step |
| --- | --- | --- |
| `set 'model' in ~/.lunate/settings.json` | No default model is configured | Set `model` in `settings.json`, or pass `--model` |
| `run /auth to configure a provider` | The model is known but no preset credential resolves | Run `/auth` in the session, or `lunate auth login <provider>` before starting it |
| `set <VARIABLE> to a valid key` | The variable is present but empty or unusable | Set a non-empty value, or store the key with `lunate auth login` |
| `Cannot connect to Podman` | The Podman machine is not running | `podman machine start`; on Linux start the engine's daemon or socket |
| Interactive mode exits with a hint to use `-p` | stdout is not a terminal, or `TERM=dumb` | Run [print mode](print.md) instead: `lunate -p "…"` |
| `/fork`, `/clone`, `/rewind` or `/setup` refuses | A turn is still running | Wait for the turn to finish, or press `Esc` to interrupt it |
| The run ends at its cost or time limit | `maxCost` or `maxTime` was reached | Raise the setting, or pass a larger `--max-cost` or `--max-time` |

## The first run refuses

- `set 'model' in ~/.lunate/settings.json` — no default model is configured. Set `model` or pass
  `--model`.
- `run /auth to configure a provider` — the model is known but no preset credential resolves. Run
  `/auth` in the session, or `lunate auth login <provider>` before starting it.
- `set <VARIABLE> to a valid key` — the variable is present but empty or unusable. Set a non-empty
  value or store the key with `lunate auth login`.

Those are the interactive header's fixes. Print mode has no picker: a run that cannot resolve a
credential exits 1 with the client factory's message, which names the variable and the missing key
(for example `The OPENAI_API_KEY environment variable is not set and no 'openai' key is stored. Set
OPENAI_API_KEY or add the key to auth.json.`).

## The container engine is not running

A missing engine or a stopped Podman machine is a visible failure, not a silent switch to the host.
The tool result names the engine, the image and the next step, and `/context` reports
`failed: <reason>`. For a stopped Podman machine, the engine's own message begins
`Cannot connect to Podman` and the next step is `podman machine start`; on Linux the engine's daemon
or socket is the thing to start. The command does not run on the host in either case.

## Interactive mode needs a terminal

If stdout is not a terminal or `TERM=dumb`, interactive mode exits with a hint to use `-p`; pipes,
CI and `TERM=dumb` are [print mode](print.md) only. On a Windows console without VT processing, Lunate reports
the terminal it cannot drive instead of writing control sequences into a console that will not
interpret them; use Windows Terminal, conhost or another supported terminal.

## A session command refuses

`/setup` refuses while a turn is running. Fork, clone and rewind refuse for the same reason — a
run's pending tool calls belong to a path — while `/tree` only reads and stays available. A `!`
submission while a turn runs is refused with a notice naming the turn; it is not queued.

## A run ends at its limit

Either limit ends the run when the reached value equals the limit, with a notice that names the
limit, its value and the spend or time reached. Print mode exits `3` for a cost limit and `4` for a
time limit (step and length limits stay `2`). A cost limit over a model the catalog cannot price
refuses to start, because an unknown price is not `$0`; pass `--allow-unpriced` only when the limit
may cover priced usage alone.

## Related

- [Quickstart](quickstart.md) — install, the setup wizard and the first task
- [Models and providers](models-and-providers.md) — choosing a provider, `lunate auth` and profiles
- [Run tool calls in a container](execution-environments.md) — the flags, the flow and the limits
- [Use the terminal](usage.md) — approvals, run limits and the modes
- [Settings reference](settings.md) — `maxCost`, `maxTime`, `approval` and every key
- [Run Lunate safely](security.md) — the trust model behind the approval prompts
- [Extension troubleshooting](extensions/troubleshooting.md) — failures inside an extension
