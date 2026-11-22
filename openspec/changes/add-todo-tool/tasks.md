# Tasks — todo planning tool

## 1. Contract

- [x] 1.1 Add red tests in `tests/Lunate.Agent.Tests` and `tests/Lunate.Extensibility.Tests` for the new event kind: a `TodoUpdated` carries the full list, folds into `ExtensionEvent` and `AgentEvent`, and is ignored by a fallback arm; confirm they fail on the missing type.
- [x] 1.2 Add `TodoItem`, `TodoItemStatus` and `TodoUpdated` to `Lunate.Contracts`, add the kind to both unions and the Agent type forwarders, update `PublicAPI.Unshipped.txt`, then rerun the focused tests.

## 2. Tool

- [x] 2.1 Add red tests in `tests/Lunate.Coding.Tests/Tools` for create, replace, clear, one-in-progress, invalid status/text, the 20-item cap, the no-approval risk and the event emitted only on success; confirm they fail.
- [x] 2.2 Implement `TodoTool` with the hand-written schema and shared validator, and its model-facing summary.
- [x] 2.3 Add red tests for core registration and implement the registration in `HarnessFactory.CreateTools`; rerun the focused tests.

## 3. Session replay

- [x] 3.1 Add red tests in `tests/Lunate.Coding.Tests/Sessions` that a loaded session reconstructs the last valid todo call on the active path, skips an invalid later call, and reconstructs nothing without a call; confirm they fail.
- [x] 3.2 Implement the replay from `Session.ToHistory()` through the same validator and wire it into the resume path.

## 4. TUI

- [x] 4.1 Add red tests in `tests/Lunate.Tui.Tests` for the todo lines, three distinguishable statuses, the five-line cap with overflow, clipping, the empty frame staying unchanged and a child event being ignored; confirm they fail.
- [x] 4.2 Add the todo state, input, reducer and bounded renderer block; wire the `TodoUpdated` arm in `InteractiveSession` with the child guard and update the focused tests.

## 5. JSON and print mode

- [x] 5.1 Add red tests in `tests/Lunate.Coding.Tests/Print` for the `todo_updated` golden line, one line per update, a refused update emitting none, and the default output staying the answer; confirm they fail.
- [x] 5.2 Map `TodoUpdated` in `PrintEventJson`, add the diagnostics and any other exhaustive switch arm, then rerun the focused tests.

## 6. Documentation

- [x] 6.1 Update the user-facing tool, live-area, session and JSON docs that enumerate the core tools or event kinds.
- [x] 6.2 Write the changelog fragment at `changes/<pr-number>.md` and commit it before the archive.

## 7. Verification

- [ ] 7.1 Run `dotnet csharpier format .`, the touched Release builds and focused test classes, and `openspec validate add-todo-tool --type change --strict`; fix failures.
- [ ] 7.2 Run `openspec validate --all --strict`, then sync and archive the change as the branch's last commit.
