# Design: parallel read-only tool batches

## Context

`AgentHarness` currently walks a turn's tool calls one at a time: `ExecuteCallAsync` emits
`ToolCallStart`/`ToolCallArgs`/`ToolCallEnd`, runs the hook and approval seam, executes the tool,
admits the output, appends history and emits `ToolCallResult` before the next call starts. The tool
contract already carries `ToolConcurrency` (ADR-0012 / `agent-tools`), but nothing consumes it. The
loop, event path and approval seam are fixed by ADR-0003 and ADR-0013; this change schedules inside
that loop and adds no dependency or public contract member.

The dynamic view of one parallel batch:

```mermaid
sequenceDiagram
    participant Loop as AgentHarness loop
    participant Ch as AgentEventChannel
    participant Prep as prepare (hooks + approval)
    participant Body1 as read body 1
    participant BodyN as read body N
    participant Hist as history/session

    Loop->>Ch: start/args/end (call 1..N, call order)
    loop each call in order
        Loop->>Prep: ToolCalling hook, approval
        Prep-->>Loop: approved args or error result
    end
    par under cap
        Loop->>Body1: ExecuteAsync
    and
        Loop->>BodyN: ExecuteAsync
    end
    Body1-->>Loop: result (own call id)
    BodyN-->>Loop: result (own call id)
    loop each call in order
        Loop->>Ch: buffered tool events, then ToolCallResult
        Loop->>Hist: append tool message (call order)
    end
```

## Goals / Non-Goals

**Goals:**

- A turn of N independent core read-only or web read-only calls finishes in less wall time than N
  serial calls, under a fixed small cap.
- The event stream stays deterministic: start/args/end in call order, results in the same order,
  each with its own call id.
- Approval stays per call; a decline or a failure is local to its call.
- Mixed batches and `Concurrency = Sequential` tools keep today's serial path.

**Non-Goals:**

- Parallelising writes, edits, `bash`, MCP or extension tools, or the Roslyn `cs_*` tools.
- A user-tunable concurrency setting; this change fixes one constant.
- Changing the event contract or the `ITool` contract.
- Reordering history or session appends relative to call order.

## Decisions

### D1 — Eligibility is an explicit conservative allow-list

A batch is parallel only when every call resolves to `read`, `grep`, `find`, `ls`, `web_search` or
`web_fetch`, and every resolved tool has `Concurrency = Parallel`. The four file tools must also
report `ToolRisk.ReadOnly`; the two web tools are allowed by name because they deliberately declare
`Execute` for the approval policy while only reading the network. `todo`, the Roslyn tools and any
extension or MCP tool stay serial even when they report read-only risk: their internal state or
backend threading is not part of this card. A batch of one call uses the existing serial path.

### D2 — Preparation stays ordered; only tool bodies run concurrently

The hook (`ToolCalling`) and approval seam run per call, in call order, before any body starts. This
keeps prompts deterministic and preserves the existing "a decline is an error result" behaviour
without concurrent approvers. The tool body is the slow part (file I/O, network), so this split
captures the wall-time win without moving the safety seam.

### D3 — Start events first, results after, in call order

All `ToolCallStart`/`ToolCallArgs`/`ToolCallEnd` events are emitted in the turn's call order before
the bodies run. Bodies write into a per-call event buffer; after all bodies finish, the loop replays
each buffer and emits the call's `ToolCallResult` in call order. This makes the stream independent of
completion timing (a fast second read cannot overtake the first result) and keeps the event sequence
validator's start → args → end → result rule per call id.

### D4 — One cap constant, a semaphore around the bodies

`MaxParallelReadOnlyCalls = 4` is the one cap. A `SemaphoreSlim` admits at most that many bodies; the
constant is internal so a test can assert the observed maximum against it. Four matches the existing
default tree concurrency for subagents and is enough to hide per-call latency without turning a
turn into an unbounded fan-out.

### D5 — History, session and admission stay on the loop's thread

The concurrent bodies may write only through thread-safe seams (`ReadLedger` is already locked).
Output admission, spill bookkeeping, `_observationFacts`, history appends and session appends happen
in the ordered completion loop, so the session file, request history and spill sequence keep the
same order as a serial turn.

### D6 — Failure and cancellation are per call

A body exception becomes its call's error result, exactly as the serial path does; the other tasks
are not cancelled. A declined call produces its error result and its body never starts. Cancellation
still cancels the batch's shared token; every call whose start event was emitted gets a result
(synthetic "cancelled by the user" where its body did not complete), history is repaired and the run
ends with `RunFinished(cancelled)`. The `_danglingCalls` repair therefore never duplicates a result.

### D7 — Telemetry is unchanged in shape

Each batched call opens one `execute_tool` child span with the same tags, and the span records
`lunate.tool.is_error` from its own result. The spans nest under the run span exactly as the serial
path's do.

## Risks / Trade-offs

- **A read-only tool turns out to mutate shared state.** The allow-list is deliberately narrow, so
  only the reviewed core tools and the two web tools enter the concurrent path; extension, MCP,
  `todo` and Roslyn calls stay serial.
- **A tool emits events out of order.** Each body's event sink is buffered per call and replayed in
  call order, so the run stream remains deterministic even if a tool reports progress.
- **An interactive approval is slow.** Preparation is serial, so a prompt blocks the batch, not the
  loop's correctness; the wall-time win is in the bodies, which is where the measured cost lives.
- **Cancellation mid-batch leaves dangling starts.** The completion loop records a result for every
  started call before it rethrows, and removes the calls from the dangling set, so the outer repair
  only sees calls that never started.
- **Nested calls from a batched tool.** The allow-list tools do not nest; the per-call buffer and
  ordered replay still keep the event stream valid if a future allow-listed tool does.

## Migration Plan

No data migration, no configuration and no rollback artifact. Reverting the change restores the
serial loop; sessions and events are unchanged in format.

## Open Questions

None. The cap value and the allow-list are recorded here and can be widened by a later change without
touching the contract.
