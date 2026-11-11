# Execution-environment seam

## Why

Lunate has no single place that says where a tool command's process runs. Bash and ripgrep call
`ProcessRunner` directly, the extension/MCP session path reaches the same runner through
`IProcessRunner`, and host support code starts its own processes. #627 has to put a container behind
the tool-command path, and S-7 measured that a host-side tree kill leaves the container's process
group alive. The seam therefore has to exist before a second implementation can be safe.

This card is a refactor with no behaviour change. It introduces the seam and leaves local execution
as the only implementation; no flag, setting, public API or file-tool change ships with it.

## What Changes

- Add one internal `IExecutionEnvironment` seam for tool-command processes: it maps a command's
  `(executable, argument prefix, working directory, environment)`, describes itself in one line, and
  owns cancellation semantics for what it starts.
- Add `LocalExecutionEnvironment` as the only implementation; it maps nothing and keeps today's
  process-tree kill and `ProcessRunner` behaviour.
- Route `BashTool`, the ripgrep search engine, and the `IProcessRunner` extension/MCP session path
  through the environment.
- Keep host support subprocesses local by construction: editor/clipboard helpers, guard probes, the
  `ps` table read and the post-bash change scan do not use the tool-command seam.
- Keep `read`, `write`, `edit`, `ls`, `glob` and `grep`'s file reads on host filesystem access. They
  never spawn a process; file tools are deliberately outside this boundary.
- Add an inventory test that fails when a new process-start site appears without being classified.

## Capabilities

### New Capabilities

- `execution-environment`: where a tool command's process runs, what the seam maps, how it cancels,
  how it describes itself, and the inventory rule that no tool-command process bypasses it.

### Modified Capabilities

- None. This is an internal seam; tool output, approvals, environment variables, cancellation
  results and file-tool behaviour stay exactly as they are.

## Impact

- Code: `src/Lunate.Coding` (the seam, local implementation, `BashTool`, `RipgrepGrepEngine`,
  `ProcessRunner`, `BashChangeTracker`, clipboard-image default), plus the extension path that
  consumes `IProcessRunner` unchanged.
- Tests: existing `ProcessRunner`, `ProcessSession`, `BashTool`, `BashChangeTracker`,
  `GrepTool`, `ClipboardImage` suites, plus new seam/inventory tests.
- No public API, settings, dependencies, session format, protocol or documentation surface changes.
- Follow-on: #627 supplies the container implementation behind the same seam.
