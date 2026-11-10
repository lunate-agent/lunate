# ADR Review Manifest

- Status: completed
- Review date: 2026-11-08

## Review Summary

ADR review completed for this change.

## In-Force ADRs Reviewed

- ADR-0007 (live-area concurrency, reaffirmed by ADR-0019): the deleted `CtrlCQuitWindow` was an
  internal `IScheduler` consumer; `QuitWindow` keeps the same injected-scheduler contract, and ADR-0007
  is untouched.
- ADR-0019 (terminal UI): recommendation (c) binds the TUI to ADR-0007; deleting an unused internal
  type re-affirms, and does not change, that boundary.
- ADR-0029 (provider API families): a family selects the adapter, which is exactly why the old
  `ProviderAdapter` comment was stale and why the corrected comment points at `ProviderApiFamily`; no
  decision in the ADR changes.
- ADR-0030 (native decisions): the decision client's non-generic `GetService(Type)` service lookup is
  what the native transport contract uses; only the unused generic wrapper goes.
- ADR-0003 (loop) and ADR-0013 (agent loop): unaffected — no loop, tool or event path is touched.

## New Durable ADRs Created

- None — no major durable architectural decision was introduced. This change deletes unreachable
  declarations, keeps the one refusal path the factory already had, and corrects a comment to match
  ADR-0029 as accepted.
