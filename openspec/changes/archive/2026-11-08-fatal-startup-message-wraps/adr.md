# ADR Review Manifest

- Status: completed
- Review date: 2026-11-08

## Review Summary

ADR review completed for this change. The change is a rendering and spec-regression fix on
existing seams; it establishes no new long-term pattern, boundary, technology or contract.
No repository-level ADR was created.

## In-Force ADRs Reviewed

- ADR-0007 — live-area concurrency: the fix commits through `LiveArea.WriteScrollback` under
  its paint gate and does not add a second writer or thread.
- ADR-0016 — workspace boundary: the state-root guard's containment rule is unchanged; the
  default (unset) home behavior is pinned by spec and test, not redefined.
- ADR-0019 — XenoAtom.Terminal.UI not adopted: the fix stays on the existing self-built
  live area and `IConsoleIO` foundation.

## New Durable ADRs Created

- None — no major durable architectural decisions were introduced.
