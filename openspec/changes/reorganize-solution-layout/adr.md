# ADR Review Manifest

- Status: completed
- Review date: 2026-11-16

## Review Summary

ADR review completed for this change. The change moves C# files into feature folders, folds two
folder-derived namespaces back into their assembly's namespace, and deletes a duplicated request
validator in the decision layer. No pattern, technology, boundary or contract is introduced. The
layout rule itself is a repository-quality convention, recorded in
`openspec/specs/repo-quality/spec.md` and enforced by a source scan — the same shape the earlier
source-file-names rule took, which also produced no ADR.

The one architectural question the card raises — merging the two decision DTO sets — needs a new
project edge and is therefore not taken here; it is recorded as the follow-up in `tasks.md` and in
the design's Open Questions, not as a new ADR.

## In-Force ADRs Reviewed

- `adr/0002-layering-and-meai-model-types.md` — unaffected: no project reference and no model type
  changes; the layout rule keeps namespaces where the layering ADR put them.
- `adr/0011-repo-quality-tooling.md` — extended in spirit: this change adds a repository-quality
  convention and its check, the same way the source-file-names change did.
- `adr/0017-extensibility-architecture.md` — unaffected: `Lunate.Extensibility.Abstractions` keeps
  every namespace, type and member an extension compiles against; only file folders move.
- `adr/0020-acp-sdk-choice.md` — affected in surface only: `IAcpServer` and its context fold from
  `Lunate.Protocols.Acp` into `Lunate.Protocols`. The choice of LibAcp, the seam and the ACP
  behaviour are unchanged, and the types are unshipped, so the ADR stays as written.
- `adr/0028-decision-layer-primitive.md` and `adr/0030-native-decisions.md` — unaffected in
  behaviour: the validator that is deleted re-checked what the boundary already validated, and the
  `Noul` → `YesNo` rename is inside the internal model. ADR-0030's anticipated "the abstraction
  becomes an adapter onto the native layer" is the follow-up named in `tasks.md`, not this change.
- The remaining in-force ADRs (`adr/0001`–`adr/0041`, addenda included) were reviewed for a
  source-layout or namespace commitment. None states one; `adr/0041` pins the framework only.

## New Durable ADRs Created

- None - no major durable architectural decisions were introduced.
