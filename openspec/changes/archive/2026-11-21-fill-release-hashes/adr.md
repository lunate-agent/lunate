# ADR Review Manifest

- Status: completed
- Review date: 2026-11-21

## Review Summary

ADR review completed for this change. The change adds a release-process script with no new
dependency, no public API and no runtime behaviour, so it does not meet the bar for a durable
architectural decision.

## In-Force ADRs Reviewed

- ADR-0011 — the documentation map and generated docs regions. The runbook keeps owning the prose and
  `scripts/docs-sync` keeps owning the README release line.
- ADR-0037 — the CI gate is sized by event. The new test joins the platform checks that already run
  the channel-manifest guard.
- ADR-0041 — everything targets `net11.0`. No C# is touched.

## New Durable ADRs Created

- None - no major durable architectural decisions were introduced.
