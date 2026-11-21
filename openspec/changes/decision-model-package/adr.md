# ADR Review Manifest

- Status: completed
- Review date: 2026-11-21

## Review Summary

ADR review completed for this change. ADR-0044 decided the model's home and named this package move
as its own follow-up; this change makes that move, so a new ADR amends ADR-0044 rather than rewriting
it (ADRs are immutable once accepted). No other in-force ADR needs revisiting: the layering rule,
the extension contract, the compile/load boundary and the release packaging all keep their shape, and
only the assembly that carries the model changes.

## In-Force ADRs Reviewed

- ADR-0002 — Three-layer project graph and Microsoft.Extensions.AI model types (and its decision
  addendum)
- ADR-0017 and its addenda — Extensibility architecture, the contract assembly and packages
- ADR-0028 — The decision layer primitive (and its decision-model addendum)
- ADR-0030 — Native decisions and the MEAI-shaped engine seam
- ADR-0041 — Everything targets `net11.0`
- ADR-0043 — The contract's closed sets are unions (no event shape moves)
- ADR-0044 — The decision model belongs to the decision layer (amended, not rewritten)

## New Durable ADRs Created

- [`adr/0045-decision-model-package.md`](../../../adr/0045-decision-model-package.md) — the model's
  home is its own package: the model and seams move to `Lunate.Decisions.Model`, the engine stays
  unpacked in `Lunate.Ai.Decisions`, Abstractions depends on the package instead of packing the
  engine, and the host owns the model at load.
