# ADR-0045: The decision model's home is its own package

Status: accepted — 2026-11-21 (amends ADR-0044, per issue #972)
Amends: ADR-0044 (the decision model belongs to the decision layer)
Completes: ADR-0044's stated follow-up — publish the model assembly as its own package

## Context

ADR-0044 put one decision model in `Lunate.Ai.Decisions` and made the engine share that assembly.
`Lunate.Extensibility.Abstractions` needed the model for `IDecisionService`, so it referenced and
packed the whole assembly; the contract package therefore carried the chat adapter, the builder, the
OpenTelemetry wrapper, the parser and the validator into every extension consumer. Three problems
followed: the assembly had no `PublicAPI` file, two packages shipped the same
`Lunate.Ai.Decisions.dll` in `lib/`, and a test project referencing both saw the engine twice over.

ADR-0044 recorded the move as a future change: *"A future change that publishes the decision
assembly as its own package moves the contract's dependency from a packed file to a package
reference; nothing else in this decision changes."* Issue #972 is that change.

## Decision

1. **The model has its own package.** `Lunate.Decisions.Model` holds only `DecisionRequest`,
   `DecisionQuestion`, `DecisionQuestionKind`, `DecisionAnswer`, `DecisionBox`, `DecisionResult`,
   `DecisionUsage` and the seams `INativeDecisionClient` and `IAgentDecisionService`. It carries
   `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`, is versioned with the contract pair under
   `ExtensionApi`, and is packed by the release, the pack dry run and the verify roundtrip.
2. **The types keep the `Lunate.Ai.Decisions` namespace.** A move never renames a type or a
   namespace; the package id is the new shipping identity. `IDecisionService`'s `PublicAPI` signature
   and every consumer `using` therefore stay unchanged.
3. **The engine stays in `Lunate.Ai.Decisions`, unpacked.** `DecisionOptions` and the engine's usage
   bookkeeping move there; the engine types remain `internal` outside the friend assemblies that
   already use them. Only `Lunate.Agent` packs `Lunate.Ai.Decisions.dll`, because only it needs the
   engine.
4. **The edge runs model → contract, engine → model + contract.** `Lunate.Contracts` never references
   a decision project and keeps only `DecisionProbability`, which `DecisionEvaluated` publishes.
   `Lunate.Extensibility.Abstractions` references `Lunate.Decisions.Model` as a package dependency
   and no longer references or packs the engine.
5. **The host owns the model at load.** `Lunate.Extensibility.Compile` embeds
   `Lunate.Decisions.Model` for extension compilation, and `HostOwnedAssemblies` names it instead of
   the engine, so a copy an extension ships is ignored exactly like a copy of the contracts.

## Consequences

- `Lunate.Extensibility.Abstractions` publishes a nuspec dependency on `Lunate.Decisions.Model`
  instead of a duplicate `lib/` file; `Lunate.Agent` keeps the engine in its own `lib/` and depends
  on the model package.
- A test project referencing both `Lunate.Agent` and `Lunate.Extensibility.Abstractions` sees one
  model assembly and one engine assembly, not two of the engine.
- `Lunate.Decisions.Model` gains the `PublicAPI` gate; its surface can no longer grow by accident.
- The engine's surface is no longer an extension contract; a future engine change does not move
  `ExtensionApi` unless it also changes the model package.
- `ExtensionApi.Current` does not move: the decision types were unshipped, and the contract package's
  public signatures are the same types under the same names.
- No compatibility shim is added for an extension compiled against `Lunate.Ai.Decisions.dll`: no
  published package ever shipped that assembly through the contract surface, and the move is the one
  ADR-0044 named in advance.

## Alternatives rejected

- **Keep the model in the engine and mark the engine's types internal.** The engine still has to be
  referenced and packed by the contract package for `IDecisionService`, so the engine remains
  extension contract and the duplicate `lib/` file remains.
- **Move `IDecisionService` into the model package.** The extension-facing service belongs to the
  extension contract assembly (ADR-0028's addendum, ADR-0044); the model package is a dependency of
  that contract, not its replacement.
- **Rename the namespace to `Lunate.Decisions.Model`.** It would rewrite a compiled identity for a
  packaging change, against the repository's move rule and with no consumer benefit.
- **Publish the engine as a package too.** Nothing outside `Lunate.Agent` needs the engine; publishing
  it would add a second package to maintain and a wider surface with no consumer.
