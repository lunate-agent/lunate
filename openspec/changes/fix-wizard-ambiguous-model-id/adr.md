# ADR Review Manifest

- Status: completed
- Review date: 2026-11-08

## Review Summary

ADR review completed for this change.

The change repairs how an existing lookup is called (which provider scope a model id is read in) and adds an internal catalogue lookup plus a provider-scoped first step in profile resolution. It introduces no new boundary, dependency, contract or data model, so it does not meet the bar for a durable architectural decision.

## In-Force ADRs Reviewed

- ADR-0022 (model catalog and provider auth) — the catalogue stays the single source for model entries and wire ids; the new lookup reads the same entries.
- ADR-0027 (provider store, model profiles and usages) — a profile still names one provider entry and one model; resolution now honours that provider first instead of only at validation time.
- ADR-0029 (provider API families) — family resolution and the wire id are unchanged; the change only affects which catalogue entry supplies them.

## New Durable ADRs Created

- None - no major durable architectural decisions were introduced.
