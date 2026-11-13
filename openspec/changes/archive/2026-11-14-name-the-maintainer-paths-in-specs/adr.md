# ADR Review Manifest

- Status: completed
- Review date: 2026-11-13

## Review Summary

ADR review completed for this change. The change is a path repair in two specifications: `repo-quality`'s map, architecture-page and ADR-numbering requirements and `distribution`'s channel-artifacts requirement now name the pages under `docs/dev/`. No architectural pattern, boundary, dependency or contract changes, so no durable decision is introduced.

## In-Force ADRs Reviewed

- ADR-0011 (repo-quality tooling) — sets the documentation map, the project entry point and the `AGENTS.md` pointer as the repository's documentation convention. This change keeps that convention and follows the map to its new home; the ADR is a record and is not edited, so its own `docs/README.md` mention stays as written when it was accepted.
- ADR-0006 (Roslyn placement), ADR-0024 (bash guard), ADR-0026 (Roslyn built-in boundary) — their `docs/guide.md` / architecture-docs references are historical context in immutable records and are left untouched by the records rule.
- ADR-0035 (tool environment pass-through) — highest in-force primary ADR number reviewed; this change adds no ADR.

## New Durable ADRs Created

None. No major durable architectural decisions were introduced, so no repository-level ADR file was created.

## Related

- [Design](design.md)
- [Proposal](proposal.md)
