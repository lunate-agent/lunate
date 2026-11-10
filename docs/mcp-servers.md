# Connect MCP servers to a session

This page answers how Lunate consumes Model Context Protocol servers, and the short answer is an
`mcpServers` map: servers start on first use, their tools arrive as `server__tool`, and a project
file needs a trust decision like any other repository code.

## Know where servers are configured

`mcpServers` maps in the user files `~/.lunate/mcp.json` and `~/.agents/mcp.json`, and in the
project files `.lunate/mcp.json`, `.agents/mcp.json` and `.mcp.json`. An entry carries `command`,
`args`, `env`, optional `cwd` and optional `callTimeoutSeconds`; `${VAR}` and `${VAR:-default}`
expand when the file loads. Within a scope `.lunate/` wins over `.agents/` wins over `.mcp.json`,
and a project file wins over a user file. Servers start on first use, their tools arrive as
`server__tool`, and a project file and each of its servers needs its own trust decision.

## Know what the child process inherits

A stdio server is a child process and inherits Lunate's environment, so existing MCP setups keep
working — with one exception: the credentials Lunate itself knows are withheld from that child.
The provider keys the model presets and the provider store reference, the keys in `auth.json`,
the extension secrets and the `env` values the configured entries set are removed from the child's
environment (the variables are gone, not emptied), unless the server's own `env` map names the
variable explicitly; `${VAR}` in a declared value still resolves from Lunate's environment.

## Serve Lunate's own tools instead

Lunate can also be the server: `lunate mcp roslyn` serves the five read-only `cs_*` C# tools over
stdio to another MCP host, with no approvals or write path behind them.
[`mcp-roslyn.md`](mcp-roslyn.md) covers the configuration and what it leaves out by design.

## Related

- [The Roslyn MCP server](mcp-roslyn.md) — Lunate as the server, and what it leaves out
- [MCP servers from an extension](extensions/contributions/mcp-servers.md) — a server an extension
  contributes
- [Skills](skills.md) — instruction bundles beside the tools
- [Project instructions](agents-md.md) — the rules in `AGENTS.md`
- [Usage](usage.md#repository-code-asks-once) — the trust prompt a project file needs
- [Security policy](../SECURITY.md) — the full trust model and how to report
