## Context

`LaunchOptions.TryParse` (`src/Lunate.Coding/LaunchOptions.cs`) is the one launch parser for the
interactive session, print mode and ACP. It walks the tokens once: it splits an inline
`--flag=value`, matches a flag, rejects any other token that starts with `-` as an unknown option,
and collects the rest as positionals. Print mode requires exactly one positional
(`print mode needs one prompt` / `print mode takes exactly one prompt`), and interactive mode refuses
any positional. `Cli.Run` runs two scans over the raw `args` before it reaches the parser:
`args.Contains("--acp")` chooses ACP mode and `args.Contains("--no-extensions")` strips the flag
before any mode dispatch.

Nothing in that path understands `--`, so it falls into the unknown-option branch
(`lunate: unknown option '--'`).

## Goals / Non-Goals

**Goals:**

- `lunate -p -- "<prompt>"` (and with `--json`) sends everything after `--` as the prompt, so a
  prompt that starts with `-` is never read as a flag.
- `--` with no prompt after it keeps the existing missing-prompt usage error, exit 2.
- A token after `--` cannot select a mode through the pre-dispatch scans.

**Non-Goals:**

- No change to the `--flag=value` escape for option values, and no change to the empty-prompt rule.
- No new flag, no settings key, no public API, no session-format change.
- No escaping for the subcommands (`trust`, `auth`, `install`, …); the card is about the launch
  parser that print mode uses.

## Decisions

### 1. Handle the marker inside `LaunchOptions.TryParse`, not by pre-splitting `args` in `Cli`

The parser owns flag meaning (design D1 in `docs/dev`), and ACP reaches it through the same call
after `--acp` is removed. A pre-split in `Cli` would have to be repeated for ACP and would leave the
parser still refusing a bare `--` when called directly (as the tests do). The marker check sits at
the top of the loop, before the inline `=` split: the first `--` ends option parsing, and every
later token becomes a positional without any flag or inline-value inspection. A second `--` is
therefore an ordinary positional (the prompt `--`), and a token before the marker stays positional —
so `-p one -- two` keeps failing with the existing "print mode takes exactly one prompt".

Alternative rejected: treating `--` as a no-op token anywhere. That would make `--acp -- --json`
parse `--json` as a flag again, which is the ambiguity the marker exists to remove.

### 2. A missing prompt after the marker is the existing usage error

`-p --` collects no positional, so the existing `print mode needs one prompt` branch fires before
any mode runs: exit 2, message on stderr, nothing on stdout. No new error text and no new path. An
explicit empty token (`-p -- ""`) still hits the existing `the prompt cannot be empty` rule.

### 3. The pre-dispatch scans stop at the marker

`Cli.Run` must not let a prompt hijack the mode: `args.Contains("--acp")` would otherwise start ACP
mode for `lunate -p -- "--acp"`, and the `--no-extensions` strip would silently delete those words
from a prompt. Both scans take the index of the first `--` and only inspect tokens before it; the
token removal keeps everything at or after the marker untouched. This is a one-line helper beside
the existing dispatch, not a second parser.

### 4. Documentation and help name the marker

`CliHelp` gains a synopsis line, and `docs/cli.md` and `docs/print.md` name `--` in their prose. The
`docs/docs_check/cli_options.py` comparison matches long options (`--` plus a lowercase name), so a
bare `--` is outside its both-directions option set and the help/page comparison stays green.

## Risks / Trade-offs

- [A caller now passes `--` twice] -> the second `--` is the prompt, which is exactly what a caller
  writing `-- --` asks for; covered by a parser test.
- [An interactive run with `--` and a token] -> the existing "interactive mode takes flags only"
  usage error still fires, so the marker adds no silent acceptance in interactive mode.
- [Docs check drift] -> the help line and the two docs pages are checked together by
  `scripts/docs-check`; the marker is bare `--`, outside the long-option set.

## Migration Plan

Additive. `lunate -p "<prompt>"` behaves exactly as before; `--` was a usage error, so no working
invocation changes meaning. Rollback is reverting the parser case and the two scans.

## Open Questions

None. The card's acceptance decides the whole surface; the choices above follow the repository's
existing one-parser shape and the conventional end-of-options meaning.
