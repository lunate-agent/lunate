# ADR Review Manifest

- Status: completed
- Review date: 2026-11-15

## Review Summary

The target frameworks are a durable architectural commitment (runtime, published packages, tool
distribution), and this change applies the maintainer's superseding decision on #192: everything
targets `net11.0` and nothing multi-targets. That supersedes the multi-target clause of the earlier
decision, so it is recorded as a new ADR rather than an addendum, and it carries the clause that
brings the multi-target question back before the first external release. No budget ADR is amended:
the startup and memory re-measurement is the maintainer's post-merge step under ADR-0005 and
ADR-0009, not this change's evidence.

## In-Force ADRs Reviewed

- ADR-0001 — No Native AOT; self-contained single-file releases held to budgets
- ADR-0002 — Three-layer project graph and Microsoft.Extensions.AI model types
- ADR-0005 and its addendum — Startup budgets and the floor metric
- ADR-0008 — Release builds: single file + ReadyToRun, no single-file compression
- ADR-0009 — Memory budgets: idle, working set, Roslyn
- ADR-0017 and its addenda — Extensibility architecture, formats, restore, trust and packages
- ADR-0023 — A first-party extension tier beside `samples/`
- ADR-0026 — Roslyn built-in composition boundary
- ADR-0037 — The CI gate is sized by the event
- ADR-0039 — The Mermaid renderer is its own Lunate-free library
- ADR-0040 — Hook-based extensibility on observables

## New Durable ADRs Created

- [`adr/0041-net11-target-frameworks.md`](../../../adr/0041-net11-target-frameworks.md) — everything
  targets `net11.0` with no multi-targeting, the tool's .NET 11 requirement, and the clause that
  returns the multi-target question before the first external release.

## ADR Addenda

_None._ The startup and memory budgets are re-measured on .NET 11 by the maintainer after the merge
(ADR-0005, ADR-0009); this change does not move a budget.
