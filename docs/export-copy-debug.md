# Copy, export and debug a session

This page answers how to take an answer, a session or a diagnostic bundle out of Lunate. The short
answer: `/copy` (or `Ctrl+X`) puts the last answer on the clipboard, `/export html|jsonl` writes the
session, and `/debug` writes one file a reporter can attach.

## Copy the last answer

`/copy` (or `Ctrl+X`) puts the last answer on the clipboard without selecting a wrapped line by
hand, and it works where the session runs: over ssh it writes the OSC 52 sequence — a local
`pbcopy` there would fill a clipboard you cannot see — and locally it runs the platform tool first,
falling back to the same sequence. The notice says only what happened:
`copied 1,842 characters via pbcopy` when a tool proved the write,
`sent 1,842 characters to the terminal clipboard (OSC 52)` when it did not, with tmux's
`set-clipboard on` requirement and the terminal's 100 KB payload limit named in the same line.

The [export contract](dev/spec/export.md#copy-transport) owns the full transport rules and the
platform tool chain.

## Export the session

`/export html` writes one self-contained document — inline CSS, no JavaScript, a
`default-src 'none'` CSP, thinking collapsed in `<details>` — that is safe to open even when a tool
result carried hostile markup. `/export jsonl` writes the session file byte for byte. Both default
to `~/.lunate/exports/<date>-<name>.<ext>` (never the current directory, where an export would be
committed), refuse an existing file without `--force`, redact every credential Lunate knows as
`[redacted:<name>]`, and print the absolute path plus a reminder that the file may contain file
contents and command output. [`spec/export.md`](dev/spec/export.md) owns the full contract.

## Write a diagnostic bundle

`/debug [path]` writes one text file a reporter can attach to an issue without explaining their
setup. Without a path it goes to
`<state root>/diagnostics/<yyyyMMdd-HHmmss-fff>-<session id>.txt`, stamped in UTC, and it never uses
the current directory. The bundle carries the header, session, state-root paths, resolved
configuration, model, extensions and the last 64 events — and never prompt text, reasoning text,
tool arguments, tool output, file contents or command output. The
[diagnostic bundle contract](dev/spec/diagnostics.md) owns what it contains, where it goes and what
it leaves out.

## Related

- [Use the terminal](usage.md) — the session these commands run in
- [Export contract](dev/spec/export.md) — destination, redaction, the HTML guarantees and copy
  transport
- [Diagnostic bundle](dev/spec/diagnostics.md) — the bundle's sections and the input trace
