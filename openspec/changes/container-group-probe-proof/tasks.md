## 1. Prove the failure before the fix

- [x] 1.1 Add failing `ContainerEnvironmentTests` cases that cancel a running container command with
      a timed-out probe (`ExitCode` null) and with an engine-failure probe (nonzero, not `1`) and
      assert that the container is removed (`rm` request).
- [x] 1.2 Run those cases at the current head and record that they fail because no `rm` is sent.

## 2. Make the group proof explicit

- [x] 2.1 Replace the boolean group check with a three-state result: `0` alive, `1` gone, every
      other result or timeout unproven.
- [x] 2.2 Remove the container for alive or unproven; keep it only for gone.
- [x] 2.3 Treat an engine failure during TERM, KILL or the probe as unproven and continue to removal.

## 3. Verification and close-out

- [x] 3.1 Run `ContainerEnvironmentTests` green, plus every class that references
      `ContainerEnvironment` or `ProcessRunResult`.
- [x] 3.2 Run `dotnet csharpier format .`, `openspec validate container-group-probe-proof --strict`
      and `bash scripts/check-openspec-skills.sh`.
- [x] 3.3 Answer the review findings in the pull request by number with a `file:line` or test name.
