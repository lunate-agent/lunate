# ADR Review Manifest

- Status: completed
- Review date: 2026-11-22

## Review Summary

ADR review completed for this change. The change adds tools and an internal job registry inside
`Lunate.Coding`; it does not alter the loop, the tool contract, a protocol, a dependency boundary,
the session format or the public API. No durable architectural decision outside the existing ADRs is
introduced, so no repository-level ADR was created.

## In-Force ADRs Reviewed

- ADR-0002 — layering and Microsoft.Extensions.AI model types
- ADR-0003 — the loop owns tool execution
- ADR-0012 — the tool contract and its risk levels
- ADR-0017 — extensibility architecture (core tool name reservation)
- ADR-0024 — the bash guard
- ADR-0034 — POSIX child descriptor isolation
- ADR-0035, ADR-0036, ADR-0038 — tool-child environment pass-through and credential stripping
- ADR-0041 — .NET 11 only
- ADR-0043 — the contract's closed sets
- ADR-0044, ADR-0045 — the decision model's ownership and package boundary

## New Durable ADRs Created

- None — no major durable architectural decisions were introduced.
