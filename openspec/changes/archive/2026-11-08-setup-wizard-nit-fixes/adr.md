# ADR Review Manifest

- Status: completed
- Review date: 2026-11-09

## Review Summary

ADR review completed for this change. The four fixes stay inside boundaries existing ADRs already
commit to: they change the text a staged answer renders, the guard on one wizard step, the detail a
check failure records, and a usage string. No layering, loop, protocol or dependency decision is
touched, and no new package is required.

## In-Force ADRs Reviewed

- ADR-0012 addendum (exact-value credential redaction at the persistence boundary) and ADR-0022
  (API keys in `AuthStore`) — the credential answer's `ToString()` now renders a typed key as redacted,
  which is the same exact-value rule the diagnostics and export scrubbers already apply; nothing new
  is stored or logged.
- ADR-0029 (provider API families) — the model check's failure detail names the resolved family the
  check actually sent, so the family in the failure text is the family the profile will use.

## New Durable ADRs Created

- None - no major durable architectural decision was introduced: the fixes correct text and one step
  guard inside the setup wizard's existing structure (design D1-D4).
