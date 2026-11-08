# Tasks — execution-environment-seam

## 1. Seam and local implementation

- [ ] 1.1 Add the internal `IExecutionEnvironment`, `ExecutionCommand` mapping record and
      `IExecutionProcess` handle, with a one-line description and an explicit cancel hook.
- [ ] 1.2 Implement `LocalExecutionEnvironment` for batch commands: identity mapping, unchanged
      spawn/environment/output-cap/timeout/line-pump behavior, and tree-kill cancellation.
- [ ] 1.3 Implement the local interactive session through the same environment, preserving the
      existing line framing and tree kill.
- [ ] 1.4 Make `ProcessRunner` a facade that delegates `RunAsync` and `StartSessionAsync` to its
      environment, and give host support an explicit `ProcessRunner.Local`.

## 2. Wire the tool commands and keep host support local

- [ ] 2.1 Route `BashTool` through the injected environment while keeping its guard, result and
      change-scan behavior unchanged.
- [ ] 2.2 Route `RipgrepGrepEngine` through the same environment.
- [ ] 2.3 Make the change scan and clipboard-image default explicitly local; leave editor,
      clipboard writer, guard probes and the `ps` table read on their host-local starts.
- [ ] 2.4 Confirm the extension/MCP `IProcessRunner` path receives a runner backed by the environment,
      and leave the public `IProcessRunner` contract unchanged.

## 3. Tests

- [ ] 3.1 Add seam tests proving Bash and ripgrep commands are mapped/run through the environment.
- [ ] 3.2 Add a test proving the post-bash change scan stays local when a non-local environment is
      injected into the tool.
- [ ] 3.3 Add the process-start inventory test: every direct start is classified as a tool command
      (through the seam) or host support (local), and an unclassified site fails.
- [ ] 3.4 Run the focused existing process, session, Bash, change-tracker, grep and clipboard test
      classes; formatting passes and the tree is clean.

## 4. OpenSpec and archive

- [ ] 4.1 `openspec validate execution-environment-seam --strict` passes.
- [ ] 4.2 Update any document that names the old process structure; no user-facing docs change.
- [ ] 4.3 Sync/verify the change and archive it as the final commit.
