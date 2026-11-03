## ADDED Requirements

### Requirement: Setup command line

`lunate setup` SHALL run the setup wizard: interactively through the same nine-step flow an
interactive first run opens, and non-interactively for scripts and CI through explicit flags —
`--provider <preset-id>`, `--endpoint <url>` (custom providers), `--credential <env>:<VARIABLE>|auth:
<key>`, `--key <value on stdin with --key-stdin>`, `--model <id>`, `--profile <name>`,
`--thinking <off|low|medium|high>`, `--as-decision` (binds the written profile to `decisions.profile`
in place of `chat`), `--fast` (adds the written profile as the `fast` usage), and `--force` (an
existing reference value is replaced). Flags absent from one run mean that step is skipped, exactly
as skipping works interactively; no flag invents a value. A non-interactive run with no terminal
SHALL never wait for input: anything needing interaction exits 2 with the missing answer named.
The command SHALL make the connection test's discovery call unless `--no-test` is given, and the
summary SHALL print stdout as one human-readable block (machine-readable only via exit codes). After
the summary-equivalent all flags agree, the command SHALL commit the collected writes through the
same store writers as the interactive wizard and SHALL print what was written, never a credential.
`lunate setup` on a terminal with no flags and existing, resolvable configuration SHALL still open
the wizard (re-running is always allowed); when no configuration exists, bare `lunate` and
`lunate setup` behave identically.

#### Scenario: A CI script configures a provider without a TTY

- **GIVEN** no terminal, no stored configuration, and `--key-stdin` piped
- **WHEN** `lunate setup --provider openrouter --model deep/deepseek-chat --profile main --key-stdin`
  runs
- **THEN** the run exits 0, the provider entry, profile and `chat` usage are stored through the
  settings writers, and the key text appears in no output

#### Scenario: Flags skip steps the way Esc skips them

- **GIVEN** a non-interactive run without `--model`
- **WHEN** the flow completes
- **THEN** no model value is invented, the summary names the model step skipped, and the commit
  writes the provider and credential only

#### Scenario: A value that needs interaction exits instead of hanging

- **GIVEN** stdin is not a terminal and no `--credential` is given
- **WHEN** `lunate setup --provider openai --model gpt-5` runs
- **THEN** it exits 2 and names the credential as the answer only interaction can supply

#### Scenario: Re-running setup is allowed

- **GIVEN** a fully configured machine
- **WHEN** `lunate setup` runs on a terminal
- **THEN** the wizard opens, the existing values show as the current ones, and confirming without
  changes rewrites nothing

### Requirement: The configured-state check

The wizard trigger and `lunate setup` SHALL share one configured-state check: the user provider store
has no provider entry, or the `chat` usage has no resolvable profile (`usages.chat` names no
configured profile and no `model` default exists). The check SHALL agree with the welcome header's
first-run state — when the check says the wizard applies, the header names no fix, and when it says
the configuration resolves, the header's normal block renders — and the check result SHALL be
reproducible from configuration alone: no network call, no clock, no environment probe beyond the
credential resolution the client factory already performs.

#### Scenario: One check answers both entry points

- **GIVEN** a machine with one provider entry but no `chat` usage
- **WHEN** the check runs for a session start and for `lunate setup`
- **THEN** both report the wizard applies for the same reason: no chat profile
