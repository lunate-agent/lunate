## Context

`ContainerEnvironment.KillGroupOrRemoveAsync` sends `TERM`, waits, sends `KILL`, then probes the
container-side process group with `kill -0 -<pgid>`. The probe runs through `ProcessRunner`, which
returns `ExitCode == null` when the engine call times out and returns any engine or shell exit code
unchanged. The lifecycle currently reads `ExitCode == 0` as alive and everything else as gone, so a
timed-out or failed probe silently keeps the container. The container is then reused even though the
cancelled command may still be running inside it.

The process-isolation spec already requires removal when the group cannot be proven stopped. This
change makes "proven" explicit in the implementation.

## Goals / Non-Goals

**Goals:**

- Only an explicit `kill -0` exit code of `1` (no such process group) counts as proof that the group
  is gone.
- A timed-out probe, any other nonzero probe result, or a failed engine call removes the container.
- The next command recreates the container lazily, as it already does for a missing pgid.
- The normal path (group gone after KILL) keeps the container and pays no extra removal.

**Non-Goals:**

- Changing the kill sequence, grace period, pgid capture, or engine budgets.
- Adding a retry or a second probe format.
- Changing the security boundary or BashGuard policy.

## Decisions

- **A three-state probe instead of a boolean.** `GroupAliveAsync` becomes a state decision returning
  alive, gone, or unproven. `ExitCode == 0` is alive; `ExitCode == 1` is gone; `TimedOut` /
  `ExitCode == null` and every other nonzero result are unproven. The shell and engine both use
  nonzero for failures, so treating all nonzero as "gone" is the bug; `kill -0` specifically uses
  `1` for absence.
- **Unproven and alive share the removal path.** Only "gone" returns early. Anything else removes the
  container and resets readiness to `starting`, so the next call creates a fresh container. This is
  the existing fallback for a missing pgid and for a group that survives `KILL`.
- **An engine failure during TERM, KILL or the probe is also unproven.** `KillGroupOrRemoveAsync`
  catches `ContainerEnvironmentException` from those calls, logs nothing user-facing, and proceeds to
  removal. `Remove` is already best-effort. Cancellation must not throw because cleanup could not
  prove its work.
- **Tests drive the failure, not a private method.** The fake engine exposes the probe result and the
  existing cancellation path drives `KillGroupOrRemoveAsync`; the test asserts the `rm` request. A
  timed-out result and an engine-style nonzero result are pinned separately.

## Risks / Trade-offs

- **A transient probe failure removes a healthy container.** That costs one create+start on the next
  command, which the spike measured at about 146 ms. It is the safe direction: a survivor must not
  outlive its cancelled call.
- **Trusting exit code `1` assumes the engine preserves the shell's status.** The existing real
  container E2E and the docker/podman argv shape already rely on engine exit-code pass-through; the
  normal cancellation path still exercises it.
- **Removal can also fail.** `Remove` remains best-effort; a container the engine cannot remove is
  reported by the next readiness state rather than making cancellation crash.

## Migration Plan

None. The default host path is unchanged and no persisted state changes.

## Open Questions

None.
