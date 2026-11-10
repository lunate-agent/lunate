# Load skills a project or you ship

This page answers how Lunate finds and loads Agent Skills, and the short answer is a directory with a
`SKILL.md`: Lunate indexes the skills it finds, lists them with `/skills` and loads one with
`/skill:<name>`.

## Know where Lunate looks

A skill is a directory with a `SKILL.md`. Project scope: `.lunate/skills`, `.agents/skills`,
`.claude/skills`; user scope: `~/.lunate/skills`, `~/.agents/skills`. The nearest scope wins.

## Load one

`/skills` lists them, `/skill:<name>` loads one, and the model gets an index of the rest — one
line per skill, its description carried whole to the Agent Skills limit of 1,024 characters
(a longer description is truncated in the index with one warning, and the skill still loads),
sized by the model's context
window: a one-time notice above the 2% soft threshold, a cut-from-the-end overflow above the 5%
hard ceiling. `skills.indexBudget` overrides both.

## Trust a project skill

Project skills need a trust decision like any other repository code. Answers are recorded in
`~/.lunate/trust.json`; [Usage](usage.md#repository-code-asks-once) has the prompt `lunate trust`
records for scripted runs.

## Related

- [Project instructions](agents-md.md) — the rules in `AGENTS.md`
- [MCP servers](mcp-servers.md) — tools the session starts from configuration
- [Skill sources in extensions](extensions/contributions/skill-sources.md) — shipping skills inside an
  extension
- [Configuration](configuration.md) — `skills.indexBudget` and every other key
