## MODIFIED Requirements

### Requirement: The state root is guarded, visible and environment-only

The state root SHALL be read from the process environment only — no `.env` file, settings file, `AGENTS.md` or project file may supply `LUNATE_HOME` — because a repository that can choose the store can choose the directory its own trust records live in. A process that knows its workspace or repository root SHALL refuse a `LUNATE_HOME` whose store resolves inside it, before opening the store, with the message `LUNATE_HOME points inside the workspace; trust records there could be written by the repository`. When `LUNATE_HOME` is unset, the default profile home (`<profile>/.lunate`) SHALL never be refused, including when the working directory is the user's home directory, so a session started in `~` starts normally. Containment SHALL compare real paths (symlinks resolved, `.` and `..` resolved, trailing separators ignored, the platform's case rule), a store equal to the root SHALL count as inside, and a sibling or unrelated directory SHALL be accepted. When `LUNATE_HOME` is set, `lunate --version` and the interactive welcome header SHALL both print the active home as `home <path> (LUNATE_HOME)`, and neither SHALL print it when the variable is unset. The trust store, the extension cache, user extensions, sessions, `auth.json`, `settings.json`, history, exports, the model catalog, Lunate's own user skills and recordings SHALL follow the root; `~/.agents/skills`, `~/.agents/mcp.json` and NuGet's packages folder SHALL NOT, because they are shared conventions or content-addressed package storage rather than Lunate state. No log file is promised: extension logging is the terminal through `IExtensionLog`, and the session file is the transcript.

#### Scenario: A store inside the workspace is refused

- **GIVEN** `LUNATE_HOME` set to a directory inside the worktree, or equal to its root
- **WHEN** a session composes, or `lunate extensions build`, `lunate cache prune` or `lunate trust` runs there
- **THEN** the command fails with `LUNATE_HOME points inside the workspace; trust records there could be written by the repository`, and nothing is written to that path

#### Scenario: A sibling or unrelated store is accepted

- **GIVEN** `LUNATE_HOME` set to a sibling of the worktree, or to a directory inside no workspace
- **WHEN** a session or a store-writing subcommand runs
- **THEN** it proceeds and writes under that directory

#### Scenario: The default home starts a session from the home directory

- **GIVEN** `LUNATE_HOME` unset and the working directory set to the user's home directory
- **WHEN** a session composes
- **THEN** it starts normally, uses `<profile>/.lunate` as its state root, and does not report the workspace refusal

#### Scenario: A redirected run is visible

- **GIVEN** `LUNATE_HOME` set to an absolute temporary directory
- **WHEN** `lunate --version` runs and an interactive session starts
- **THEN** both print `home <that directory> (LUNATE_HOME)` in the same spelling, and a run without the variable prints neither

#### Scenario: A session with an unusable profile writes only the root

- **GIVEN** `LUNATE_HOME` set to a temporary directory, and `HOME`/`USERPROFILE` pointing at a directory that does not exist
- **WHEN** a full session runs one turn with a tool call
- **THEN** it exits successfully, its session file is under the redirected root, and the nonexistent home is still nonexistent
