# ADR Review Manifest

- Status: completed
- Review date: 2026-11-15

## Review Summary

ADR review completed for this change. The change refines ADR-0036's credential-shape rule with one
fixed name-family exemption, so a new repository-level ADR records it as an amendment; ADR-0036 stays
frozen as the historical record.

## In-Force ADRs Reviewed

- `adr/0035-tool-environment-pass-through.md` — the exact-name pass-through this change leaves
  untouched.
- `adr/0036-tool-child-credential-shape-strip.md` — the shape rule, its word list and the false
  positive this change repairs.

## New Durable ADRs Created

- `adr/0038-tool-child-git-config-family.md` — git's environment configuration reaches the tool child
  whole; amends ADR-0036.
