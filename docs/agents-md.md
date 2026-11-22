# Teach Lunate your project with AGENTS.md

This page answers how to give Lunate your repository's rules and the facts it should carry between
sessions. The short answer is two files: `AGENTS.md` holds the rules, and `LUNATE.md` holds the
notes the agent writes.

## Put the rules in AGENTS.md

Every `AGENTS.md` from the repository root down to the directory you started Lunate in is appended
to the system prompt under its relative path — the nearest file adds to the chain rather than
replacing it. There is no Lunate-specific instructions file.

## Let Lunate remember facts in LUNATE.md

`LUNATE.md` is the project's notes file. Lunate reads it from the repository root — the worktree
root for a linked worktree — even when you start in a subdirectory, and appends it to the system
prompt after the `AGENTS.md` chain. `AGENTS.md` comes first and its rules win if a note disagrees:
notes are facts from earlier sessions, not instructions.

The model appends one note at a time with the `remember` tool, for example "this project builds with
.NET 11" or "the client prefers tabs". `remember` takes no path, so it always writes the project's
own file, never a global or user-level notes file. It adds a Markdown bullet and leaves every
existing byte alone; the first note creates the `# Project notes` heading. The write appears in the
transcript like any other tool call, and the next session reads it from the system prompt.

`LUNATE.md` holds at most 8,000 characters. Above that the system prompt carries the first 8,000
characters and a marker naming how many were omitted, so the file can be opened for the rest. An
append that would exceed the budget is refused and the file is left unchanged; shorten the note, or
use `edit` to remove a stale one.

A repository without `LUNATE.md` composes exactly as before, and a note that is not needed yet can
wait — the file is ordinary Markdown you can also edit by hand.

## Know what the model receives

Their content is appended at runtime beside the OS, shell, working directory and date. Project
rules come from `AGENTS.md`, not from the harness, so the same file serves any coding agent that
reads the repository; the notes stay Lunate's own.

## Related

- [Skills](skills.md) — reusable instructions a project or the user ships as `SKILL.md`
- [MCP servers](mcp-servers.md) — tools the session starts from configuration
- [Settings reference](settings.md) — every settings key, generated from the schema
- [Usage](usage.md) — the interactive session and its turns
