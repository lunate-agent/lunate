# ADR Review Manifest

- Status: completed
- Review date: 2026-11-15

## Review Summary

ADR review completed for this change.

## In-Force ADRs Reviewed

- ADR-0002 — layering and MEAI model types: the new library references no Lunate project and does
  not introduce a parallel model type.
- ADR-0019 — Xenoatom terminal UI: the TUI keeps owning terminal painting; the renderer moves out
  but the TUI's reference and presentation stay in `Lunate.Tui`.
- ADR-0037 — CI gate sized by event: the pack dry run is unchanged in shape; `Mermaid.Terminal`
  joins its package list without adding a required context.

No superseded ADR constrains this change.

## New Durable ADRs Created

- `adr/0039-mermaid-terminal-library.md` — the renderer becomes a Lunate-free project and package,
  with parse-and-render as its public boundary, a port of mermaid-ascii for layout and drawing, and a
  place in the release pack.
