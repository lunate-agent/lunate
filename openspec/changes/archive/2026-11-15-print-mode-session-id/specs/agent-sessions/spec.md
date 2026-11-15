## ADDED Requirements

### Requirement: A caller-chosen session id

The store SHALL serve a caller-chosen session id for the frontends. `--session-id <id>` SHALL
resolve `<id>.jsonl` inside the working directory's own session directory — the same directory a
fresh run writes to — and SHALL load it when it exists and create it when it does not, with the
repository identity and worktree of a fresh session recorded in its header. The value SHALL be an id,
never a path, and SHALL match the session id grammar: 1 to 128 characters from `A-Z`, `a-z`, `0-9`,
`.`, `_` and `-`, starting with a letter or digit and without `..`. A value outside that grammar
SHALL be a usage error naming the flag and the value, before any session is created. A same-named
session in another worktree's session directory SHALL be neither loaded nor overwritten.
`--session-id` SHALL join the session selection flags: mutually exclusive with `--session`,
`--continue`, `--resume` and `--no-session`, reported as a usage error naming both flags, and a
usage error in ACP mode where the editor owns session creation. Unlike `--session <id>`, the id is
not searched across the store, so it can never be ambiguous.

#### Scenario: Creating an id that does not exist

- **GIVEN** a working directory whose session directory has no `<id>.jsonl`
- **WHEN** a run starts with `--session-id <id>`
- **THEN** `<id>.jsonl` is created in that directory with `<id>` as its header id and the working
  directory and repository identity recorded

#### Scenario: Continuing an id that exists

- **GIVEN** `<id>.jsonl` in the working directory's session directory
- **WHEN** a run starts with `--session-id <id>`
- **THEN** that file loads and its history seeds the run, with no second file and no ambiguity error

#### Scenario: Another worktree's id is not reused

- **GIVEN** a same-named session in another worktree's session directory
- **WHEN** a run starts here with `--session-id <id>`
- **THEN** the other file is untouched and this working directory creates its own `<id>.jsonl`

#### Scenario: An invalid id creates nothing

- **GIVEN** `--session-id` with a value containing a path separator, `..`, a leading `.` or a
  character outside the grammar
- **WHEN** the invocation is parsed
- **THEN** it is a usage error naming the flag and the value, and no session file is written

#### Scenario: The caller-chosen id joins the selection conflicts

- **GIVEN** `--session-id` together with `--session`, `--continue`, `--resume` or `--no-session`,
  or with `--acp`
- **WHEN** the invocation is parsed
- **THEN** it is a usage error naming the flags and no session starts
