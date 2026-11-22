# ADR Review Manifest

- Status: completed
- Review date: 2026-11-23

## Review Summary

ADR review completed for this change. The change implements the wire half of an already accepted
decision — ADR-0040 binds protocol version 3, the `event/` projection and the handshake refusal —
so no new durable decision is recorded. The projection's home in the wire package with `--json`
delegating, the bounded pump and the start condition are implementation choices inside ADR-0040's
decision, not commitments future changes must inherit.

## In-Force ADRs Reviewed

- `adr/0017-extensibility-architecture.md` — the extension contract, hook catalogue and trust model
  this wire carries.
- `adr/0017-addendum-remote-host.md` — the NDJSON JSON-RPC wire, declaration-driven proxies and the
  `initialize` handshake the negotiation extends.
- `adr/0017-addendum-commands.md` — the precedent for adding a request kind to the wire and raising
  the protocol version.
- `adr/0002-layering-and-meai-model-types.md` — the layer graph that keeps the projection below
  `Lunate.Coding` and the event union in `Lunate.Contracts`.
- `adr/0007-live-area-concurrency.md` — `System.Reactive` stays confined to the TUI; the contract and
  the wire use BCL `IObservable`/`IAsyncEnumerable` only.
- `adr/0040-hook-based-extensibility-on-observables.md` — the decision this change implements: the
  `event/` projection over protocol version 3 with the same bounded drop and the handshake refusal.
- `adr/0043-contract-unions.md` — `AgentEvent` and the hook-result families are unions; the wire
  never serialises a union as a union.

## New Durable ADRs Created

- None - no major durable architectural decisions were introduced; ADR-0040 already binds the
  protocol version, the notification form and the refusal path.
