## 1. The projection moves to the wire package

- [ ] 1.1 Move the event → `--json` switch from `Lunate.Coding/Print/PrintEventJson.cs` to
      `Lunate.Extensibility.Remote/Wire/RemoteEventJson.cs` (`Serialize`, `Describe`, `Kinds`,
      `HandledEventTypes`), unchanged in behaviour.
- [ ] 1.2 `PrintEventJson` stays in `Lunate.Coding` as a thin adapter that computes `costUsd` and
      delegates; `PrintEventJsonTests` (including the type-set reflection test) passes unchanged.
- [ ] 1.3 Red: `RemoteEventJson` serialises every `AgentEvent` record to the line the documented
      `--json` shape names, and `HandledEventTypes` matches the union's case set.

## 2. Protocol version 3 and the handshake

- [ ] 2.1 Red: `RemoteProtocol.Version` is 3, `event/<kind>` is the notification method, and the
      capability list names event notifications.
- [ ] 2.2 Red: `initialize` carries the manifest `events` list and the result's `events` reads as
      empty when absent (a version-2 answer still deserialises).
- [ ] 2.3 Red: a version-2 child with no `events` declared loads; with `events` declared the
      handshake refuses naming protocol version 3.
- [ ] 2.4 Red: a manifest declaration filters the projection; a child accepting fewer kinds warns,
      and a child accepting none is sent nothing.
- [ ] 2.5 Implement the negotiation and refusal in `RemoteExtension.HandshakeAsync`; update the
      version-mismatch test and the handshake assertions.

## 3. The host-side projection and lifecycle

- [ ] 3.1 Red: after the handshake the extension subscribes once and an emitted event reaches the
      child as `event/<kind>` with the `--json` line as its params.
- [ ] 3.2 Red: a slow child drops the oldest events, counts them and warns once; the run is not
      delayed.
- [ ] 3.3 Red: an event line over the wire cap is dropped with one warning and the wire survives.
- [ ] 3.4 Red: a crash, a restart and a shutdown each end the subscription; a restart subscribes
      again.
- [ ] 3.5 Implement the bounded pump (`RemoteExtension.Events.cs`), the subscription lifecycle and
      the drop/cap warnings.
- [ ] 3.6 Red: a remote manifest declaring only `events` starts at session start; the loader drops
      the "protocol version 3 is a follow-up" warning and generalises its contained start failure.

## 4. Conformance, schema and the reference extension

- [ ] 4.1 `RemoteEventConformance` inventory with one sample per `AgentEvent` record and the
      reflection `MissingCoverage()` check.
- [ ] 4.2 Red: every observable record round-trips over a real wire pair as the `--json` line, and
      no wire type is defined in or referenced by `Lunate.Extensibility.Abstractions`.
- [ ] 4.3 `RemoteConformanceRunner` gains the `event notification` step and its options; the
      published schema gains `events` on both handshake DTOs and the `event.notification` entry.
- [ ] 4.4 Node sample: protocol 3, `"events": ["*"]`, records `event/<kind>`, `observed` command,
      and the manifest lists it.
- [ ] 4.5 Red: the node sample's fitness test runs one real turn through `ExtensionTestHost` and the
      child reports `run_started` … `run_finished` in order; the conformance test asserts the new
      step passes.

## 5. Documentation, gate and archive

- [ ] 5.1 `docs/extensions-remote.md`: protocol 3, the method table, the negotiation, the refusal,
      the bounded drop and the cap, the runner option, the sample.
- [ ] 5.2 `docs/extensions/contributions/event-stream.md`, `docs/extensions/manifest.md`,
      `docs/json-events.md` and `docs/dev/spec/extensibility.md` describe the wire projection as
      shipped.
- [ ] 5.3 Regenerate `schemas/extensions-remote.schema.json`; `changes/<pr>.md` line for the
      changelog.
- [ ] 5.4 `openspec validate extension-wire-protocol-3 --strict`, `dotnet csharpier format .`,
      Release build and the named test classes green; archive as the last commit.
