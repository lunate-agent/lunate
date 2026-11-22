## MODIFIED Requirements

### Requirement: Bash tool
The `bash` tool SHALL run a single non-interactive command through the platform's resolved shell, in the workspace root, with stdout and stderr captured separately, output decoded as UTF-8, both streams bounded with an explicit cap marker, and the exit code surfaced in the result (culture-invariant). The resolved shell SHALL be started detached from the terminal as the `process-isolation` capability specifies — on POSIX in a new session with no controlling terminal, stdin at `/dev/null` (or the line-pump answer pipe when the run answers lines) and stdout/stderr on captured pipes. The tool SHALL be marked `Execute` risk so the approval policy applies, its description SHALL name the resolved shell and dialect, and a missing shell SHALL produce a clear error result rather than a partial run. On timeout (default 120 s) or cancellation the process tree SHALL be terminated, children before parents (the mechanism is platform-specific: `ps`-walk + SIGKILL on POSIX, job-object tree kill on Windows); cancellation SHALL rethrow `OperationCanceledException`; `IsError` SHALL be true for non-zero exits, timeouts and spawn failures. The tool SHALL accept an optional `background` boolean (default `false`); when it is `true` the call SHALL start the command without waiting and return the job id immediately, and the `background` value SHALL be a boolean or the result SHALL be an error naming the argument.

#### Scenario: Shell resolution follows the platform table
- **GIVEN** a Windows machine with Git Bash installed
- **WHEN** the shell is resolved
- **THEN** Git Bash wins with bash-dialect invocation, `C:\Windows\System32\bash.exe` is never selected, and the order falls through pwsh 7, Windows PowerShell, cmd only when earlier shells are absent

#### Scenario: A timeout kills the whole tree
- **GIVEN** a command that spawns a long-lived child
- **WHEN** the timeout expires
- **THEN** the result reports the timeout and neither the command nor its child remains alive

#### Scenario: Cancellation kills the tree and propagates
- **GIVEN** a running command
- **WHEN** the call's token is cancelled
- **THEN** the tree is killed and an `OperationCanceledException` is raised

#### Scenario: A cancelled call leaves the session terminal untouched
- **GIVEN** an interactive session running a `bash` call with an interactive child inside it
- **WHEN** the call is cancelled from the key reader
- **THEN** the whole tree is gone, the session's termios is unchanged, and keys typed afterwards reach the session, never the dead child

#### Scenario: UTF-8 and exit codes round-trip
- **GIVEN** a command emitting non-ASCII text and exiting non-zero
- **WHEN** it runs
- **THEN** the output carries the text intact and the result shows the exit code with `IsError` true

#### Scenario: Output is bounded
- **GIVEN** a command producing more than the capture cap
- **WHEN** it runs
- **THEN** the captured output ends with the cap marker and memory stays bounded

#### Scenario: A background call returns a job id without waiting
- **GIVEN** a command that would run past the turn's patience
- **WHEN** `bash` is called with `background: true`
- **THEN** the call returns promptly with a job id and the command keeps running

## ADDED Requirements

### Requirement: Background bash jobs

The bash tool family SHALL provide one per-session job registry shared by `bash`, `bash_output` and `bash_kill`. A background `bash` call SHALL allocate an id unique within the session, start the command with the foreground path's shell resolution, workspace root, guard, environment, UTF-8 decoding and detached process isolation, and return that id immediately. Job state SHALL NOT be written to the session file and SHALL NOT survive the session.

`bash_output` SHALL take a required `job_id` and return the combined stdout and stderr captured since the previous read of that job, followed by the job's state: still running, or ended with its exit code (culture-invariant). Output SHALL be the job's unread output, not its whole history. It SHALL be marked read-only. An ended non-zero exit SHALL be an error result carrying the exit code. A timed-out job SHALL report the same timeout wording as the foreground tool. An unknown id SHALL be an error naming the id and how to start a background job.

`bash_kill` SHALL take a required `job_id`, terminate that job's whole process tree children before parents with the same mechanism as the foreground path, reap it, and report that the tree was stopped. Killing an already-ended job SHALL report its exit code instead of failing. An unknown id SHALL be an error naming the id and how to start a background job. It SHALL be marked `Execute` risk so the approval policy covers ending processes.

Each captured stream SHALL stay bounded regardless of how much a job writes while unread: when unread output exceeds the capture cap, the oldest output SHALL be discarded, an explicit truncation marker SHALL appear once in the next `bash_output` result, and memory SHALL stay bounded. A background job SHALL use the same timeout as the foreground tool; on expiry it SHALL be killed and its next `bash_output` SHALL report the timeout. Ending a session SHALL kill every job it owns and wait for the kills, so no orphan survives.

#### Scenario: A job starts, is polled and ends with its exit code
- **GIVEN** a background job that prints a line and exits with code 7
- **WHEN** `bash_output` is called after it ends
- **THEN** the result contains the line and `exit code: 7`, and it is an error result

#### Scenario: Polling returns only unread output
- **GIVEN** a background job that prints `first`, waits, then prints `second`
- **WHEN** `bash_output` is called once after `first` and again after the job ends
- **THEN** the first result contains `first` and not `second`, and the second contains `second` and not `first`

#### Scenario: Killing stops the whole tree
- **GIVEN** a background job whose child writes a pid file and sleeps
- **WHEN** `bash_kill` names the job
- **THEN** the job is no longer running and both the command and the child it recorded are gone

#### Scenario: Unread output stays bounded
- **GIVEN** a background job that writes more than the capture cap before the first poll
- **WHEN** `bash_output` reads it
- **THEN** the result carries the truncation marker and the captured text stays within the cap

#### Scenario: The timeout still applies
- **GIVEN** a background job that outlives the tool's timeout
- **WHEN** the timeout expires and the job is polled
- **THEN** the result reports the timeout and the whole process tree is gone

#### Scenario: Session end leaves no job running
- **GIVEN** a session with a running background job
- **WHEN** the session ends
- **THEN** every job is killed and its process tree is gone before the session finishes

#### Scenario: Unknown jobs are teaching errors
- **GIVEN** no job with the requested id
- **WHEN** `bash_output` or `bash_kill` is called
- **THEN** the error names the id and says to start a background job with `bash` and `background: true`
