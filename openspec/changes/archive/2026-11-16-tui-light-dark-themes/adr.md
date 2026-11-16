# ADR Review Manifest

- Status: completed
- Review date: 2026-11-16

## Review Summary

ADR review completed for this change.

## In-Force ADRs Reviewed

- ADR-0019 (XenoAtom.Terminal.UI inline mode: not adopted) — the terminal foundation and the
  TESTABLE-frame rule the theme goldens follow.
- ADR-0039 (Mermaid.Terminal library) — adjacent TUI rendering boundary; unaffected.

No other in-force ADR constrains a built-in palette table: the theme contract is the `tui` spec's,
and this change adds two names to it without touching a loop, a tool contract, a protocol or a
dependency.

## New Durable ADRs Created

- None - no major durable architectural decisions were introduced.
