# ADR Review Manifest

- Status: completed
- Review date: 2026-11-16

## Review Summary

The choice of shape for the extension-facing contract's closed sets is durable and belongs in its own
record: it fixes how every closed set on the contract is declared, and it is the decision #192
deferred to its own card. [ADR-0043](../../../adr/0043-contract-unions.md) records it and supersedes
[ADR-0042](../../../adr/0042-contract-closed-sets.md), which had chosen `closed` hierarchies; both
link to each other and 0042 keeps its measurements as the evidence 0043 reuses. No other ADR changes:
ADR-0041 made the shape available, ADR-0040's observer surface consumes the closed set unchanged, and
ADR-0002's rule that the contract owns the event type still holds.

## In-Force ADRs Reviewed

- ADR-0002 — Three-layer project graph and Microsoft.Extensions.AI model types
- ADR-0012 — The tool contract
- ADR-0013 — The agent loop and the harness surface
- ADR-0017 and its addenda — Extensibility architecture, the contract assembly, the remote host and packages
- ADR-0023 — A first-party extension tier beside `samples/`
- ADR-0040 — Hook-based extensibility on observables
- ADR-0041 — Everything targets `net11.0` with no multi-targeting

## New Durable ADRs Created

- [`adr/0043-contract-unions.md`](../../../adr/0043-contract-unions.md) — the contract's closed sets
  are C# 15 unions over their case records; the shape, the stamped-state base, the consumer idiom and
  the rule that adding a case is a contract change with a version note.

## Superseded ADRs

- [`adr/0042-contract-closed-sets.md`](../../../adr/0042-contract-closed-sets.md) — accepted earlier
  on this card, superseded by ADR-0043 (link both ways).

## ADR Addenda

_None._ The inventory in [`docs/dev/csharp-15.md`](../../../docs/dev/csharp-15.md) is a live
instruction document, not a record, so the correction lands there directly.
