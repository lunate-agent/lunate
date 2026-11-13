# Read the `--json` event stream

This page answers what `lunate -p --json` writes, and the short answer is one JSON object per line for
every event of the run, in arrival order: a stamped envelope (`type`, `runId`, `sessionId`,
`parentRunId`, `source`) plus the fields of that event type.

## Stream one line per event

With `--json` every event of the run is written to stdout as one JSON object per line in arrival
order, reasoning events included, with the same stamped fields as the event records. Each line is
parseable on its own, so a consumer reads the stream as it arrives instead of waiting for the answer
`--json` replaces.

<!-- run: no needs a model request -->
```bash
lunate -p --json "list the files you changed" > events.jsonl
```

Every line carries `type` and `runId`; `sessionId` is present when the run has a session,
`parentRunId` on a child run's events, and `source` only when it is not `core`. A consumer scopes
events by `runId` and never assumes one run per stream: a child run's events keep their own ids.

Two example lines, the run's start and its finish:

```json
{"type":"run_started","runId":"run_1","sessionId":"s_20261110-090000-ab12"}
{"type":"run_finished","runId":"run_1","sessionId":"s_20261110-090000-ab12","stopReason":"stop"}
```

Diagnostics do not travel in the stream: they stay on stderr, exactly as in [print mode](print.md).

## Know the event types

### Run lifecycle

| `type` | Fields | Meaning |
| --- | --- | --- |
| `run_started` | — | the run began |
| `run_finished` | `stopReason` | the run ended; see the vocabulary below |
| `run_error` | `message` | the run failed with this message |
| `retrying` | `attempt`, `reason` | a transient provider error is being retried |

`stopReason` is one of `stop` (finished normally), `cancelled`, `step_limit`, `cost_limit`,
`time_limit`, `length` (the model's token limit cut the turn off) or `token_limit`.

### Text and reasoning

| `type` | Fields |
| --- | --- |
| `text_message_start`, `text_message_end` | `messageId` |
| `text_message_content` | `messageId`, `text` |
| `reasoning_start`, `reasoning_end` | `messageId` |
| `reasoning_message_content` | `messageId`, `text` |

Reasoning fragments are bracketed by `reasoning_start` and `reasoning_end` per reasoning message, in
arrival order, like every other event.

### Tools

| `type` | Fields |
| --- | --- |
| `tool_call_start` | `callId`, `toolName`, optional `parentToolCallId` |
| `tool_call_args` | `callId`, `args` (the arguments JSON as a string), optional `parentToolCallId` |
| `tool_call_end` | `callId`, optional `parentToolCallId` |
| `tool_call_result` | `callId`, `output`, `isError`, optional `details`, optional `parentToolCallId` |
| `tool_progress_update` | `callId`, `message` |
| `tool_output_spill_failed` | `message` |

`tool_call_result` carries the tool's detail record as a JSON element in `details` — for an edit, the
path, the line range, the match tier and the diff. A nested call id reads `<parent>/<n>`, so a
subagent's tool calls are distinguishable from the parent's.

### Approvals and decisions

| `type` | Fields |
| --- | --- |
| `approval_requested` | `callId`, `toolName`, `args`, optional `workspaceRoot` |
| `decision_evaluated` | `questionId`, `modelId`, `method`, `answer`, `option`, `probability`, plus optional `distribution`, `confidence`, `confidenceThreshold`, `outcome`, `strategy`, `warning` |
| `decision_unavailable` | `questionId` |
| `decision_failed` | `questionId`, `modelId`, `reason` |
| `model_routed` | `requestedModel`, `resolvedModel` |
| `steering_injected` | optional `entryId` |

A decision event's extra fields are the ones [Decisions](decisions.md) describes; a run without a
decision profile emits none of the three decision types.

### Usage, limits and the window

| `type` | Fields |
| --- | --- |
| `usage_updated` | `usage`, optional `cacheReadTokens`, `cacheWriteTokens`, `modelId`, `costUsd` |
| `compaction_applied` | `replacedEntryIds`, `estimatedTokensAfter` |
| `step_limit_reached` | `maxSteps` |
| `cost_limit_reached` | `maxCostUsd`, `spendUsd` |
| `time_limit_reached` | `maxTimeSeconds`, `elapsedSeconds` |

`usage_updated` carries the provider's prompt-cache counts beside the unchanged `usage` object:
`cacheReadTokens` and `cacheWriteTokens`, each emitted only when the provider reported a positive
count. A reported zero is treated as unknown and omitted, so the two are never synthetic zeros.
`costUsd` is a number when the record's model is in the catalog and every token lane with a positive
count has a known price, and the field is absent for an unknown or unpriced model rather than `0`.

## Related

- [Run one prompt and exit](print.md) — the mode that writes this stream, and its exit codes
- [Decisions](decisions.md) — the profile behind the decision events
- [Run limits](usage.md#run-limits) — the limits the limit events report
- [Models and providers](models-and-providers.md) — the catalog that prices `costUsd`
