## Why

`bash` is synchronous: a build, test suite or dev server blocks the turn until it ends or the timeout fires, so the model cannot start something long-running and keep working. The bash family needs the same start/poll/stop lifecycle a terminal user has.

## What Changes

- `bash` gains an optional `background` flag. A background call starts the command, returns a job id immediately and leaves the turn free.
- `bash_output` returns the output captured since the last read and whether the job is still running, ended (with its exit code) or timed out.
- `bash_kill` ends a job and its whole process tree with the same children-first kill the foreground path uses.
- Captured output is bounded for a running job: unread output keeps a bounded tail with an explicit truncation marker rather than growing without limit.
- The existing bash timeout applies to a background job. Ending the session kills its jobs, so no detached child outlives Lunate.
- The three tools share one per-session job registry; the background start goes through `IExecutionEnvironment` like the foreground command.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `agent-tools`: the bash family gains background start, output polling and kill, with the model-facing results and argument contracts.
- `process-isolation`: background jobs apply the existing detached-tree isolation, timeout kill and session-end cleanup.
- `execution-environment`: background starts use the same tool-command seam and cancel hook as batch starts.

## Impact

- `Lunate.Coding`: `BashTool`, new `BashJobRegistry`/`BashJob`/`BashOutputTool`/`BashKillTool`, the local and container execution environments, the composition factory and the three frontends' session end.
- No new NuGet package, no change to the public API, no session-format change.
- Docs: the tool tables and the interactive/print guidance name the background lifecycle.
