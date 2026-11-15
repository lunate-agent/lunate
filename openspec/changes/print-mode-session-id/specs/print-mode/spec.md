## ADDED Requirements

### Requirement: Caller-chosen session id

Print mode SHALL accept `--session-id <id>`. It SHALL resolve `<id>.jsonl` in the working
directory's session directory: when that file exists it SHALL load it and continue its history, and
when it does not it SHALL create a session whose header id is `<id>`, recording the launch prompt
directives as a fresh run does. The chosen id SHALL be the run's session id, so `run_started` and
`run_finished` SHALL carry it as `sessionId`, and the invocation SHALL pass `--session-id <id>` on
the process command line. An id outside the session id grammar — 1 to 128 characters from `A-Z`,
`a-z`, `0-9`, `.`, `_` and `-`, starting with a letter or digit and without `..` — SHALL be a usage
error (exit 2) naming the flag and the value, before any model request. `--session-id` SHALL be
mutually exclusive with `--session`, `--continue` and `--no-session`, refused as a usage error
naming both flags. A session that cannot be loaded or created SHALL exit 2 naming the id, like the
other print-mode selection failures.

#### Scenario: A run creates the named session

- **GIVEN** a session directory with no `<id>.jsonl`
- **WHEN** `lunate -p --session-id <id> "hi"` runs
- **THEN** `<id>.jsonl` exists with `<id>` as its header id, `run_started` and `run_finished` carry
  `sessionId: <id>`, and the process command line contains `--session-id <id>`

#### Scenario: The same id continues its session

- **GIVEN** a session file `<id>.jsonl` with recorded history
- **WHEN** print mode runs again with `--session-id <id>`
- **THEN** that file loads, the first request carries the recorded history, and no second session
  file is created

#### Scenario: An id outside the grammar is a usage error

- **GIVEN** `--session-id` with an id that has a path separator, `..`, a leading `.`, a character
  outside the grammar or no value
- **WHEN** the invocation is parsed
- **THEN** it exits 2 with a message naming `--session-id` and the value, writes nothing to stdout,
  and makes no model request

#### Scenario: The id conflicts with the other selection flags

- **GIVEN** `--session-id` together with `--session`, `--continue` or `--no-session`
- **WHEN** the invocation is parsed
- **THEN** it exits 2 naming both flags, starts no session, and writes nothing to stdout
