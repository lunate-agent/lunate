## ADDED Requirements

### Requirement: Remote observable event stream

The observable event stream SHALL reach an out-of-process extension over the wire as a declared
projection, never as a second event model. The projection SHALL be the same JSON object one
`--json` line carries — `type`, `runId`, `sessionId` when present, `parentRunId` on a child run,
`source` when it is not `core`, and the event's own fields — sent host → child as an `event/<kind>`
notification whose params are that object, where `<kind>` is the `--json` type name. Event
notifications SHALL never be awaited, never be answered and never carry a result: an observer has no
opinion and no place on the run's path.

The handshake SHALL negotiate the projection in both directions: the `initialize` request SHALL
carry the manifest's `events` watch list, and the result SHALL carry the event kinds the child
accepts (or `["*"]`). The wire protocol version SHALL be 3. A child that answers a version below 3
SHALL still load when the manifest declares no `events`; when the manifest declares `events`, the
handshake SHALL refuse the extension with a reason naming protocol version 3, never ignore the
declaration. A version-3 child that accepts no kinds while the manifest declares some SHALL be
warned about and sent none; the manifest's declaration SHALL be the projection filter, so a child
that accepts more kinds than the manifest declares SHALL never receive them.

The host SHALL subscribe to the session's event stream once per started child and SHALL drain the
subscription through one pump with a bounded queue of 256 events, so the run never waits on a
child's stdin. On overflow the oldest event SHALL be dropped, counted and warned about once naming
the extension; an event whose line exceeds the wire's line cap SHALL be dropped with one warning
naming the extension and the kind rather than breaking the wire. The subscription SHALL be disposed
when the child stops, crashes or is restarted, and a restart SHALL subscribe again. A remote
extension whose manifest declares `events` SHALL start at session start even when it declares no
hook, service or command, so an observer-only extension is live before the first run emits.

The wire DTOs of the projection SHALL live in `Lunate.Extensibility.Remote`; no wire type SHALL be
added to `Lunate.Extensibility.Abstractions`, which SHALL NOT reference the wire package. A
conformance inventory SHALL cover every `AgentEvent` record so a new event type cannot ship without
a wire round-trip.

#### Scenario: A remote extension observes a turn end to end

- **GIVEN** an installed remote extension declaring `events` whose child accepts them and speaks
  protocol 3
- **WHEN** a session runs one turn through the host
- **THEN** the child receives `event/run_started` through `event/run_finished` in emission order,
  each carrying the `--json` line object with its run id, and has changed nothing about the run

#### Scenario: A version-2 child still loads, and names protocol 3 when events are declared

- **GIVEN** a remote child that answers protocol 2
- **WHEN** the manifest declares no `events`
- **THEN** the extension loads and its hooks and services work exactly as before
- **AND WHEN** the manifest declares `events`
- **THEN** the handshake refuses the extension with a reason naming protocol version 3, and the
  session continues without it

#### Scenario: The manifest declaration filters the wire only

- **GIVEN** a remote manifest declaring `["tool_call_result"]` whose child accepts `["*"]`
- **WHEN** a turn emits `run_started`, `tool_call_result` and `run_finished`
- **THEN** the child receives only `event/tool_call_result`, while an in-process subscription in the
  same session still receives every event

#### Scenario: A slow child loses events, not the run

- **GIVEN** a started remote extension whose child stops reading stdin
- **WHEN** more events are published than the projection's queue holds
- **THEN** the oldest events are dropped, the drop is counted, exactly one warning names the
  extension, and the run and the other extensions are unaffected

#### Scenario: An oversized event is dropped, not the wire

- **GIVEN** a started remote extension and an event whose JSON line exceeds the wire's line cap
- **WHEN** it is projected
- **THEN** that event is dropped with one warning naming the extension and the kind, and the next
  event still reaches the child

#### Scenario: Every observable record round-trips and the boundary holds

- **GIVEN** the observable event conformance inventory
- **WHEN** every `AgentEvent` record is projected and sent over a real wire pair
- **THEN** each notification's method and params parse to the same JSON object `--json` writes, the
  inventory covers every event record, and no wire type is defined in or referenced by
  `Lunate.Extensibility.Abstractions`
