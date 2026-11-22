# Tasks

## 1. Job runtime (test-first)

- [x] 1.1 Add the bounded capture and `BashJob` state machine (start, pumps, timeout, exit, kill, unread drain) with tests for start/poll/exit, polling only unread output, bounded output, timeout and kill
- [x] 1.2 Add the per-session `BashJobRegistry` (unique ids, lookup, `KillAllAsync`) with a session-end test proving no child survives

## 2. Tool family

- [x] 2.1 Add `background` to the `bash` schema and start a job through the selected `IExecutionEnvironment`, returning the job id immediately
- [x] 2.2 Add `bash_output` (read-only) and `bash_kill` (execute) with teaching errors for unknown or malformed arguments
- [x] 2.3 Add the background start hook to local and container execution environments, keeping the container-side cancel step
- [x] 2.4 Register the three tools together in `HarnessFactory` and reserve the new core names

## 3. Session lifetime

- [x] 3.1 Own one registry per interactive, print and ACP session and kill its jobs on every session-end path, before the execution environment is disposed
- [x] 3.2 Share the interactive session's registry with its child-workspace tool factory

## 4. Tests and documentation

- [x] 4.1 Add background tests on the CI platforms: start, poll, kill the process tree, natural exit code, timeout, bounded output, session-end kill, unknown ids
- [x] 4.2 Update the core-tool expectations, system-prompt parity and workspace-factory tests for the two new tools
- [x] 4.3 Update the current tool documentation (guide, how-it-works, usage, security) for the background lifecycle
- [x] 4.4 Run the fast gate for the touched projects, format, then write the changelog fragment

## 5. Close

- [ ] 5.1 `openspec validate add-background-bash --strict`, sync the delta specs and archive the change as the last commit
