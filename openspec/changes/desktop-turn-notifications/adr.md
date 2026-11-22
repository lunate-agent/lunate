# ADR Review Manifest

- Status: completed
- Review date: 2026-11-22

## Review Summary

ADR review completed for this change. The design extends the existing terminal and session
boundaries; it introduces no new long-lived architectural commitment beyond the documented focus
request and the best-effort platform sender, both of which are tactical implementations of the
`notifications` requirement. No repository-level ADR was created.

## In-Force ADRs Reviewed

- ADR-0001 through ADR-0045 were reviewed as the current decision set. The relevant in-force
  commitments are ADR-0002 (layering and Microsoft.Extensions.AI model types), ADR-0004 (the
  product's own VT input decoder), ADR-0007 (live-area concurrency on an injected scheduler),
  ADR-0015/0018 (session format, unchanged) and ADR-0041 (the net11.0 target).
- Superseded clauses were treated as historical context only: ADR-0027's single-base-URL half
  (ADR-0029), ADR-0017's skills-index cap and description limit (ADR-0031/0032), ADR-0031's unknown
  window fallback (ADR-0033), ADR-0042 (ADR-0043) and ADR-0044 (ADR-0045).

## New Durable ADRs Created

- None - no major durable architectural decisions were introduced.
