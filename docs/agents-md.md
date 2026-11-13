# Teach Lunate your project with AGENTS.md

This page answers how to give Lunate your repository's rules, and the short answer is an `AGENTS.md`
file: every one from the repository root down to the directory you started Lunate in is appended to
the system prompt, so the rules travel with the code.

## Put the rules in AGENTS.md

Every `AGENTS.md` from the repository root down to the directory you started Lunate in is appended
to the system prompt under its relative path — the nearest file adds to the chain rather than
replacing it. There is no Lunate-specific instructions file.

## Know what the model receives

Their content is appended at runtime beside the OS, shell, working directory and date. Project
rules come from `AGENTS.md`, not from the harness, so the same file serves any coding agent that
reads the repository.

## Related

- [Skills](skills.md) — reusable instructions a project or the user ships as `SKILL.md`
- [MCP servers](mcp-servers.md) — tools the session starts from configuration
- [Settings reference](settings.md) — every settings key, generated from the schema
- [Usage](usage.md) — the interactive session and its turns
