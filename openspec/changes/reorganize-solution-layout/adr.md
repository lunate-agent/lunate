# ADR Review Manifest

- Status: completed
- Review date: 2026-11-16 (updated for the decision-model merge)

## Review Summary

ADR review completed for this change. The change moves C# files into feature folders, folds two
folder-derived namespaces back into their assembly's namespace, and deletes a duplicated request
validator in the decision layer. No pattern, technology, boundary or contract is introduced. The
layout rule itself is a repository-quality convention, recorded in
`openspec/specs/repo-quality/spec.md` and enforced by a source scan — the same shape the earlier
source-file-names rule took, which also produced no ADR.

The one architectural question the card raises — merging the two decision DTO sets — is a project
edge, and the maintainer signed it off after the first round: the one decision model lives in
`Lunate.Ai.Decisions`, the edge runs down to `Lunate.Contracts`, and the contract keeps only the
distribution record its event publishes. That is a boundary decision with a durable rationale, so it
is recorded as ADR-0042 rather than in the design.

## In-Force ADRs Reviewed

- `adr/0002-layering-and-meai-model-types.md` — amended: its decision note keeps `IDecisionService`
  in Abstractions and typed with the decision model, which still holds; the model it is typed with
  now lives one layer down rather than in `Lunate.Contracts` (ADR-0042), and Abstractions references
  the decision project instead of only the contract. The rule it states — never upward, the contract
  stays below the implementation — is unchanged and checked by `LayeringTests`.
- `adr/0011-repo-quality-tooling.md` — extended in spirit: this change adds a repository-quality
  convention and its check, the same way the source-file-names change did.
- `adr/0020-acp-sdk-choice.md` — affected in surface only: `IAcpServer` and its context fold from
  `Lunate.Protocols.Acp` into `Lunate.Protocols`. The choice of LibAcp, the seam and the ACP
  behaviour are unchanged, and the types are unshipped, so the ADR stays as written.
- `adr/0028-decision-layer-primitive.md` and `adr/0030-native-decisions.md` — behaviour unchanged:
  the validator that is deleted re-checked what the boundary already validated, and the
  `Noul` → `YesNo` rename is inside the model. Their placement of the decision types is amended by
  ADR-0042: the model is the decision layer's, not the contract's. ADR-0030's anticipated "the
  abstraction becomes an adapter onto the native layer" is still a follow-up, not this change.
- `adr/0017-extensibility-architecture.md` — affected in one place and consistent with it: the
  extension contract package now carries the decision assembly in `lib/` so an extension compiling
  against `IDecisionService` binds to one copy of the model; the host-owned assembly list enforces
  the one copy at load.
- The remaining in-force ADRs (`adr/0001`–`adr/0041`, addenda included) were reviewed for a
  source-layout, namespace or decision-model commitment. None states one; `adr/0041` pins the framework only.

## New Durable ADRs Created

- `adr/0042-decision-model-ownership.md` — one decision model, its home (`Lunate.Ai.Decisions`), the
  dependency direction to the contract, and why `IDecisionService` stays in the extension contract.
