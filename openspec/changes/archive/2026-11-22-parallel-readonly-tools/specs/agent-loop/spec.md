## ADDED Requirements

### Requirement: Parallel read-only tool batches

When one model turn returns more than one tool call and every call resolves to a core read-only tool
(`read`, `grep`, `find`, `ls`) or a network-read-only web tool (`web_search`, `web_fetch`), the loop
SHALL execute those tool bodies concurrently under one fixed concurrency cap. A call whose tool
declares `Concurrency = Sequential` SHALL keep the whole batch serial. A batch containing any other
tool — `write`, `edit`, `bash`, a mutating extension or MCP tool — SHALL stay serial and in the turn's
call order.

For a concurrent batch the loop SHALL emit `ToolCallStart`, `ToolCallArgs` and `ToolCallEnd` for
every call in the turn's call order before the bodies run, and SHALL emit each `ToolCallResult` in
that same call order after the bodies complete, so the event stream is deterministic and every result
carries its own call id. Events emitted by a batched tool body SHALL be attributed to that call and
replayed before its result, in call order. History and session messages SHALL be appended in the same
call order.

Approval SHALL remain per call. A declined call SHALL become its own error result and SHALL NOT
cancel or decline its siblings. A failing, malformed or unknown call SHALL become its own error result;
the other calls in the batch SHALL still execute and report separately. Cancellation SHALL cancel the
whole batch and still leave a valid event stream and history.

#### Scenario: Independent reads finish faster than the serial path

- **GIVEN** a turn with four independent `read` calls whose tool bodies each wait the same short
  fixture delay
- **WHEN** the loop executes the batch concurrently
- **THEN** its wall time is below the sum of the four serial waits, and each result carries its own
  call id

#### Scenario: The event stream is deterministic

- **GIVEN** a turn with several read-only calls
- **WHEN** the same run is repeated
- **THEN** the calls' start/args/end events appear in call order, the results appear in the same call
  order with their own ids, and the event sequence validator reports no violation

#### Scenario: Approval stays per call and a decline is isolated

- **GIVEN** a read-only batch and an approver that declines one call and allows the others
- **WHEN** the batch executes
- **THEN** every call reaches its own result, the declined call is an error result, and the allowed
  calls still run

#### Scenario: A failing call does not cancel its siblings

- **GIVEN** a read-only batch whose first call throws or returns an error
- **WHEN** the batch executes
- **THEN** the first result is an error and every sibling still completes with its own result

#### Scenario: The concurrency cap is respected

- **GIVEN** more read-only calls in one turn than the cap
- **WHEN** the batch executes with bodies that record the number of calls in flight
- **THEN** the observed maximum never exceeds the constant cap and the remaining calls still complete

#### Scenario: A mixed batch stays serial

- **GIVEN** a turn with a read-only call beside a `write`, `edit` or `bash` call
- **WHEN** the loop processes the turn
- **THEN** the calls start and finish one at a time in call order, with no overlap

#### Scenario: Cancellation leaves a valid stream

- **GIVEN** a read-only batch in progress
- **WHEN** the run's token is cancelled
- **THEN** the batch stops, every started call has a result, the stream ends with
  `RunFinished(cancelled)`, and the next run sends a valid history
