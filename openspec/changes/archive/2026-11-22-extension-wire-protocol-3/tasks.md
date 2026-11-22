## 1. The projection moves to the wire package

- [x] 1.1 Move the event → `--json` switch from `Lunate.Coding/Print/PrintEventJson.cs` to
      `Lunate.Extensibility.Remote/Wire/RemoteEventJson.cs` (`Serialize`, `Describe`, `Kinds`,
      `HandledEventTypes`), unchanged in behaviour.
- [x] 1.2 `PrintEventJson` stays in `Lunate.Coding` as a thin adapter that computes `costUsd` and
      delegates; `PrintEventJsonTests` (including the type-set reflection test) passes unchanged.
- [x] 1.3 Red: `RemoteEventJson` serialises every `AgentEvent` record to the line the documented
      `--json` shape names, and `HandledEventTypes` matches the union's case set.

## 2. Protocol version 3 and the handshake

- [x] 2.1 Red: `RemoteProtocol.Version` is 3, `event/<kind>` is the notification method, and the
      capability list names event notifications.
- [x] 2.2 Red: `initialize` carries the manifest `events` list and the result's `events` reads as
      empty when absent (a version-2 answer still deserialises).
- [x] 2.3 Red: a version-2 child with no `events` declared loads; with `events` declared the
      handshake refuses naming protocol version 3.
- [x] 2.4 Red: a manifest declaration filters the projection; a child accepting fewer kinds warns,
      and a child accepting none is sent nothing.
- [x] 2.5 Implement the negotiation and refusal in `RemoteExtension.HandshakeAsync`; update the
      version-mismatch test and the handshake assertions.

## 3. The host-side projection and lifecycle

- [x] 3.1 Red: after the handshake the extension subscribes once and an emitted event reaches the
      child as `event/<kind>` with the `--json` line as its params.
- [x] 3.2 Red: a slow child drops the oldest events, counts them and warns once; the run is not
      delayed.
- [x] 3.3 Red: an event line over the wire cap is dropped with one warning and the wire survives.
- [x] 3.4 Red: a crash, a restart and a shutdown each end the subscription; a restart subscribes
      again.
- [x] 3.5 Implement the bounded pump (`RemoteExtension.Events.cs`), the subscription lifecycle and
      the drop/cap warnings.
- [x] 3.6 Red: a remote manifest declaring only `events` starts at session start; the loader drops
      the "protocol version 3 is a follow-up" warning and generalises its contained start failure.

## 4. Conformance, schema and the reference extension

- [x] 4.1 `RemoteEventConformance` inventory with one sample per `AgentEvent` record and the
      reflection `MissingCoverage()` check.
- [x] 4.2 Red: every observable record round-trips over a real wire pair as the `--json` line, and
      no wire type is defined in or referenced by `Lunate.Extensibility.Abstractions`.
- [x] 4.3 `RemoteConformanceRunner` gains the `event notification` step and its options; the
      published schema gains `events` on both handshake DTOs and the `event.notification` entry.
- [x] 4.4 Node sample: protocol 3, `"events": ["*"]`, records `event/<kind>`, `observed` command,
      and the manifest lists it.
- [x] 4.5 Red: the node sample's fitness test runs one real turn through `ExtensionTestHost` and the
      child reports `run_started` … `run_finished` in order; the conformance test asserts the new
      step passes.

## 5. Documentation, gate and archive

- [x] 5.1 `docs/extensions-remote.md`: protocol 3, the method table, the negotiation, the refusal,
      the bounded drop and the cap, the runner option, the sample.
- [x] 5.2 `docs/extensions/contributions/event-stream.md`, `docs/extensions/manifest.md`,
      `docs/json-events.md` and `docs/dev/spec/extensibility.md` describe the wire projection as
      shipped.
- [x] 5.3 Regenerate `schemas/extensions-remote.schema.json`; `changes/<pr>.md` line for the
      changelog.
- [x] 5.4 `openspec validate extension-wire-protocol-3 --strict`, `dotnet csharpier format .`,
      Release build and the named test classes green; archive as the last commit.

## 6. Rework: the priced line and the startup reconciliation

- [x] 6.1 Red: a priced `usage_updated` reaches the child with the same `costUsd` the `--json` line
      carries, and an unpriced record omits it on both; the wire takes the price from the
      composition's catalog seam (`ExtensionWireCost` → `ExtensionHostOptions.CostUsd` →
      `RemoteExtensionOptions.CostUsd`), so the wire package still references only the contracts.
- [x] 6.2 Red through the loader: a host composed with the `CostUsd` seam sends the priced line to
      the observer.
- [x] 6.3 The delta spec documents the priced line and MODIFIES *Out-of-process extension host*, so
      the lazy-start rule and the `events` session start no longer read as incompatible; the
      untouched scenario for `background` names that it declares no `events`.
- [x] 6.4 Red: an `events` declaration starts a `background` child at session start, pinning the
      exception the reconciled requirement states.
- [x] 6.5 Re-archive as the last commit after `openspec validate --all --strict`.
