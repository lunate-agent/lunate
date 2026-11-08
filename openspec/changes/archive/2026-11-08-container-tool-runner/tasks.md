## 1. Selection and settings (test-first)

- [x] 1.1 Pin the selection parser and precedence in failing tests: `docker:<image>` and
      `podman:<image>` accepted; empty engine/image, unknown engine and extra separators refused;
      `--env` overrides `environment`.
- [x] 1.2 Add `--env` to `LaunchOptions`, the usage/help text, `AgentSettings.Environment`, the
      settings parser/validator, `settings-schema.json` and the generated configuration reference.
- [x] 1.3 Wire the resolved value into interactive, print and ACP composition; ACP keeps the host
      path and says so in the change notes.

## 2. Container environment and lifecycle (test-first)

- [x] 2.1 Introduce the internal environment type: engine resolution from `PATH`, one container per
      session and workspace root, same-path workspace mount, no state/install/auth mount, a unique
      name, lazy create/start, and idempotent removal on session teardown.
- [x] 2.2 Pin the exact create/start/remove argv and the refusal for a path the volume syntax cannot
      express.
- [x] 2.3 Add the engine-missing and non-POSIX-host teaching errors; a selected environment never
      falls back to host execution.

## 3. Exec runner, session marker and cancellation (test-first)

- [x] 3.1 Add the `exec` command shape: working directory, `-e LUNATE_SESSION=1`, the pgid wrapper,
      and stripping the wrapper's first line from the tool result.
- [x] 3.2 Add the internal `ProcessRunner` cancel hook and first-line observer without changing the
      public `IProcessRunner`/`ProcessRunRequest` contracts.
- [x] 3.3 Cancel and timeout kill the container process group (`TERM`, grace, `KILL`), still kill the
      host `exec` client, and remove/recreate the container when the group cannot be proven stopped.
- [x] 3.4 Keep the bash guard on the host half, its pre-run refusal and its post-run hint; never
      describe it as constraining the inner command.
- [x] 3.5 Keep the host change scan after every container call and test that a container-written
      file is published with source `bash`.

## 4. User-visible environment (test-first)

- [x] 4.1 Add the environment status record and the header segment for the block and single-line
      forms; no segment when unset.
- [x] 4.2 Add the `/context` segment with `starting`/`ready`/`failed: <reason>`.

## 5. Verification

- [x] 5.1 Focused suites: environment parser/settings tests, container argv/lifecycle tests, exec and
      cancel tests, bash tool tests, header and `/context` tests, and every existing test class that
      references the changed types.
- [x] 5.2 Real-container E2E test (skipped unless `LUNATE_CONTAINER_E2E` names an engine and image):
      same-path mount, container-written file visible on host, host file tools and change scan,
      state root absent, session marker, and cancel ending a long command.
- [x] 5.3 Add the Linux-only E2E step to the existing `verify` job, pulling a small image and running
      the gated test class; no new required job.
- [x] 5.4 Docs: configuration key, usage flag, guide's execution-environment section, and the
      platform-support statement (Linux CI Docker + macOS Podman; Windows/WSL2 follow-up).
- [x] 5.5 Fast gate: build the touched project and test project, run the focused classes, then
      `dotnet csharpier format .` and confirm a clean tree; `openspec validate --all --strict`;
      `bash scripts/check-openspec-skills.sh`.
- [x] 5.6 Answer every acceptance check in the pull request by number with a `file:line` or a test
      name, including the sign-off gate as pending rather than satisfied.
