# ADR Review Manifest

- Status: completed
- Review date: 2026-11-16

## Review Summary

The choice between a C# 15 `union` wrapper and a `closed` hierarchy on the extension-facing contract
is durable: it fixes how every future closed set on the contract is shaped, and it is the decision
#192 deferred to its own card. It is recorded as a new ADR ([0042](../../../adr/0042-contract-closed-sets.md))
rather than an addendum, because it answers a question no in-force ADR asks and it corrects the
inventory that assumed the answer. No other ADR changes: ADR-0041 made the shape available,
ADR-0040's observer surface consumes the closed set unchanged, and ADR-0002's rule that the contract
owns the event type still holds.

## In-Force ADRs Reviewed

- ADR-0002 — Three-layer project graph and Microsoft.Extensions.AI model types
- ADR-0012 — The tool contract
- ADR-0013 — The agent loop and the harness surface
- ADR-0017 and its addenda — Extensibility architecture, the contract assembly, the remote host and packages
- ADR-0023 — A first-party extension tier beside `samples/`
- ADR-0040 — Hook-based extensibility on observables
- ADR-0041 — Everything targets `net11.0` with no multi-targeting

## New Durable ADRs Created

- [`adr/0042-contract-closed-sets.md`](../../../adr/0042-contract-closed-sets.md) — the contract's
  closed sets are `closed` hierarchies, not `union` wrappers; the shape rule, the measurements and
  the rule that adding a case is a contract change with a version note.

## ADR Addenda

_None._ The inventory in [`docs/dev/csharp-15.md`](../../../docs/dev/csharp-15.md) is a live
instruction document, not a record, so the correction lands there directly.
