# CI and unattended runs

This page answers how to run Lunate where nobody can answer a prompt, and the short answer is three
steps: `lunate setup` with flags to configure the machine, `lunate trust --yes --digest <sha>` to
approve the repository's own code, and `lunate -p` to do the work — all of it with a state root you
chose and a dollar limit you set.

## Configure a machine without a terminal

`lunate setup` runs the setup wizard without a TTY. Every answer is a flag; an absent flag skips its
step and invents nothing. The connection test and the model check run unless suppressed: `--no-test`
sends no provider request at all (it skips both), while `--no-model-check` skips only the paid model
check and keeps the free discovery request. The summary goes to stdout and never contains a
credential.

<!-- run: offline -->

```bash
OPENAI_API_KEY="sk-ci-example"   # any non-empty value: --no-test sends no provider request
printf '%s\n' "$OPENAI_API_KEY" |
  lunate setup \
    --provider openai --credential env:OPENAI_API_KEY \
    --model gpt-5 --profile chat-main --thinking medium --no-test
```

The flag set is `--provider`, `--endpoint`, `--api-family`, `--credential-placement`,
`--models-url`, `--credential` (an `env:`/`auth:` reference), `--key-stdin`, `--model`, `--profile`,
`--thinking`, `--fast`, `--as-decision`, `--no-test`, `--no-model-check` and `--force`. A run without
a terminal exits `2` and names the missing answer when a value needs interaction; `0` after a real
commit or a no-op; `1` for a store/load failure.

A `--thinking` level needs a model whose catalog entry declares a reasoning style: a model that has
none refuses the flag by name and names the setting, which is why the example above names `gpt-5` and
not a plain chat model.

## Approve repository code by digest

A repository can carry an extension, a project `mcp.json` server and a skill, and each is code from
that repository. `lunate trust` answers them without a panel:

<!-- run: no needs a repository with a pending trust decision, and a digest from its own output -->

```bash
lunate trust --json                       # inspect; exit 3 while a decision is pending
lunate trust --yes --digest <sha>         # approve exactly the decision that digest names
```

- `--json` prints the same records as a machine-readable object for a script to read.
- `--yes` on its own is a person's answer typed at a terminal, and it refuses to record anything from
  a script or a closed stdin.
- `--yes --digest <sha>` is the reviewed non-interactive path: it records exactly the decision the
  digest names, so a CI run approves what a human reviewed and nothing else.
- `--clear` removes this repository's records; revoking trust escalates nothing, so it needs no
  digest.
- Exit codes are `0` for success, `1` for an unexpected failure, `2` for a usage error, `3` when the
  inspect found pending decisions and `4` when a digest did not match.

Neither `lunate trust --yes` nor `lunate install` runs from inside a Lunate session: the bash tool's
children carry `LUNATE_SESSION=1`, and approving from inside the session would be the model answering
its own question. Clear it (`env -u LUNATE_SESSION …`) and the command works again, because the
marker is a visibility guard, not a lock.

## Run the work unattended

`lunate -p "…"` is the run itself. Three things make it safe in a pipeline:

- **No prompt, ever.** No credential prompt, no setup wizard, no approval panel, no question. A
  missing credential exits `1` with the message naming the environment variable.
- **Approvals follow the policy.** `ask` allows read-only tools and declines writes and commands;
  `auto-edit` also allows writes and edits. `--yolo` allows everything and prints a line saying so —
  it removes the only boundary between the model and the runner, so pair it with the container below
  or do not use it.
- **A budget and an exit code.** `--max-cost <usd>` and `--max-time <duration>` end a run that
  overspends or overruns, and the exit code says which limit fired.
  [Print mode](print.md#read-the-exit-code) owns the code table, and
  [JSON events](json-events.md) is the stream a job log should keep.

For a `--yolo` run, or any run in a repository you do not trust, put the command half in a container:
[Run tool calls in a container](execution-environments.md) has the flag, the settings key, what is
mounted and the measured cost.

## Keep the state where the runner expects it

Set `LUNATE_HOME` to a directory in the job's workspace before anything else. It moves sessions,
extensions, the credential store, the trust store and recordings together, refuses a path inside the
workspace under test, and prints in `lunate --version` so a log shows which root a run used. With
`CI` set, the daily update check stays off and the setup wizard never opens.
[Environment variables](environment-variables.md) has both.

## Related

- [Print mode](print.md) — the run, its approval mapping and its exit codes
- [JSON events](json-events.md) — what to keep when the run happens in a job
- [First run](first-run.md) — the same setup wizard with a person at the terminal
- [Run tool calls in a container](execution-environments.md) — the confinement an unattended run wants
- [Environment variables](environment-variables.md) — `LUNATE_HOME`, `CI` and the credential variables
- [CLI reference](cli.md) — every flag these commands accept
