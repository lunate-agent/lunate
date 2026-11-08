## ADDED Requirements

### Requirement: Cancellation reaches inside the container

When a tool command runs in a container environment, cancelling or timing out the call SHALL end the
command's process group inside that container in addition to the host `exec` client. The runner SHALL
capture the container-side process group id before the command body can outlive it, terminate the
group with `TERM` and then `KILL` after a short grace, and remove the container when the group cannot
be proven stopped. A cancelled container command SHALL leave no process of that call running in the
container, and the session's next command SHALL still run. The host tree kill from the existing
process-isolation contract SHALL still run for the `exec` client.

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
