# JSON events

This page answers what `lunate -p --json` writes and how to read it, and the short answer is: one JSON
object per line, in arrival order, one line per event of the run — so a script can follow the tools,
the usage and the stop reason instead of parsing prose.

## Read one line at a time

<!-- run: no a real run needs a provider credential and a paid model call -->

```bash
lunate -p --json "which files does the approval flow touch?" > run.jsonl
jq -r 'select(.type == "tool_call_start") | .toolName' run.jsonl
```

Every line is one complete object, newline-terminated, in the order the events arrived. There is no
array wrapper and no header line, so a run can be streamed while it happens. The final answer is not
printed in this mode: it is the concatenation of the `text_message_content` lines of the last message
that had any.

## Expect the stamp on every line

| Field | Meaning |
| --- | --- |
| `type` | the event type, in the table below |
| `runId` | the run the event belongs to |
| `sessionId` | the session, when the run has one (`--no-session` has none) |
| `parentRunId` | set on a child run's events forwarded onto the parent stream |
| `source` | who emitted it; omitted for the core |

Consumers scope by `runId` and never assume one run per stream: a subagent's events keep their own
`runId` and `sessionId` and carry `parentRunId`, so the two runs can never be confused.

## Recognise the event types

| `type` | Fields beyond the stamp |
| --- | --- |
| `run_started` | — |
| `run_finished` | `stopReason` (`stop`, `cancelled`, `step_limit`, `cost_limit`, `time_limit`, `length`, `token_limit`) |
| `run_error` | `message` |
| `text_message_start`, `text_message_end` | `messageId` |
| `text_message_content` | `messageId`, `text` |
| `reasoning_start`, `reasoning_end` | `messageId` |
| `reasoning_message_content` | `messageId`, `text` |
| `tool_call_start` | `callId`, `toolName`, `parentToolCallId` when nested |
| `tool_call_args` | `callId`, `args` (the raw JSON the model produced) |
| `tool_call_end` | `callId` |
| `tool_call_result` | `callId`, `output`, `isError`, `details` (the tool's own detail record) |
| `tool_progress_update` | `callId`, `message` |
| `tool_output_spill_failed` | `message` |
| `approval_requested` | `callId`, `toolName`, `args`, `workspaceRoot` |
| `usage_updated` | see the next section |
| `retrying` | `attempt`, `reason` |
| `compaction_applied` | `replacedEntryIds`, `estimatedTokensAfter` |
| `steering_injected` | `entryId` |
| `step_limit_reached` | `maxSteps` |
| `cost_limit_reached` | `maxCostUsd`, `spendUsd` |
| `time_limit_reached` | `maxTimeSeconds`, `elapsedSeconds` |
| `model_routed` | `requestedModel`, `resolvedModel` |
| `decision_evaluated` | `questionId`, `modelId`, `method`, `answer`, `option`, `probability`, and `distribution`, `confidence`, `confidenceThreshold`, `outcome`, `strategy`, `warning` when they apply |
| `decision_unavailable` | `questionId` |
| `decision_failed` | `questionId`, `modelId`, `reason` |

The event set is closed: a new event type arrives with its JSON mapping, so a `type` no table names
above is a version difference, not a free-form extension.

## Read usage, cache and cost

`usage_updated` carries the provider's own `usage` object unchanged, plus:

- `modelId` — the model the usage belongs to; a routed call reports the resolved model.
- `cacheReadTokens` and `cacheWriteTokens` — the prompt-cache counts the provider reported, each
  emitted only for a positive count. A reported zero is treated as unknown and left out, so a missing
  field means "the provider did not say", never "zero".
- `costUsd` — the record's cost in US dollars, present only when the model is in the catalog and every
  token lane with a positive count has a known price. An unknown or unpriced model omits the field
  rather than reporting `0`.

## Recognise the end of a run

Exactly one `run_finished` or `run_error` ends the stream, and nothing follows it. That is the
**top-level** run's terminal event — the one whose line has no `parentRunId`. A subagent's events are
forwarded onto the same stream carrying their own `runId` and a `parentRunId`, so a child
`run_finished` is not the end: the parent keeps emitting until its own terminal event arrives.
`run_finished` carries the stop reason of the run it belongs to, and the process exit code follows
the top-level one: `0` for `stop`, `2` for `length` and `step_limit`, `3` for `cost_limit`, `4` for
`time_limit`, `130` for `cancelled`. A limit that ended a run also has its own event carrying the
limit and the value reached.
[Print mode](print.md#read-the-exit-code) owns the code table a script branches on.

## Related

- [Print mode](print.md) — the mode `--json` belongs to, and its exit codes
- [CLI reference](cli.md) — the flags the run accepts
- [CI and unattended runs](ci.md) — where this stream replaces a human reading the output
