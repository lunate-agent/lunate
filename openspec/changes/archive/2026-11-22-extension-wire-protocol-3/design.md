## Context

ADR-0040 decides one observing surface, `IExtensionContext.Events`, and binds the out-of-process
half to protocol version 3: "`event/` notifications carrying the same JSON line object `--json`
writes, never awaited, same bounded drop, and an `events` declaration against a protocol-2 host is
refused at the handshake rather than ignored". #996 implemented the in-process surface and left
this card the wire: the stream, its filtering, the sample and its conformance entry.

In force and reviewed: ADR-0017 and its remote-host addendum (the NDJSON JSON-RPC wire, declaration
driven proxies, the handshake), ADR-0002 (the layer graph; the event union is a `Lunate.Contracts`
type), ADR-0007 (`System.Reactive` is confined to the TUI), ADR-0040 (the decision this card
implements) and ADR-0043 (the contract's closed sets are unions). None of them moves.

The constraints that shape the design: (1) the wire package `Lunate.Extensibility.Remote` sits below
`Lunate.Extensibility` and `Lunate.Coding` and may reference only
`Lunate.Extensibility.Abstractions`; (2) the `--json` projection lives in `Lunate.Coding`, above
both, so the wire cannot call it; (3) the run's path must never wait on a child's stdin; (4) a
manifest that declares `events` must not be silently ignored.

```mermaid
sequenceDiagram
    participant Run as AgentHarness run
    participant FE as Front end (print / TUI / ACP)
    participant Stream as SessionEventStream
    participant Remote as RemoteExtension (host)
    participant Child as Remote extension child
    Run->>FE: AgentEvent
    FE->>Stream: Publish(event)
    Stream-->>Stream: one bounded queue per subscription
    Stream->>Remote: observer.OnNext(event) on the pump
    Remote-->>Remote: filter by manifest events, enqueue (bounded 256)
    Remote->>Child: event/<kind> notification (never awaited)
    Child-->>Child: observed; no answer
```

## Goals / Non-Goals

**Goals**

- A remote extension observes a whole turn, child runs included, in emission order, with the same
  kind vocabulary and line shape `--json` writes.
- A protocol-2 child keeps working; an `events` declaration against it is refused naming version 3.
- A slow, crashing or oversized-event child loses its own events, never the run or another
  extension, and the loss is counted and warned once.
- One projection implementation, owned by the wire package; `--json` delegates so the bytes cannot
  drift.
- No wire type in `Lunate.Extensibility.Abstractions`; the boundary is pinned by a test.

**Non-Goals**

- No change to `AgentEvent`, `IAgentEvent`, `SessionEventStream` or in-process delivery/filtering.
- No event requests, acknowledgements, replay, backfill or per-kind subscription protocol.
- No change to the hook catalogue, the command wire, the service wire or the contract API version.
- No second JSON schema for each event kind: the event line is documented by its one source
  (`docs/json-events.md`) and the schema names the kind enum and the stamped fields.
- Not dropping a field the `--json` line has: `costUsd` is on the wire line too, priced by the host
  from the same catalog estimate the print stream uses.

## Decisions

### 1. Protocol version 3 with an `events` negotiation in both directions

`RemoteProtocol.Version` becomes 3 and `MinimumVersion` stays 1. `InitializeRequest` gains `Events`
(the manifest's watch list as declared, `*` allowed) and `InitializeResult` gains `Events` (the
kinds the child accepts, `*` allowed, absent on a pre-3 answer reads as empty). A capability
`event-notifications` joins the host capability list.

The gate is the declaration, not the version: a child answering 1 or 2 loads exactly as before when
the manifest declares no `events`; when the manifest declares `events`, `HandshakeAsync` refuses with
"it speaks protocol 2 but the manifest declares 'events', which reaches a child only over protocol
version 3". Alternative: inferring support from the version — rejected, because a fork can speak 3
and accept nothing, and the hooks/commands wire already treats a capability as the statement of
intent.

### 2. The manifest is the filter; the child's answer is acceptance

The host sends only the manifest's declared kinds (all of them for `["*"]`) intersected with what
the child accepts. A child that accepts `["*"]` while the manifest names three kinds sees three; a
child that accepts fewer than the manifest warns once naming the missing kind and receives the rest;
a version-3 child that accepts nothing while the manifest declares events receives none and warns.
A child accepting more than the manifest is not a refusal — unlike an undeclared hook, acceptance
cannot leak a surface; it only lets the host choose. Alternative: refusing an over-wide acceptance
list — rejected, it would refuse the ordinary `["*"]` child for a narrow manifest.

### 3. One projection in the wire package; `--json` delegates

The event → JSON switch moves from `Lunate.Coding/Print/PrintEventJson.cs` into
`Lunate.Extensibility.Remote/Wire/RemoteEventJson.cs` as the wire's projection (`Serialize`,
`Describe`, `Kinds`, `HandledEventTypes`). Pricing is the one part of the line the wire package
cannot compute: `costUsd` needs the session catalog, and the wire package references only
`Lunate.Extensibility.Abstractions`. So `Serialize` takes an optional `Func<UsageUpdated,
decimal?>?`: `PrintEventJson` in `Lunate.Coding` passes the estimate `UsageCost` gives for the
catalog and the run's model, and the remote host passes the same estimate through
`ExtensionHostOptions.CostUsd` → `RemoteExtensionOptions.CostUsd`, which the composition sets from
`ExtensionWireCost.For(catalog)`. Both lines then carry the same fields and bytes, an unpriced
record omits `costUsd` on both, and `--json`'s reflection test is unchanged.

Alternatives: duplicating the switch in the wire package (rejected — "there is no second event
model"); moving it into `Lunate.Contracts` (rejected — a new public contract type and version bump
for a presentation concern, and the pricing logic does not belong there); injecting a
`Func<AgentEvent, string>` from the composition (rejected — a host that forgets it silently stops
projecting, and the type would still need a home for the conformance inventory); leaving `costUsd`
off the wire (rejected — the params would not be the `--json` line ADR-0040 binds, and a child could
not tell a priced line from an unpriced one); making the wire package reference `Lunate.Ai`
(rejected — the wire stays a lean contract package and the catalog owner already sits above it).

### 4. The subscription belongs to the started child, drained by its own pump

`RemoteExtension` subscribes to `_options.Context.Events` after a successful handshake when the
manifest declares `events`. `OnNext` only enqueues; a pump task serializes and writes
`wire.NotifyAsync("event/<kind>", line)`, so ordering is emission order and the child's stdin never
blocks the stream's pump. The queue is bounded at 256 (the in-process capacity): overflow dequeues
the oldest, counts it and warns once naming the extension. A line over the wire's `MaxLineBytes` is
dropped with one warning naming the extension and the kind instead of throwing `RemoteProtocolException`
and tearing the wire down. `OnCompleted`/`OnError` from the stream stop the pump; the subscription is
disposed in `StopChildAsync`, so a crash, a restart and an unload all end it, and a restart
subscribes again.

Alternatives: blocking inside `OnNext` so the stream's own queue drops (rejected — sync-over-async
on the stream's pump, and a blocked pump holds the subscription); no second queue (rejected — a
stalled child would grow the stream's queue and drop for the in-process side too, attributing the
loss to the wrong subscriber).

### 5. An observer-only remote extension starts at session start

`LoadRemoteAsync` starts the child when the manifest declares `commands` **or** `events` (renaming
`StartForDeclaredCommandsAsync` to `StartForHandshakeAsync` and generalising its contained-failure
warning). Without it, an `events`-only extension would never start — nothing would dispatch a hook,
call a service or list a command — and would observe nothing. The failure stays contained: a child
that cannot start leaves the extension's declarations and the load continues; a handshake refusal
reads as the extension's own refused state, with the reason naming protocol 3.

### 6. Conformance and the reference extension

`RemoteEventConformance` in `Lunate.Extensibility.Testing` holds one sample per `AgentEvent` record
and a `MissingCoverage()` reflection check, mirroring `RemoteConformance` for hooks. The conformance
runner gains an `event notification` step: when the child accepts events, it sends the probe events
as real `event/` notifications, then invokes `EventProbeCommand` (default `observed`) and requires
its output to name the kinds it received; a child with no such command is sent-only and the step is
best effort, and a child that accepts nothing marks the step not exercised.

The node sample becomes protocol 3, declares `"events": ["*"]`, records every `event/<kind>` it
receives and answers `observed` with the recorded kinds. Its fitness test runs a real turn through
`ExtensionTestHost` with a scripted chat client, then invokes `observed` and asserts `run_started`
and `run_finished` arrived in order — no double anywhere on the wire.

### 7. Documentation and schema

`docs/extensions-remote.md` gets protocol 3, the `event/<kind>` row, the negotiation, the refusal,
the bounded drop and the cap; the event-stream contribution page and the manifest page lose their
"follow-up card" caveat; `docs/json-events.md` names the wire as a second projection of the same
lines; `schemas/extensions-remote.schema.json` is regenerated with `events` on both handshake DTOs
and an `event.notification` entry whose `type` enum is `RemoteEventJson.Kinds`.

## Risks / Trade-offs

- **A child that stops reading loses events** → deliberate (ADR-0040): the drop is counted, warned
  once and named; the run keeps its pace.
- **A child that receives events while the manifest declares none** → impossible: the projection is
  built from the manifest, and the declaration is already shown by `/extensions`.
- **The projection move could alter `--json` bytes** → `PrintEventJson` delegates to the same code
  and its existing test suite (including the type-set reflection test) runs unchanged.
- **A huge `tool_call_result` exceeds the line cap** → dropped with one warning; the wire survives,
  and the per-event cap is documented.
- **An extension that declares `events` but its child is version 2** → refused at the handshake with
  the version named, so the author learns the requirement at first start rather than from silence.
- **A restart races queued events** → the subscription is disposed before the child stops, so a
  restarted child starts with an empty queue and a fresh subscription.

## Migration Plan

No stored data, no configuration and no session format migrate. A version-1 or version-2 child keeps
loading when it declares no `events`; a version-2 child whose manifest declares `events` is refused
until it speaks 3, which is the documented breaking edge and the reason the version moved. Rollback
is the revert of this change: a manifest `events` declaration is unknown-field-ignored by older
hosts, and a protocol-3 child is readable by the older host only for hooks and services.

## Open Questions

None. The remaining per-event caps beyond the line cap (a rate or count budget per kind) are not
named by ADR-0040 or this card and would be a separate change if a real child needs them.
