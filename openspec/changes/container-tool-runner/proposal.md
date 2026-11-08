## Why

Lunate's `bash` tool runs model-chosen commands directly on the host, so a command has the user's
whole filesystem and every credential the host process can reach. Users who already run
containerised development want the command half of the agent (not its file tools) to execute inside
a long-lived container whose only host mount is the workspace. The S-7 spike measured the shape that
works on macOS with Podman: one container per session, the host's absolute workspace path mounted at
the same path, `exec` for each command, and a container-side process-group kill on cancel.

## What Changes

- Add `--env <engine>:<image>` (`docker` or `podman`) and the `environment` settings key. The flag
  overrides the file value; the unset value keeps today's host execution.
- Create one long-lived container per session and workspace before the first command tool call.
  The create/start command mounts **only** the canonical workspace at the same absolute path
  (`-v <worktree>:<worktree> -w <worktree>`) and never mounts the state root, the install prefix or
  `auth.json`.
- Run the `bash` tool's command through `<engine> exec` in that container. Host approvals, the agent
  loop, the file tools, write verification and the file-change bus stay on the host; Bash writes
  arrive through the shared mount and the existing host scan publishes them.
- Forward the session marker with `-e LUNATE_SESSION=1`; `exec` does not inherit the host
  environment.
- Capture the container-side process group id from a wrapper's first output line and cancel with
  `kill -TERM -- -<pgid>` (then `KILL`, then remove/recreate the container if the group cannot be
  stopped). The host-side tree kill alone leaves the container's processes alive.
- Show the selected environment in the startup header and `/context`, with a short readiness state.
- Keep the bash guard wrapping the host half (the `exec` client) exactly as today. The container
  filesystem boundary is the protection for the inner command; this change does **not** claim that
  BashGuard constrains that command.
- Add a Linux CI E2E test (Docker) that proves a container-written file is visible on the host, the
  host file tools still work, the state root is absent inside the container, and cancel ends a
  long-running command inside the container. The local/host path stays the default and keeps
  working.
- Windows is out of scope; a Windows container cannot expose `C:\…` at the same absolute path. The
  WSL2 route is named as a follow-up, never claimed.

**Sign-off gate:** the maintainer must still decide whether the container filesystem boundary
replaces BashGuard for inner commands or an in-container guard is required. This change keeps the
host guard in place and makes no claim about the inner command; the question is carried on the pull
request with `needs-signoff`.

## Capabilities

### New Capabilities

- `execution-environment`: how a selected container environment is described, created, exposed to
  tools, shown to the user and torn down; which host paths may be mounted; and how cancellation
  reaches a process inside it.

### Modified Capabilities

- `agent-tools`: the `bash` tool gains a container execution path while its result contract, host
  approval and host change scan stay unchanged.
- `agent-config`: a new `environment` settings key with the `docker:<image>` / `podman:<image>` shape
  and its precedence against `--env`.
- `process-isolation`: cancellation of a container-backed command ends the command's process group
  inside the container, not only the host `exec` client.
- `tui`: the welcome header and `/context` name the active execution environment.

## Impact

- `Lunate.Coding`: `LaunchOptions`, `Cli`, `CliHelp`, `SettingsStore`, `AgentSettings`,
  `settings-schema.json`, `HarnessFactory`, `BashTool`, a new container environment/runner, and the
  interactive/print composition and teardown.
- `Lunate.Tui`: an environment status record and header rendering; the `/context` segment is
  composed in `Lunate.Coding`.
- No public API change: the new types are internal and the container engine is invoked through the
  existing internal `ProcessRunner`.
- No new NuGet package.
- Docs: `docs/configuration.md` (generated key table), `docs/usage.md` (flag and environment
  behaviour), `docs/guide.md` where execution environments are described.
- CI: one Linux-only container E2E step in the existing `verify` job; the E2E test is skipped
  locally unless `LUNATE_CONTAINER_E2E` names an engine and image.
