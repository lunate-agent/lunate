## Context

The `bash` tool is the one core tool that runs model-chosen code with the user's rights. ADR-0024
guards that process by denying writes under the state root and install prefix, but the guard is an
OS wrapper around the process Lunate spawns — it does not follow a command into a container. The
S-7 spike (macOS 26.3.1 arm64, Podman 6.1.0, Apple-VM machine, `mcr.microsoft.com/dotnet/sdk:10.0`)
measured what a container mode can and cannot do:

- A container created with the host's absolute workspace path on both sides (`-v <worktree>:<worktree>
  -w <worktree>`) makes host file tools, verified edits and the bash change scan work with no path
  translation. Identical bytes on both sides were measured.
- `podman exec` does not forward the host environment; the session marker must be passed with
  `-e LUNATE_SESSION=1`.
- The host tree kill stops the `podman exec` client but leaves the container process group alive
  (`33 sleep 4271` survived); `kill -TERM -- -<pgid>` inside the container stops it.
- One container per session is affordable (create+start 146 ms median); stop+start is 10.3 s, so a
  container is never stopped and restarted per call.
- The test image had neither `bwrap` nor `sandbox-exec`; no in-container guard is available to
  assume.

## Goals / Non-Goals

**Goals:**

- A user can select `docker:<image>` or `podman:<image>` per run (`--env`) or per settings file
  (`environment`), and command tools run in one long-lived container for the session.
- The container mounts the workspace and nothing else: no state root, no install prefix, no
  `auth.json`.
- File tools, approvals, write verification and the file-change bus remain host-side and unchanged.
- Cancel ends the command's process group inside the container.
- The header and `/context` name the selected environment and its readiness.
- The Linux CI matrix runs a real container E2E test; the host path is the default and every
  existing host test keeps passing.
- The BashGuard policy question is visible as a sign-off gate, not silently answered.

**Non-Goals:**

- Containerising `read`/`write`/`edit` or their verification. The file tools talk to the host mount.
- Docker Engine support claims beyond the CI Docker test, and any Windows/native-Linux host claim
  beyond what CI proves. Windows needs WSL2 with a translated mount; that is a follow-up.
- An in-container guard, image supply-chain policy, registry authentication, or a container image
  Lunate owns. The user names an image that has a POSIX shell.
- ACP container support in this change; the ACP frontend keeps the host path. (Follow-up if the
  policy is accepted for every mode.)
- Per-call containers, container reuse across sessions, or container warm pools.

## Decisions

- **The selection is one string, parsed at composition.** `docker:<image>` and `podman:<image>` share
  one parser; only `argv[0]` and the displayed engine name differ. The settings key is
  `environment`; `--env` overrides it. A malformed value is a settings/launch error with the
  accepted shape, never a silent host fallback.
- **One container per session per workspace root.** The session's workspace is the one named at
  composition. A child run in another worktree gets its own container, created lazily, mounted at
  that root only; the session disposes every container it created. Nothing is mounted from the
  state root or the user's profile. This keeps the "mount only the workspace" rule even for
  per-run workspaces (ADR-0021).
- **Same absolute path on both sides.** `-v <canonical worktree>:<canonical worktree>` and
  `-w <canonical worktree>`; no `/workspace` translation. A path containing a colon is refused with
  a teaching error, because the engine's volume syntax cannot express it unambiguously.
- **A wrapper captures the pgid in-band.** The container command is
  `sh -c 'echo LUNATE_PGRP:$$; exec "$@"' lunate-sh <shell> <args> <command>`. The first stdout line
  is the shell's pid, which the engine starts as the process-group leader. The runner strips that
  first line from the tool result and keeps the number as the cancel handle. Cancel sends
  `kill -TERM -- -<pgid>`, waits briefly, then `kill -KILL -- -<pgid>`. If the pgid is unknown or
  the kills fail, it removes the container and marks it for lazy recreation; the host `exec` client
  is still tree-killed by `ProcessRunner` as today.
- **The host guard composes with the engine client.** The guard wraps the engine executable (the
  "host half"), preserving ADR-0024's state-root/install-prefix deny and `auth.json` read-deny on
  the `exec` client, and its pre-run redirection scan still runs on the model's command text. It is
  explicitly not described as constraining the inner command; the container mount boundary is what
  limits that command's host reach. If an in-container guard is required, the sign-off decision
  adds one; this change does not pretend it exists.
- **Cancellation plumbing stays internal.** `ProcessRunner` gains an internal overload taking an
  `onCancel` callback and a first-stdout-line observer. The public `IProcessRunner` and
  `ProcessRunRequest` are unchanged, so extensions see no contract change.
- **The header names the selection before any call; `/context` adds readiness.** The startup line
  gets `environment docker:<image>` (ASCII separator off the block path) and `/context` appends
  `environment: <engine>:<image> (ready|starting|failed: …)`. Readiness comes from the environment
  object without creating the container; the container is created lazily on the first command so a
  session that never runs a command pays nothing.
- **The engine is resolved from `PATH` at composition.** A missing engine or a non-POSIX host is a
  visible selection error on the first command (and in `/context`), not a silent switch to host
  execution. The tool result names the engine, the image and what to install.
- **E2E is opt-in by environment variable.** The real-container test class runs only when
  `LUNATE_CONTAINER_E2E=<engine>:<image>` is set, so local `verify` does not require Docker. The
  Linux CI step pulls a small image and runs that class in the existing `verify` shard, so no new
  required job or `ci-ok` wiring is needed.

## Risks / Trade-offs

- **Container escape is not solved by a mount.** The engine socket, a hardlink into the state root,
  and the user's own files outside the mount remain reachable by a privileged container command
  (the same class ADR-0024 names). The mount limit reduces reach; it is not a jail. That is exactly
  the policy question the sign-off gate asks.
- **~126 ms per bash call** on macOS with Podman (S-7); the file tools are unaffected. The change
  states the budget rather than trying to hide it.
- **The wrapper's `$$` is only a pgid if the engine makes the exec'd process a group leader.** The
  S-7 measurement confirms Podman does; the fallback removes and recreates the container when the
  kill does not prove the group is gone. The E2E test pins the real behaviour on Linux Docker.
- **A command that detaches into its own session** escapes the pgid kill; the container removal
  fallback still ends it when a cancel finds it.
- **Images without `sh`** cannot run the wrapper; the tool reports the spawn failure with the
  engine's stderr and the image name.
- **ACP is not containerised** in this change; the mode keeps the host path. This is named in the
  PR rather than implied.

## Migration Plan

None: the default (no `environment`, no `--env`) is byte-for-byte the host path. A user opts in per
run or in their settings file; reverting the setting returns to the host path. No session format,
public API or persisted state changes.

## Open Questions

1. **Security boundary (maintainer sign-off).** Does the container filesystem boundary replace
   BashGuard's protection for inner commands, or is an in-container guard required? This change
   keeps the host guard and claims nothing about the inner command; the answer may add an
   in-container guard in a follow-up.
2. **Docker Engine and native Linux host.** CI proves Docker on Linux; the spike proved Podman on
   macOS. Native Linux Podman and Docker Desktop on macOS are not claimed until tested.
