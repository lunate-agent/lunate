## Why

A long run has no place to record what it intends to do, so it re-derives the plan each turn and loses the thread — the shape [#858](https://github.com/lunate-agent/lunate/issues/858) records, 50 steps against a baseline's 29. A small planning tool that keeps one short, replaceable list gives the model a stable plan and gives the person watching a bounded view of it.

## What Changes

- Add a core `todo` tool in `Lunate.Coding`: the model sends one full list of items, each with `text` and a `status` of `pending`, `in_progress` or `done`; each call replaces the previous list and at most one item may be `in_progress`.
- Emit a `TodoUpdated` extension event with the complete list on every successful call, so print mode's `--json` stream and every other event consumer see each update.
- Show the current list in the TUI live area as a fixed, bounded block above the input; a session with no `todo` call renders exactly as today.
- Replay the list from the session's existing tool-call messages on resume: no new session entry, no schema version and no golden-file change.
- Register `todo` in the core tool set (not the optional search-tool group), so every frontend and child run offers it without a setting.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `agent-tools`: define the `todo` tool, its arguments, validation (one `in_progress` at most, a bounded short list) and its replacement semantics.
- `agent-events`: add the `TodoUpdated` extension event carrying the complete list.
- `tui`: render the bounded todo block in the live area and leave the frame unchanged when the list is empty.
- `print-mode`: emit one `todo_updated` JSON line per update with the full list.
- `agent-sessions`: replay the current list from the session's tool-call messages on resume without changing the session format.

## Impact

- `Lunate.Contracts`: the `TodoItem` value, its status set and the `TodoUpdated` event in the extension union; the public API additions are tracked in `PublicAPI.Unshipped.txt`.
- `Lunate.Coding`: the tool, core registration, TUI event handling, resume replay and the `--json` projection.
- `Lunate.Tui`: the live-area state and renderer for the bounded list.
- No new NuGet package, no session schema change and no golden session fixture change.
