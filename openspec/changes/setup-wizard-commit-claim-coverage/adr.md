# ADR Review Manifest

- Status: completed
- Review date: 2026-11-11

## Review Summary

ADR review completed for this change. The change records and tests the
cross-process claim inside the setup wizard's commit; it introduces no new
architectural commitment, contract or dependency.

## In-Force ADRs Reviewed

- `adr/0027-provider-store-model-profiles-and-usages.md` — the provider-store
  format, model profiles and usages are unchanged; the commit only reads and
  replaces the store under the existing claim.
- `adr/0029-provider-api-families.md` — provider API families and the entry shape
  are unchanged.
- `adr/0035-tool-environment-pass-through.md` — no environment or child-process
  contract changes; the tests only spawn the existing test child process.

## New Durable ADRs Created

- None - no major durable architectural decisions were introduced.
