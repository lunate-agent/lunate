## Context

Lunate has no place for the model to keep a plan. The loop re-derives intent every turn, and long runs lose the thread ([#858](https://github.com/lunate-agent/lunate/issues/858)). The tool contract lives in `Lunate.Contracts` (`ITool`, `AgentEvent` as the C# 15 union from ADR-0043); the loop runs tools itself (ADR-0003); the TUI paints one immutable live-area snapshot (ADR-0007); and sessions store assistant tool calls and tool results as ordinary messages (ADR-0015), which is how the `ask_user` pair already replays without a schema change.

The change must add one tool, one event kind, a bounded live-area block and a `--json` line, while leaving the session format and every golden fixture untouched.

## Goals / Non-Goals

**Goals:**

- Give the model a short, replaceable todo list with text and three statuses.
- Show it in the TUI without displacing the input and without changing a session that never uses it.
- Emit one full-list event per update so `--json` and any event consumer see the current plan.
- Reconstruct the list on resume from the existing tool-call messages.

**Non-Goals:**

- A task database, scheduling, dependencies, owners, priorities or cross-session plans.
- A TUI command that edits the list; the model owns it.
- A new session entry, schema version or golden-file update.
- A settings toggle or an optional-tool entry: the planning tool is part of the core set.

## Decisions

### The tool is core and stateless over one full-list argument

`TodoTool` implements `ITool` with one hand-written schema: `items`, an array of objects with `text` and `status`. Every call carries the whole list; there is no partial update verb. That keeps the state repair trivial (the latest valid call is the list) and matches how the model reasons about a plan. It is registered in `HarnessFactory.CreateTools` beside `read`/`write`/`edit`/`bash`, so print, TUI, ACP and child runs all get it. It is `ToolRisk.ReadOnly`, because it touches no workspace file, so it never opens an approval panel.

Validation is in the tool, not the event: non-empty text, one of three statuses, at most 20 items, and at most one `in_progress`. The 20-item cap makes "short list" concrete while the live area separately bounds what a person sees. A refused call returns an actionable error and emits nothing, so the previous list stays current.

Alternative considered: an optional tool like `grep`/`find`/`ls`. Rejected — a planning tool is the point of the card, and an opt-in setting would leave the long-run problem unsolved by default.

### The event is a full snapshot in the extension union

`TodoUpdated(RunId, Items)` is a sealed record deriving from `ExtensionEventBase`; `TodoItem(Text, Status)` carries the values and `TodoItemStatus` is a small public enum. The event joins both unions in `Lunate.Contracts`. Because ADR-0043 makes a new case a contract change, it rides `ExtensionApi.Current` `1.16.0` (still unpublished) and appears in `PublicAPI.Unshipped.txt`. The full list, not a delta, means a consumer can render from any one event and a dropped stream event never corrupts the view.

Alternative considered: string statuses in the event. Rejected: the closed status set is part of the behaviour, and an enum makes the TUI map and the JSON serializer exhaustive.

### The TUI owns a bounded view, the session owns the list

`TodoUpdated` feeds a todo list on the TUI's immutable live-area state. The renderer draws at most five lines — four items plus an overflow line, or fewer when the list is short — with a status glyph, clipped to the width and with ASCII fallbacks. The lines are part of the existing height budget, so the tail shrinks before the input box does; when the terminal is shorter than the frame, `LiveAreaTrim` removes transient lines first. Child-run events are ignored, because the parent session's list is the one on screen.

The TUI keeps its own small value type; `Lunate.Tui` references no other Lunate project. `Lunate.Coding` maps the contract event to it.

### Resume replays from the existing tool messages; JSON projects from the event

The current list is reconstructed by scanning `Session.ToHistory()` for the last `todo` call on the active path and parsing its arguments through the same validator. A call whose arguments do not validate is skipped. No entry is appended and no format changes, so golden sessions stay byte-identical and a session without a call replays nothing.

`PrintEventJson` maps the event to `{"type":"todo_updated", …, "items":[{"text":…,"status":"pending"|"in_progress"|"done"}]}`. Non-JSON print output stays the final answer. The same projection is the ACP/consumer fallback: an existing switch with a `_` arm ignores the kind, which the closed-set spec already permits.

### Component and flow view

```text
 Model
   │ todo(items[])
   ▼
 Lunate.Coding.TodoTool ──validates──> TodoUpdated event ──> TUI live area (max 5 lines)
   │                                         │
   │ tool result + call message              └──> PrintEventJson ("todo_updated")
   ▼
 Session JSONL (existing tool messages) ──resume──> replay last valid todo call
```

The boundary is deliberate: the tool and the session hold the list, the event is the one projection every frontend reads, and the TUI never parses a session file itself.

## Risks / Trade-offs

- [The event union change breaks an exhaustive switch] -> Add the arm to every consumer (TUI, JSON, diagnostics, ACP mapper if present) and let `CS8509` find any missed switch in the build.
- [A long list makes the live area useless] -> The tool caps the list at 20 and the renderer caps it at five lines with an explicit overflow count.
- [A resumed list differs from what was on screen] -> Replay uses the same parser and validation as the live call, and only the last valid call counts.
- [Child plans overwrite the parent's view] -> The TUI ignores `TodoUpdated` for child runs; a child's own session still records its calls.
- [The tool silently becomes a write path] -> It has no file or command access and is declared `ReadOnly`.

## Migration Plan

This is additive: no settings migration, no session version, no golden change. Code lands test-first in `Lunate.Contracts`, `Lunate.Tui`, `Lunate.Coding`, with a focused test for the tool, the event/JSON line, the renderer bound and the resume replay. Existing sessions simply have no `todo` call and render as before. Rollback is removing the tool registration and the event kind.

## Open Questions

None. The card fixes the tool, the statuses, the event projection and the resume source; the only judgement calls (core registration, five visible lines, 20-item cap) are recorded above. No in-force ADR is revisited; ADR-0043's rule that a new event case carries a version note is followed.
