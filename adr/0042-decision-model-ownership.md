# ADR-0042: The decision model belongs to the decision layer

Status: accepted — 2026-11-16 (maintainer sign-off on #695, recorded in the card's Decisions)
Completes: ADR-0028 (decision layer) and ADR-0030 (native decisions); amends ADR-0002's decision note

## Context

ADR-0028 put the decision contract — request, question, kinds, answer, result — in
`Lunate.Contracts`, because a contract both the extension surface and the agent implementation need
belonged *below* both. The decision layer then grew a second set of the same records inside
`Lunate.Ai.Decisions`: the loop's own request (with the resolved model id), the loop's own answer (with
confidence, box geometry and expected score) and its own result (carrying usage). Every field added
since — the box kind, the usage sidecar, the confidence — landed twice, and `DecisionEngine` mapped
between the two on every call, including a version-locked "1.15 constructor" test that pinned a shape
no published consumer had ever compiled against.

The duplication was never a boundary requirement. It came from where the first set happened to be
written, and the mapping cost grew with each kind.

## Decision

1. **One model, one home: `Lunate.Ai.Decisions`.** `DecisionRequest`, `DecisionQuestion`,
   `DecisionQuestionKind`, `DecisionAnswer`, `DecisionBox`, `DecisionResult`, `DecisionUsage` and the
   two seams `INativeDecisionClient` and `IAgentDecisionService` live in the decision assembly and are
   public. The chat adapter, the System One transport, the parser and validator, the engine, the
   `decide` tool, the ask-user suggestions and the wizard check all use that one set.
2. **`Lunate.Contracts` keeps only what an event publishes.** `DecisionProbability` stays, because
   `AgentEvent.DecisionEvaluated.Distribution` exposes it to every event consumer; the decision layer
   reads it from the contract instead of defining a second copy.
3. **The edge runs down.** `Lunate.Ai.Decisions` references `Lunate.Contracts`. `Lunate.Contracts`
   never references a decision project, and no contract type names a provider, a tool or the loop.
4. **The extension-facing service keeps its home.** `IDecisionService` stays in
   `Lunate.Extensibility.Abstractions`, which now references `Lunate.Ai.Decisions` for the model its
   signature names. The assembly is not published on its own, so the contract package carries it in
   `lib/` beside the contract; the host owns it at load, and the embedded compile contract offers it
   to an extension compiled elsewhere.
5. **No compatibility shim.** The old request/result records were in `PublicAPI.Unshipped.txt` — never
   shipped — so they are deleted rather than forwarded, aliased or kept beside the new set.

## Consequences

- The `1.15` answer-constructor and deconstruction compatibility test is deleted with the second set:
  nothing published ever compiled against those records.
- `Lunate.Extensibility.Abstractions` ships `Lunate.Ai.Decisions.dll` in `lib/`. Its API version does
  not move, because the decision types it exposes were unshipped; the two assemblies it publishes
  together are otherwise unchanged.
- The extension host-assembly rule, the host-owned assembly list and the embedded compile contract
  each gain one name, so an extension binds to the host's one copy of the decision assembly.
- A future change that publishes the decision assembly as its own package moves the contract's
  dependency from a packed file to a package reference; nothing else in this decision changes.
- `Lunate.Agent` still packs the decision assembly in its own `lib/`, unchanged.

## Alternatives rejected

- **Keep both DTO sets and map between them.** Two records with the same meaning is the debt the card
  exists to kill, and every new field would need a mapping on both sides.
- **Move `IDecisionService` into `Lunate.Ai.Decisions`.** It would take the extension-facing service
  out of the extension contract — the one assembly an extension compiles against — and the
  decision-layer spec places it in Abstractions.
- **Put the model back in `Lunate.Contracts` as the single copy.** It would make the shipped contract
  carry the decision loop's internal machinery (usage records, transport options, the native seam) and
  keep the contract growing with every decision field.
- **Publish `Lunate.Ai.Decisions` as a package in this change.** Correct eventually; the release
  pipeline names each published package and no consumer needs the assembly alone today.
