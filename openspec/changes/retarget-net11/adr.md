# ADR Review Manifest

- Status: completed
- Review date: 2026-11-15

## Review Summary

ADR review completed for this change. The target-framework split is a durable architectural
commitment (runtime, published contract floor, tool distribution), so it is recorded as a new ADR
based on the maintainer's decision on #192. No existing ADR is superseded. ADR-0009 is amended by an
addendum that records the memory gate's re-calibration from the .NET 11 measurement under the rule
ADR-0009 already owns.

## In-Force ADRs Reviewed

- ADR-0001 — No Native AOT; self-contained single-file releases held to budgets
- ADR-0002 — Three-layer project graph and Microsoft.Extensions.AI model types
- ADR-0005 and its addendum — Startup budgets and the floor metric
- ADR-0008 — Release builds: single file + ReadyToRun, no single-file compression
- ADR-0009 — Memory budgets: idle, working set, Roslyn (amended by the addendum below)
- ADR-0017 and its addenda — Extensibility architecture, formats, restore, trust and packages
- ADR-0023 — A first-party extension tier beside `samples/`
- ADR-0026 — Roslyn built-in composition boundary
- ADR-0037 — The CI gate is sized by the event
- ADR-0039 — The Mermaid renderer is its own Lunate-free library

## New Durable ADRs Created

- [`adr/0040-net11-target-frameworks.md`](../../../adr/0040-net11-target-frameworks.md) — the
  `net11.0` app/tool, the `net10.0;net11.0` extension contract floor, and the follow-up rules for
  dropping the floor.

## ADR Addenda

- [`adr/0009-addendum-net11-idle-gate.md`](../../../adr/0009-addendum-net11-idle-gate.md) — the idle
  memory gate re-calibrated to 550 MB from the .NET 11 published single-file ReadyToRun subject.
