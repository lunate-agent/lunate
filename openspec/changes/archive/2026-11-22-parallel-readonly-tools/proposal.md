## Why

A turn that reads five files or runs four greps pays five sequential round trips even though the
calls are independent. The loop's per-tool `Concurrency` intent has been recorded since T-38, but no
executor honours it, so read-only exploration is slower than it needs to be.

## What Changes

- When one turn returns several tool calls and every one of them is a core read-only tool (`read`,
  `grep`, `find`, `ls`) or a network-read-only web tool (`web_search`, `web_fetch`), execute the
  batch concurrently under a fixed small cap.
- Keep a mixed batch (any write, edit or `bash` call beside reads) serial and in order.
- Emit each call's `ToolCallStart`/`ToolCallArgs`/`ToolCallEnd` in the turn's call order, then emit
  the results in that same order, so the stream stays deterministic and every result carries its own
  call id.
- Approve each call independently; a declined call becomes its error result and never cancels its
  siblings. A failing call becomes its own error result; siblings still finish and report separately.
- Honour a tool's `Concurrency = Sequential` intent: such a call keeps the whole batch serial.
- Add a constant concurrency cap and a test that observes the maximum number of tools in flight.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `agent-loop`: read-only tool batches execute concurrently under a cap, with deterministic events,
  per-call approval and independent failures.
- `agent-tools`: the per-tool `Concurrency` intent now participates in scheduling; `Sequential`
  keeps a tool out of the parallel batch.

## Impact

- `src/Lunate.Agent/Loop/AgentHarness.Tools.cs`: split preparation (hooks and approval) from tool
  execution so bodies can run concurrently while approvals stay per call.
- `src/Lunate.Agent/Loop/AgentHarness.cs`: choose the parallel batch path for an all-read-only turn.
- `tests/Lunate.Agent.Tests/Loop/`: wall-time, event-order, approval, failure and cap tests using
  scripted tools.
- No new dependency and no public contract change; `ITool.Concurrency` already exists.
