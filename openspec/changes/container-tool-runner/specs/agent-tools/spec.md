## ADDED Requirements

### Requirement: Bash execution follows the selected environment

The `bash` tool SHALL run its command through the selected execution environment when one is active,
as the `execution-environment` capability specifies: `<engine> exec` against the session's
container, in the mounted workspace, with the session marker forwarded and the runner's pgid wrapper
line removed from the result. On the host path (no selection) the tool SHALL behave exactly as
before. In both paths the tool SHALL remain `Execute` risk, keep host approvals before the run,
capture stdout and stderr separately, bound both streams with the cap marker, report the exit code
culture-invariantly, scan for bash-made changes after the call, and return `IsError` for non-zero
exits, timeouts and spawn failures. A container selection that cannot start (missing engine, bad
image, unsupported host) SHALL produce a teaching error result naming the engine, image and next
step; it SHALL NOT silently run the command on the host.

#### Scenario: The same tool contract holds in a container
- **GIVEN** an active container environment
- **WHEN** a bash call exits non-zero
- **THEN** the result carries the exit code, the captured streams and `IsError: true`, exactly as
  the host path does

#### Scenario: A failed container start never falls back to the host
- **GIVEN** a selection naming an engine that is not installed
- **WHEN** a bash call runs
- **THEN** the result is an error naming the engine and the installation step, and no host process
  ran the command
