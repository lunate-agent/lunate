# Run Lunate unattended in CI

This page answers how to set Lunate up and run it from a script or a pipeline, and the short answer
is three commands with no terminal anywhere: `lunate setup` with every answer as a flag,
`lunate trust --yes --digest <sha>` for a repository you control, and `lunate -p` for the task itself.

## Set it up without a terminal

`lunate setup` runs the same walk as the wizard without a TTY. Every answer is a flag; an absent flag
skips its step and invents nothing. The connection test and the model check run unless suppressed:
`--no-test` sends no provider request at all (it skips both), while `--no-model-check` skips only the
paid model check and keeps the free discovery request. The file manifest and the post-write receipt
go to stdout and never contain a credential.

<!-- run: offline -->
```bash
OPENAI_API_KEY="sk-ci-example" \
  lunate setup \
    --provider openai --credential env:OPENAI_API_KEY \
    --model gpt-5 --profile chat-main --thinking medium --no-test
```

`gpt-5` declares a reasoning style, so `--thinking` applies; a model that declares none refuses a
level. The placeholder key is never sent, because `--no-test` suppresses both checks.

The flag set is `--provider`, `--endpoint`, `--api-family`, `--credential-placement`,
`--models-url`, `--credential` (an `env:`/`auth:` reference), `--key-stdin`, `--model`, `--profile`,
`--thinking`, `--fast`, `--as-decision`, `--no-test` (offline: skips the connection test and the
model check, so no provider request is sent), `--no-model-check` (skips the model check only), and
`--force`. A run without a terminal exits 2 and names the missing answer when a value needs
interaction; 0 after a real commit or a no-op; 1 for a store/load failure.

An environment credential always wins over a stored key, so a pipeline sets the provider's variable
(`OPENAI_API_KEY`, `ANTHROPIC_API_KEY`, …) instead of writing `auth.json`. [Models and
providers](models-and-providers.md) owns the credential rules; [the provider
reference](providers.md) lists each preset's variable.

## Record trust once, by digest

A project extension, a project `mcp.json` server and a project skill are code from the repository, so
a session asks once per repository before it loads them. No unattended run can answer that question:
print and ACP sessions never auto-approve repository code, they refuse the load and log
`project extension '<id>' was not trusted; run an interactive session or 'lunate trust' to approve it`.

The scripted path records the decision without a terminal, and it records only the exact decision
being approved:

<!-- run: no needs a repository's pending trust record -->
```bash
lunate trust --yes --digest <sha>
```

- `lunate trust` lists the records and pending decisions and exits 3 when something is pending, so a
  pipeline can use it as a gate before the run.
- A bare `--yes` needs a terminal at stdin; `--yes --digest <sha>` is the non-interactive form and
  exits 4 when the pinned digest does not match.
- `--clear` works everywhere, because revoking escalates nothing.
- Use this for checkouts you control, never for an untrusted pull request: `--yes` records exactly
  what it prints.

[Extension lifecycle and trust](extensions/lifecycle-and-trust.md) owns what a record covers and what
it does not; `LUNATE_SESSION` marks a command started from inside a session, which is why an install
or a `trust --yes` from there says so.

## Run the task

`lunate -p "<prompt>"` is the mode for a pipeline: one prompt to completion, the answer on stdout,
diagnostics on stderr, and exit codes a script can branch on. Set a budget so an unattended run cannot
spend without saying so, and read the JSON stream when a later step needs the details:

<!-- run: no needs a model request -->
```bash
lunate -p --max-cost 2 --json "summarise this diff" > events.jsonl
```

- [Print mode](print.md) owns the answer contract, the approval mapping and the exit codes.
- [The JSON event stream](json-events.md) owns the line shapes.
- `--offline` skips the daily update check; print mode, ACP and CI never run it anyway
  ([configuration](configuration.md)).
- Unattended runs never wait on a person: `ask_user` returns `unanswered: no user present` and the
  model continues on a stated assumption.

## Related

- [Quickstart](quickstart.md) — install and the interactive first run this page scripts
- [Run one prompt and exit](print.md) — the run itself
- [The JSON event stream](json-events.md) — a machine-readable run
- [Extension lifecycle and trust](extensions/lifecycle-and-trust.md) — what `lunate trust` records
- [Installing packages](extensions/packages.md) — a vendored install and the `--yes` rule
- [Command and option reference](cli.md) — every flag named here
