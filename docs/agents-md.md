# Teach Lunate your project with AGENTS.md

This page answers how to give Lunate your repository's rules, and the short answer is an `AGENTS.md`
file: every one from the repository root down to the directory you started Lunate in is appended to
the system prompt, so the rules travel with the code.

## Put the rules in AGENTS.md

Every `AGENTS.md` from the repository root down to the directory you started Lunate in is appended
to the system prompt under its relative path — the nearest file adds to the chain rather than
replacing it. There is no Lunate-specific instructions file.

`AGENTS.md` is the rule file any coding agent can read, so the rules stay with the repository
instead of with one harness.

## Know what the model receives

The files' content is appended at runtime beside the OS, shell, working directory and date. Project
rules come from `AGENTS.md`, not from the harness.

## Related

- [Skills](skills.md) — reusable instructions a project or the user ships as `SKILL.md`
- [MCP servers](mcp-servers.md) — tools the session starts from configuration
- [Configuration](configuration.md) — every settings key, generated from the schema
- [Usage](usage.md) — the interactive session and its turns
