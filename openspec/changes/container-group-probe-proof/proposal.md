## Why

Review of the container tool runner found that a timed-out group probe has `ExitCode == null`, but
the lifecycle treats every nonzero or null result as "not alive". A failed or unproven probe can
therefore leave the cancelled command's process group running while the container stays in use.
The existing process-isolation spec already requires removal when the group cannot be proven
stopped; the implementation did not distinguish proof from failure.

## What Changes

- Classify the container-side group probe as alive, gone, or unproven: only `kill -0` exiting `0`
  proves the group is alive, and only exiting `1` proves it is gone.
- Treat a timed-out probe (`ExitCode` null), any other nonzero probe result, or a failed engine call
  as unproven, remove the container, and let the next command recreate it.
- Also remove the container when a TERM/KILL engine call cannot complete, rather than trusting an
  incomplete sequence.
- Pin the timed-out and engine-failure probe paths with container lifecycle tests.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `process-isolation`: the container cancellation requirement names what proves a process group is
  gone; a timed-out or failed probe is unproven and removes the container.

## Impact

- `src/Lunate.Coding/ContainerEnvironment.Lifecycle.cs`
- `tests/Lunate.Coding.Tests/ContainerEnvironmentTests.cs`
- No public API, dependency, session format or user-facing string change.
