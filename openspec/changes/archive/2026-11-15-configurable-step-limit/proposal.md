## Why

Print mode stops a run at 50 model calls, and 0.4.0 offers no way to raise that ceiling: there is no
`--max-steps` flag and no step setting. Two unattended worker sessions on Lunate itself
(lunate#871, lunate#872) ended with `step_limit_reached` (`maxSteps=50`) mid-work, after about 45
tool calls, with exit 2 and no commit. A worker card routinely needs more steps, and Pi — the lane's
other harness — has no step limit at all.

## What Changes

- Add the top-level `maxSteps` settings key (a positive integer, default 50) to the settings store,
  its schema and the generated settings reference.
- Add `--max-steps <n>` to the launch parser for the interactive session, print mode and ACP,
  overriding the setting for that invocation; a value that is not a positive integer is a usage
  error (exit 2).
- Pass the resolved step ceiling into every harness the frontends compose (print, interactive and
  ACP), so the setting and the flag behave the same in print mode as interactively.
- Update the run-limits documentation (`docs/usage.md`, `docs/cli.md`, `docs/print.md`) so the
  default, the flag and the setting are named together.
- Add a print-mode test whose looping run passes 50 steps under a higher limit and stops at the
  configured one with `step_limit_reached`.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `agent-loop`: the run's step ceiling resolves from the `maxSteps` setting, overridden by
  `--max-steps`, instead of being fixed at 50; `step_limit` still ends the run when the resolved
  ceiling is reached.
- `print-mode`: `--max-steps <n>` sets the run's step ceiling for that invocation, and a value that
  is not a positive integer is a usage error (exit 2).

## Impact

- `src/Lunate.Coding/SettingsStore.cs`: the `maxSteps` key, its validation and `ApplyLaunch`.
- `src/Lunate.Coding/LaunchOptions.cs` and `RunLimits.cs`: the `--max-steps` flag and its parse.
- `src/Lunate.Coding/PrintMode.cs`, `InteractiveSession.cs` and `AcpMode.cs`: pass `MaxSteps` into
  `AgentHarnessOptions`.
- `src/Lunate.Coding/settings-schema.json` and the generated `docs/settings.md` entry.
- `src/Lunate.Coding/CliHelp.cs` and `docs/cli.md`, `docs/usage.md`, `docs/print.md`.
- Tests in `tests/Lunate.Coding.Tests` for flag parsing, settings resolution and the print-mode
  step-limit run.
- No new package, no new public API in `Lunate.Agent` (`AgentHarnessOptions.MaxSteps` already
  exists), no session-format change.
