# ADR Review Manifest

- Status: completed
- Review date: 2026-11-22

## Review Summary

ADR review completed for this change. The tool, loop, live-area and session-format boundaries are the accepted ones; the new event kind is the case addition ADR-0043 already governs, and the list reuses the existing tool-call/result messages rather than adding session state. No new durable architectural decision is introduced.

## In-Force ADRs Reviewed

- ADR-0003 — the loop owns tool execution
- ADR-0007 — live area concurrency and the injected scheduler
- ADR-0012 — hand-authored tool contract and declaration-only adapter
- ADR-0015 — session format pinned by byte-for-byte goldens
- ADR-0019 — existing TUI/input stack retained
- ADR-0043 — the contract's closed sets are C# 15 unions; adding a case is a contract change

## New Durable ADRs Created

- None — no major durable architectural decisions were introduced, and no repository-level ADR files were created.
