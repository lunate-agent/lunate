# process-isolation Specification

## Purpose
Where a tool's child process runs: a tool child owns no terminal (no controlling tty, stdin never the session's keys, captured stdout/stderr), cancellation and timeout end the whole tree it started, and the opt-in key-route trace (`LUNATE_INPUT_TRACE`) reports why a key never reached a turn without ever recording input content.

## Requirements

### Requirement: Tool children run with no terminal

Every process a tool starts SHALL run with no control over Lunate's terminal. On POSIX the child SHALL be spawned in a new session (`POSIX_SPAWN_SETSID`) so it has no controlling terminal and cannot open `/dev/tty`; its stdin SHALL be `/dev/null` when no line handler answers it, and the line-pump answer pipe when one does; its stdout and stderr SHALL be captured pipes, never the session's tty. On Windows the child SHALL be started with redirected stdio (`Process.Start` with `UseShellExecute: false`), so stdin is a pipe that reads as end-of-file and no console input handle exists. A child therefore can neither consume the session's keystrokes (a masked secret prompt included) nor reconfigure the session's raw mode, and the interactive prompt stays usable throughout a tool call.

#### Scenario: A tool child cannot touch the session tty
- **GIVEN** the interactive session is running in raw mode
- **WHEN** a `bash` tool call starts a child that asks for the terminal
- **THEN** the child sees no controlling terminal (`tty` is without a name), its stdin is not a terminal, and the session's own termios (`stty -a`) is identical while the call runs, after cancellation and after quit

#### Scenario: Keys typed during an interactive child stay in the session
- **GIVEN** a tool call has started a child that wants terminal input
- **WHEN** the user types or presses approval keys
- **THEN** the child receives nothing from the session's tty; the keystrokes stay in the session's own key reader

### Requirement: Cancellation kills the detached tree

Cancelling or timing out a tool process SHALL end the whole tree: the runner SHALL kill the spawned child and every descendant it started (children before parents) on cancellation and on timeout, then reap it, so no grandchild survives an interrupted call. This composes with the isolation above: the command session, its prompts and any dead-end spinner are all part of the killed tree.

#### Scenario: Cancel during a long call leaves nothing running
- **GIVEN** a `bash` call has started a long-running command tree
- **WHEN** the user presses `Esc` while the call runs
- **THEN** the turn stops, the process tree is gone from the process table, and the input line accepts the next command without a restart

### Requirement: The key-route diagnostic is secret-safe

`LUNATE_INPUT_TRACE` SHALL trace the keys the interactive session reads when set. A value of `1` SHALL trace to `<state root>/diagnostics/input-trace.log`; any other value SHALL name a path that canonicalises to inside the state root and SHALL be refused otherwise. A trace line SHALL record only the key's kind, its modifiers, the routing decision the router made, the run/approval state at that key, and a UTC timestamp. Printable characters and paste content SHALL always be reduced to a `chars=N` count — a credential typed into a masked prompt or carried by a paste leaves no bytes in the file. On POSIX the trace file SHALL be owner-only (0600), UTF-8 and LF.

#### Scenario: A paste carrying a secret leaves no bytes
- **GIVEN** `LUNATE_INPUT_TRACE=1`
- **WHEN** the user pastes text that contains a credential
- **THEN** the trace names the paste and its character count, and no byte of the secret appears in the trace file

#### Scenario: An `Esc` that never reached a turn is still in the trace
- **GIVEN** `LUNATE_INPUT_TRACE=1` and a report that `Esc` did not stop a run
- **WHEN** the session writes the trace for every key it read
- **THEN** the trace shows the `Esc` with the routing decision and the run state, so the report has the route it missed

### Requirement: A POSIX child inherits only its own descriptors

Every POSIX child Lunate spawns SHALL be given exactly the descriptors its own spawn names: standard input at `/dev/null` or the line-pump pipe, standard output and standard error on captured pipes, and nothing else. The pipe ends SHALL be created close-on-exec, and the child SHALL receive its descriptors explicitly through the spawn file actions (`adddup2`/`addopen`) rather than by inheritance. On Darwin the spawn SHALL also set `POSIX_SPAWN_CLOEXEC_DEFAULT`, so the child keeps only the descriptors its file actions name; Linux keeps the close-on-exec handles, because glibc and musl have no equivalent spawn flag. A child's descriptor set SHALL NOT contain a pipe end belonging to a concurrent spawn.

#### Scenario: An overlapping spawn does not hold the earlier child's stdout
- **GIVEN** a spawn's captured stdout pipe is open in the parent while it is starting
- **WHEN** a second child is spawned at the same instant and stays alive
- **THEN** the first child's stdout reader reaches end-of-file as soon as the first child exits, while the second child is still alive

#### Scenario: An overlapping spawn does not hold the earlier child's stderr
- **GIVEN** a spawn's captured stderr pipe is open in the parent while it is starting
- **WHEN** a second child is spawned at the same instant and stays alive
- **THEN** the first child's stderr reader reaches end-of-file as soon as the first child exits, while the second child is still alive

#### Scenario: A child's descriptor list names no pipe end it did not receive
- **GIVEN** a child spawned while a concurrent child is alive
- **WHEN** the child lists its own open descriptors
- **THEN** every descriptor is one of its standard streams or the descriptor used to list them, and no pipe end from the concurrent spawn appears

### Requirement: Cancellation reaches inside the container

When a tool command runs in a container environment, cancelling or timing out the call SHALL end the
command's process group inside that container in addition to the host `exec` client. The runner SHALL
capture the container-side process group id before the command body can outlive it, terminate the
group with `TERM` and then `KILL` after a short grace, and remove the container when the group cannot
be proven stopped. A probe SHALL count as proof only when it reports the group absent (exit code
`1`); a timed-out probe (`ExitCode` null), any other nonzero probe result, or a failed engine call
SHALL count as unproven and SHALL remove the container. A cancelled container command SHALL leave no
process of that call running in the container, and the session's next command SHALL still run. The
host tree kill from the existing process-isolation contract SHALL still run for the `exec` client.

#### Scenario: A cancelled container command leaves nothing in the container

- **GIVEN** a container-backed bash call running a long command
- **WHEN** the user cancels the turn
- **THEN** no process of that call remains in the container, the host `exec` client is gone, and a
  subsequent bash call succeeds in the same session

#### Scenario: A missing pgid still ends the call

- **GIVEN** a container command whose wrapper line was not captured
- **WHEN** the call is cancelled
- **THEN** the container is removed, the host client is killed, and a later call recreates the
  container rather than reusing one with a survivor

#### Scenario: An unproven group probe removes the container

- **GIVEN** a cancelled container command whose group probe times out or fails without reporting the
  group absent
- **WHEN** the runner cannot prove the process group is gone
- **THEN** it removes the container instead of reusing it, and the next command creates a fresh one
