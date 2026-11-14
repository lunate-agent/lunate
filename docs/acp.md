# Run Lunate inside an editor over ACP

This page answers how an editor drives Lunate as its agent, and the short answer is `lunate --acp`: the
Agent Client Protocol over stdio, so Zed or JetBrains start Lunate the way they start any other agent,
with nothing but protocol on stdout.

## Start it from the editor

`lunate --acp` serves the Agent Client Protocol over stdio. Register it as the editor's agent command
and let the editor start it from the repository root; the editor owns the protocol connection, so
Lunate writes protocol frames to stdout and nothing else. Logs go to stderr or `~/.lunate/logs/`.

<!-- run: no serves a protocol session on stdio -->
```bash
lunate --acp
```

## What the editor gets

The server implements the protocol's initialize, session, prompt and cancel paths, and reports honest
capabilities: a requested version above 1 negotiates down, and an unsupported capability is logged,
not fatal.

- **Sessions** — the editor creates them, and each ACP session gets a fresh harness. The factory
  receives the session context: the working directory, a client-backed approver, and the client's file
  access when the editor offers it.
- **Prompts** — one `session/prompt` is one run. Text and resource-link content blocks are accepted;
  a resource link maps to model-readable text, and any other content kind is logged and skipped.
- **Streaming updates** — `session/update` notifications are mapped from the run's events, so agent
  message chunks and agent thought chunks stream into the editor, and a tool call arrives as a tool
  call plus its update. The prompt response carries the run's stop reason.
- **Permissions** — the editor's permission prompt is the approval panel: editor file reads, writes
  and permission prompts map onto Lunate's tools and approval policy through the client-backed
  approver.
- **Cancel** — `session/cancel` cancels the in-flight run, the prompt response reports the
  cancellation, and no updates follow it.
- **Child runs** — a subagent's events are not passed to the client raw; the server filters them out
  of `session/update` in this version, while approvals still surface through the approver.

## Expect these limits

- ACP is mutually exclusive with print mode and the TUI; combining them is a usage error (exit 2).
- The editor owns session creation, so the session selection flags (`--continue`, `--resume`,
  `--session`, `--no-session`) are usage errors in ACP mode.
- Nothing may write to stdout except the protocol. A diagnostic, a notice or a stray `Console.WriteLine`
  would corrupt the connection, so everything else goes to stderr.
- Print mode, ACP and runs with piped stdin never prompt for a credential: a missing one stays the
  explicit error naming the key and the environment variable.
- `/copy` and `/export` do not exist in print or ACP mode; the editor owns the transcript.
- A project extension, `mcp.json` server or skill that still needs a trust answer is refused and named;
  print and ACP sessions never auto-approve repository code.

## Related

- [Run one prompt and exit](print.md) — the other non-interactive mode, and what it does with an
  approval
- [Set up your terminal](terminal-setup.md) — which terminals suit the TUI and which suit an editor
- [Extension lifecycle and trust](extensions/lifecycle-and-trust.md) — what ACP refuses without an
  answer
- [MCP servers](mcp-servers.md) — the servers a session starts beside the editor's own tools
