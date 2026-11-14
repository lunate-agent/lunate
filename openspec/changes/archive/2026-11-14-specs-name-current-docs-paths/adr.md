# ADR Review Manifest

- Status: completed
- Review date: 2026-11-14

## Review Summary

ADR review completed for this change. The change corrects two documentation path references in
current requirements: the `ci-pipeline` prose example names `docs/dev/guide.md`, and the `tui`
key-table requirement names `docs/keybindings.md` and calls it the keybindings reference. No
architectural pattern, boundary, dependency, storage format, API or security contract changes, so
no durable decision is introduced.

## In-Force ADRs Reviewed

- ADR-0011 (repository quality tooling) — the in-force ADR that establishes the documentation map
  and writing guideline. This change follows that convention to the paths the map now records; the
  ADR is a record and keeps its original `docs/README.md` wording.
- ADR-0006, ADR-0024 and ADR-0026 — reviewed for their historical `docs/guide.md` and architecture
  references; the records rule leaves those immutable records untouched.
- The remainder of the in-force set was reviewed for documentation or CI obligations; none constrains
  where these two requirements point.

## New Durable ADRs Created

None. No major durable architectural decision was introduced, so no repository-level ADR file was
created. The same branch does rename the CI-gate record from `0036-ci-gate-sized-by-event.md` to
`0037-ci-gate-sized-by-event.md` to free the duplicate `0036`; that is a numbering repair of an
existing record, not a new decision, so `0037` is now the highest primary ADR number.

## Related

- [Design](design.md)
- [Proposal](proposal.md)
