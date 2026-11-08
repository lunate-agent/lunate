# execution-environment Specification

## Purpose
How Lunate selects, creates, uses and tears down a containerised execution environment for
tool-command processes: the accepted selection shape, the one-workspace mount rule, the long-lived
session container, the host-side boundary that stays unchanged, and cancellation of a command's
process group inside the container.

## Requirements

### Requirement: Environment selection has one accepted shape

`--env` SHALL accept `docker:<image>` or `podman:<image>`, and the `environment` settings key SHALL
accept the same two shapes. The flag SHALL override the settings value. An absent value SHALL select
today's host execution, unchanged. A value with another prefix, an empty engine, an empty image, or
an extra colon that does not form an engine prefix SHALL be refused with a message naming the
accepted shapes; it SHALL never silently fall back to host execution. The engine and the image SHALL
be reported in the vocabulary the user selected (`docker` or `podman`).

#### Scenario: The flag selects the environment over the settings file
- **GIVEN** a settings file with `"environment": "podman:alpine"`
- **WHEN** the session starts with `--env docker:debian`
- **THEN** the active environment is `docker:debian`

#### Scenario: An unaccepted shape is refused
- **GIVEN** `--env nerdctl:alpine` or `--env docker:`
- **WHEN** the launch options are parsed
- **THEN** the launch fails with an error naming `docker:<image>` and `podman:<image>`, and no
  session composes

#### Scenario: No selection keeps the host path
- **GIVEN** no `--env` and no `environment` setting
- **WHEN** a session runs a bash command
- **THEN** the command runs on the host exactly as before

### Requirement: Only the workspace is mounted at its host path

When a container environment is active, the container SHALL mount the canonical workspace root at
the same absolute path on both sides and SHALL set that path as the working directory. It SHALL NOT
mount the Lunate state root, the install prefix, `auth.json`, the user profile, or any path outside
the workspace. The container's host reach SHALL therefore be the workspace and the engine's own
runtime, not the agent's keys or state. A workspace path the engine's volume syntax cannot express
(for example a colon in a POSIX path) SHALL be refused with a teaching error rather than rewritten
or partially mounted.

#### Scenario: The workspace appears at the same path on both sides
- **GIVEN** a container environment on `/home/user/repo`
- **WHEN** the container is created
- **THEN** its create arguments mount `/home/user/repo` at `/home/user/repo`, set the working
  directory to `/home/user/repo`, and name no directory under the state root or the install prefix

#### Scenario: Agent material is absent inside the container
- **GIVEN** a running container environment
- **WHEN** a command reads the state root, `auth.json` and the install prefix
- **THEN** each path is absent, and no agent credential or Lunate state was mounted

### Requirement: One long-lived container per session and workspace

The environment SHALL create and start one container per session and each workspace root it is
asked to run in, before the first command in that root, and SHALL reuse it for every later command
in the same root. It SHALL NOT create a container per tool call or stop and restart one between
calls. A child run in another worktree SHALL get its own container mounted at that root rather than
run in a container mounted at the parent's path. Every container the session created SHALL be
removed when the session ends, including after a failed start or a cancelled command.

#### Scenario: Two commands share one container
- **GIVEN** an active container environment
- **WHEN** two bash calls run in the same workspace
- **THEN** the engine's create/start ran once and both calls used `exec` against the same container

#### Scenario: Session teardown removes the container
- **GIVEN** a session that created a container
- **WHEN** the session ends
- **THEN** the container is removed, and a second teardown is harmless

### Requirement: Command tools run through the engine's exec

With a container environment active, each `bash` tool call SHALL run its command through
`<engine> exec` in the session's container, in the workspace working directory. The session marker
SHALL be forwarded explicitly with `-e LUNATE_SESSION=1`, because `exec` does not inherit the host
environment. The container's stdout and stderr SHALL be the tool result's stdout and stderr, with
the runner's own pgid wrapper line removed. The host change scan SHALL run after every call exactly
as on the host path, so a file the container writes through the mount is published as a `bash`
change.

#### Scenario: A container-written file is published on the host
- **GIVEN** a container environment with the workspace mounted
- **WHEN** a bash call writes `probe.txt` in the working directory
- **THEN** the file exists on the host and the change bus reports it with source `bash`

#### Scenario: The session marker is visible inside the container
- **GIVEN** a container environment
- **WHEN** a bash call reads `LUNATE_SESSION`
- **THEN** it reads `1`

#### Scenario: A result does not carry the runner's pgid line
- **GIVEN** a container command prints `hello`
- **WHEN** the tool result is returned
- **THEN** stdout is exactly `hello`, without the wrapper's pgid marker

### Requirement: The host boundary is unchanged and named honestly

Approvals SHALL happen on the host before the command runs. `read`, `write` and `edit`, write
verification and the file-change bus SHALL remain host-side and operate on the mounted workspace
without container awareness. The bash guard SHALL keep wrapping the host process the tool spawns —
the engine's `exec` client — and its pre-run redirection refusal and post-run hint SHALL remain in
force. The product SHALL NOT claim that the bash guard constrains the inner container command; the
container mount boundary is what limits that command's host reach.

#### Scenario: An edit made by a container command is verified on the host
- **GIVEN** an active container environment
- **WHEN** a bash command writes a file that a later `edit` changes
- **THEN** the edit runs in the host tool with the host verifier and publishes its change on the
  host bus

#### Scenario: The guard refusal still precedes the engine
- **GIVEN** a bash guard that denies writes under the state root and an active container
  environment
- **WHEN** a command's redirection targets the state root
- **THEN** the tool refuses before starting the engine, naming the guard, and no container process
  runs

### Requirement: Cancel ends the command's process group inside the container

Cancelling or timing out a container-backed command SHALL terminate the command's whole process
group inside the container, not only the host-side `exec` client. The runner SHALL capture the
container-side process group id from the command wrapper, send `TERM` to the group, wait a bounded
time, send `KILL` to the group, and confirm the group is gone. When the id cannot be captured or
the group cannot be stopped, the runner SHALL remove the container (and recreate it lazily for a
later call) so no command survives the cancellation. The host-side process tree kill SHALL still
run, so the `exec` client and its descendants do not linger either.

#### Scenario: Cancel kills a long-running container command
- **GIVEN** a bash call in a container is running `sleep 30`
- **WHEN** the turn is cancelled
- **THEN** the sleep and its process group are gone inside the container, the host `exec` client is
  gone, and the next bash call still works

### Requirement: The active environment is visible to the user

The startup header SHALL name the active execution environment when one is selected, in the form
`environment <engine>:<image>`, using the ASCII separator on the single-line form. `/context` SHALL
name it with a readiness state (`ready`, `starting`, or `failed: <reason>`). With no environment
selected the header SHALL NOT print an environment line and `/context` SHALL NOT add an
environment segment, so today's output is unchanged.

#### Scenario: The header names the selected environment
- **GIVEN** a session started with `--env podman:alpine`
- **WHEN** the welcome header prints
- **THEN** it contains `environment podman:alpine`

#### Scenario: /context reports readiness
- **GIVEN** a session with an active container environment
- **WHEN** `/context` runs
- **THEN** its text contains `environment: docker:<image> (starting|ready|failed: …)`

### Requirement: Platform support is claimed only where tested

The local host path SHALL remain the default and SHALL keep working on every platform Lunate
supports. Container execution SHALL be claimed for Linux hosts on the CI matrix (Docker) and macOS
with Podman as demonstrated by the S-7 spike; other engines, native Linux Podman and Windows SHALL
NOT be claimed. Operating a container environment on Windows SHALL fail with a teaching message
naming the WSL2 follow-up, not a partial mount. The Linux CI job SHALL run a real container E2E test
covering the mount, the host file tools, the absent state root and cancellation.

#### Scenario: Windows refuses the container path with a next step
- **GIVEN** a Windows host and `--env docker:<image>`
- **WHEN** a bash call runs
- **THEN** it returns a teaching error naming the WSL2 follow-up, and no command runs on the host
