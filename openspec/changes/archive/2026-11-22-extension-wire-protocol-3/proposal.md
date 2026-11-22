## Why

ADR-0040 gives an in-process extension one observing surface, `IExtensionContext.Events`, but its
observable half stops at the process boundary: a remote manifest that declares `events` gets one
warning and observes nothing. The ADR binds the wire projection to protocol version 3, and #996
shipped the in-process surface with that projection explicitly deferred to this card (#988). Until
it lands, the manifest promises parity the wire cannot deliver and an out-of-process logger,
metrics exporter or UI cannot be written at all.

## What Changes

- **Protocol version 3.** `initialize` gains `events` in both directions: the host sends the
  manifest's watch list, the child answers with the kinds it accepts (or `["*"]`). A child that
  answers a version below 3 still loads; if the manifest declares `events`, the handshake refuses
  it with a message naming protocol 3 — never a silent ignore.
- **`event/<kind>` notifications, host → child.** Each notification's params are the same JSON
  object one `--json` line carries (`type`, `runId`, `sessionId`, `parentRunId`, `source` and the
  event's own fields). Never awaited, never answered: an event has no result and no place on the
  run's path, exactly as in-process.
- **One projection, no second event model.** The `--json` projection moves from `Lunate.Coding`
  into the wire package and `--json` calls it, so the stream, the wire and the documented kind
  vocabulary cannot drift. The wire is the projection filtered by the manifest's declaration; the
  in-process surface stays unfiltered.
- **Bounded, isolated delivery.** The host drains the subscription through one pump with a bounded
  queue (256, the in-process capacity); overflow drops the oldest event, counts it and warns once
  naming the extension. An event line over the wire's line cap is dropped with one warning, not a
  torn-down wire. A broken child never takes the run or the other extensions with it.
- **A `events`-only remote extension starts at session start**, so an observer needs no hook,
  service or command to be alive when the first run emits.
- **The observability of the wire is tested end to end**: a conformance inventory round-trips every
  `AgentEvent` record over a real wire pair, the conformance runner gains an `event notification`
  step, and the node reference extension records what it observes and answers an `observed`
  command, so its fitness test runs one real turn through the host and asserts the child saw it from
  `run_started` to `run_finished`.
- **No wire type reaches `Lunate.Extensibility.Abstractions`.** The new wire DTOs stay in
  `Lunate.Extensibility.Remote`; a test pins the contract assembly's referenced assemblies and
  types, so the boundary cannot rot.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `extensions`: the observable event stream reaches an out-of-process extension as the `event/`
  wire projection over protocol version 3 — the handshake's `events` negotiation, the refusal of a
  pre-3 child that declares events, the bounded drop and per-event cap on the wire, and the
  conformance coverage of the projection.

## Impact

- `Lunate.Extensibility.Remote`: `RemoteProtocol` (version 3, `event/` method, the event-queue
  capacity), `InitializeRequest`/`InitializeResult` `events`, the `RemoteExtension` subscription
  pump, the moved event projection.
- `Lunate.Extensibility`: the loader starts an `events`-only remote extension at session start and
  drops the "not yet" warning; the stream itself is unchanged.
- `Lunate.Coding`: `--json` delegates to the moved projection, so behaviour and bytes stay put.
- `Lunate.Extensibility.Testing`: the event conformance inventory and the runner's
  `event notification` step.
- `samples/extensions/node-remote`: protocol 3, `events: ["*"]`, the `observed` command and the
  end-to-end turn fitness test.
- Specs, `docs/extensions-remote.md` (protocol 3, the method table and the event section), the
  event-stream contribution page, the manifest page, `docs/json-events.md`, and the published
  `schemas/extensions-remote.schema.json`.
- No new package, no new dependency, no change to `Lunate.Extensibility.Abstractions`, no change to
  the extension API version, no session-format change.

## Intent decisions

The grill-me interview was run unattended, answering each question from the card, ADR-0040, the
specs and the code.

- **Does a version-2 child load?** Yes, when the manifest declares no `events` (the version gate is
  about the projection, not the handshake). With `events` declared, the refusal names protocol 3.
- **Is the child's `events` answer an authority or an acceptance list?** Acceptance. The manifest is
  the watch list (ADR-0040: "a remote filter needs a declaration"); the host sends only the
  manifest's kinds the child accepts. A child accepting more than the manifest never sees them, and
  that is not a refusal.
- **Where does the projection live?** In the wire package, with `--json` delegating to it: one
  switch, one vocabulary, no second event model. The wire carries no `costUsd` because pricing is a
  `--json` presentation concern the child cannot compute.
- **Does the wire add a second queue?** One pump with the same bounded capacity as the in-process
  subscription, because the child's stdin can block and the run must not wait on it.
- **When does an observer-only remote extension start?** At session start, the same point commands
  already start a child, so the first turn is not missed.
- **Is the `events` list validated against the `--json` kinds?** No: the declaration is already
  free-form information in-process, and an unknown kind simply matches nothing. The wire names
  kinds from the same projection, so the vocabulary is shared without a second parser.
- **Are event notifications acknowledged?** No. There is no round trip and no failure policy per
  event: an observer has no opinion, and delivery loss is the bounded drop, counted and warned.
