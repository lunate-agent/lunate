## Why

Print mode refuses the conventional end-of-options marker: `lunate -p -- "prompt"` exits 2 with
`lunate: unknown option '--'`. A caller with arbitrary prompts therefore has no safe form — a prompt
that starts with `-` is read as a flag — while Pi, the lane's other harness, accepts `--` before the
prompt for exactly that reason (lunate#869).

## What Changes

- The launch parser accepts `--` as the end of options: every token after it is positional, never a
  flag, so `lunate -p -- "--not-a-flag"` sends the prompt `--not-a-flag` in print mode and with
  `--json`.
- `--` with no prompt keeps the existing missing-prompt usage error (exit 2), and interactive mode
  still refuses a positional argument.
- The two scans that run before the launch parser (`--acp`, `--no-extensions`) look only at tokens
  before `--`, so a prompt that contains one of them cannot change the mode.
- `lunate --help`, `docs/cli.md` and `docs/print.md` name the marker.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `print-mode`: the launch arguments accept `--` as the end of options; everything after it is the
  print-mode prompt, and `--` without a prompt is the usual missing-prompt usage error.

## Impact

- `src/Lunate.Coding/LaunchOptions.cs`: the `--` case in the one launch parser.
- `src/Lunate.Coding/Cli.cs`: the `--acp` and `--no-extensions` pre-scans become marker-aware.
- `src/Lunate.Coding/CliHelp.cs`, `docs/cli.md`, `docs/print.md`.
- Tests in `tests/Lunate.Coding.Tests/CliTests.cs`.
- No new package, no public API change, no session-format change.
