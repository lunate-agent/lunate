# Tasks: parallel read-only tools

## 1. Red tests for the batch contract

- [x] 1.1 Add `AgentParallelToolTests` in `tests/Lunate.Agent.Tests/Loop/` with a wall-time fixture
  that times four delayed `read` calls and proves the batch beats the serial sum.
- [x] 1.2 Add tests for deterministic start/result order and per-call result ids, including a second
  run with the same script.
- [x] 1.3 Add an approval test whose approver declines one call and allows the others.
- [x] 1.4 Add a failing/erroring call test whose siblings still complete.
- [x] 1.5 Add a cap test with more calls than the constant, recording maximum in-flight bodies.
- [x] 1.6 Add a mixed-batch test proving a read beside a write/bash stays serial, and a
  `Concurrency = Sequential` read keeps the batch serial.
- [x] 1.7 Add a cancellation test for a running batch that keeps the event stream and history valid.

## 2. Implementation

- [x] 2.1 Split `InvokeToolAsync` into preparation (hook + approval) and body execution so bodies can
  be scheduled independently.
- [x] 2.2 Add the read-only eligibility check, the `MaxParallelReadOnlyCalls` constant and the
  parallel batch path with a semaphore, per-call event buffers and ordered completion.
- [x] 2.3 Route an all-eligible multi-call turn to the batch path; keep every other turn on the
  serial path.
- [x] 2.4 Ensure cancellation and failure repair leave every started call with one result and remove
  completed calls from the dangling set.

## 3. Verification and artifacts

- [x] 3.1 Run the focused `Lunate.Agent.Tests` classes and the build for the touched projects.
- [x] 3.2 Update the user guide where it describes read-only tool execution, if the behaviour is
  user-visible.
- [x] 3.3 Run `dotnet csharpier format .`, `openspec validate parallel-readonly-tools --type change
  --strict`, and the full `openspec validate --all --strict`.
- [x] 3.4 Write `changes/<pr-number>.md`, sync the delta specs and archive the change as the last
  commit.
