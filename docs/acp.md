# ACP: drive Lunate from an editor

This page answers how an editor uses Lunate as its agent, and the short answer is `lunate --acp`: the
Agent Client Protocol over stdio, with the editor owning the session, the permission prompts and file
access, and Lunate serving the loop.

## Start the server

Point the editor's ACP configuration at `lunate --acp`. The editor starts the process and speaks
ACP on its stdin and stdout. Zed and JetBrains both drive agents this way; the command is the same
one you run in a terminal, so `--model`, `--extra-tools` and the other per-run flags of the
[CLI reference](cli.md) apply — `--no-extensions` included, which leaves the editor without
extension tools.

<!-- run: no the server speaks ACP on stdin and waits for an editor -->

```bash
lunate --acp --model claude-sonnet-5-5 --extra-tools grep,find
```

## Nothing but protocol on stdout

Stdout carries JSON-RPC frames and nothing else — no banner, no progress line, no warning. Every
diagnostic goes to stderr. This is the one rule that matters most here: an editor reading a stray
printable line from stdout sees a broken server.

## Let the editor own the session

| The editor sends | Lunate does |
| --- | --- |
| `initialize` | negotiates protocol version 1, announces honest capabilities, negotiates down from a higher version, and logs what it does not support instead of failing |
| a new session | builds a fresh harness for it, with the session's working directory and the client-backed approver |
| `session/prompt` | runs one prompt to completion and answers with the stop reason, streaming `session/update` notifications meanwhile |
| `session/cancel` | cancels the in-flight run; the prompt response reports the cancellation and no update follows it |

Two prompt content kinds cross the wire: text, and resource links. A `file://` link inside the
workspace becomes `@<relative path>` for the model and any other link becomes `<name> (<uri>)`, in
prompt order. Other content kinds are logged and skipped.

The mode conflicts with the other frontends: `-p`, `--json`, `--yolo` and any session selection
(`--continue`, `--resume`, `--session`, `--no-session`) are usage errors, exit `2`, and no protocol is
spoken — the editor creates the session, so selecting one is not Lunate's decision.

## Approvals ask the editor

Every non-read-only call is the editor's decision, through `session/request_permission`:

| Call | What happens |
| --- | --- |
| read-only | runs without a request |
| a file write or edit | asks, under every policy — `auto-edit` included |
| a command | asks, under every policy |

The request offers allow-once, allow-always and reject. Allow-once runs the call; allow-always runs it
and remembers the answer for that tool for the rest of the session; reject declines it as a tool error,
exactly as a local "no" would. A cancelled request or a failed round trip resolves as a decline. Every
round trip is bounded: a permission request the editor never answers declines at its timeout and the
run continues, so a stalled editor degrades instead of hanging. Cancelling the session also stops an
in-flight request promptly.

## Use the editor's buffers

When the client advertises the file-system capability, `read`, `write` and `edit` go through the
editor's `fs/read_text_file` and `fs/write_text_file`, so unsaved buffers are respected and the editor
owns byte-order marks. Without that capability the tools read and write the local disk as usual. A
missing file comes back as a not-found failure and other client failures as I/O errors — they are
never masked into one another.

## Questions never borrow the permission prompt

A question uses only a client mechanism explicitly meant for user input. If the client advertises
none, `ask_user` returns `unanswered: no user present` immediately rather than reusing
`session/request_permission`, because an answer is information and a permission is consent. No run
reads stdin waiting for a reply nobody can type.

## What the editor sees

`session/update` notifications map from the same events the TUI renders: text arrives as agent message
chunks, reasoning as agent thought chunks, and tool calls as a tool-call notification followed by
updates. Child-run events are not forwarded raw in this version, so a subagent's stream never appears
as the parent's; the approval for a child run still reaches the editor.

## Related

- [CLI reference](cli.md) — the flags `--acp` accepts and the ones it refuses
- [Print mode](print.md) — the other surface with no terminal
- [Usage](usage.md) — the interactive session, and the same approval model
- [Extension contract](dev/spec/extensibility.md) — the surfaces an extension may contribute to
