## Why

ADR-0044 put the one decision model in `Lunate.Ai.Decisions`, beside the engine — the chat adapter, the
builder, the OpenTelemetry wrapper, the parser and the validator. `Lunate.Extensibility.Abstractions`
references that assembly only because `IDecisionService` is typed with the model, so the engine became
extension contract by accident. Three consequences follow: the engine assembly has no `PublicAPI`
files, two packages (`Lunate.Agent` and `Lunate.Extensibility.Abstractions`) ship the same
`Lunate.Ai.Decisions.dll` in `lib/`, and a test project that references both sees the engine twice.

## What Changes

- A new packable project `src/Lunate.Decisions.Model` holds only the decision model — `DecisionRequest`,
  `DecisionQuestion`, `DecisionQuestionKind`, `DecisionAnswer`, `DecisionBox`, `DecisionResult`,
  `DecisionUsage` — and the two seams `INativeDecisionClient` and `IAgentDecisionService`, with
  `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt`. The types keep the `Lunate.Ai.Decisions`
  namespace, because a move never renames a type or namespace.
- `Lunate.Ai.Decisions` keeps the engine, references the model project, and stays unpacked.
  `DecisionOptions` and the engine bookkeeping types move to the engine; the engine types remain
  `internal` where only `Lunate.Agent`, `Lunate.Ai.SystemOne` and the composition use them.
- `Lunate.Extensibility.Abstractions` references `Lunate.Decisions.Model` (a package dependency) and no
  longer references or packs `Lunate.Ai.Decisions.dll`. `Lunate.Agent` references the model as a
  package dependency and keeps the engine in its own `lib/`, which is the one package that needs it.
- `Lunate.Extensibility`, `Lunate.Extensibility.Compile` and the host-owned assembly list name the model
  assembly, so an extension compiles against and binds to `Lunate.Decisions.Model.dll` rather than the
  engine.
- `scripts/pack-dry-run.sh`, `.github/workflows/release.yml` and the verify gate's contract-package
  roundtrip pack and check `Lunate.Decisions.Model`, versioned with the contract pair under
  `ExtensionApi`.
- A new ADR amends ADR-0044: the model's home is its own package.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `decision-layer`: the one decision model lives in `Lunate.Decisions.Model`, its own package; the engine
  stays in `Lunate.Ai.Decisions`; the dependency edges and the host-owned assembly are named for the
  model package.
- `extensions`: extensions compile against and load the model package beside the two contract
  assemblies, and the host-owned list names `Lunate.Decisions.Model` instead of the engine.
- `ci-pipeline`: the release and pack dry run pack the model package with the stable contract pair.

## Impact

- Projects: new `src/Lunate.Decisions.Model`; `src/Lunate.Ai.Decisions`, `src/Lunate.Agent`,
  `src/Lunate.Extensibility.Abstractions`, `src/Lunate.Extensibility`, `src/Lunate.Extensibility.Compile`
  and their `PublicAPI` files.
- Packaging: `scripts/pack-dry-run.sh`, `.github/workflows/release.yml`, `scripts/verify.sh`,
  `scripts/release-versions.sh` comments, package READMEs.
- Tests: decision assembly/identity tests, the contract-package inventory test, the layering edge set,
  the compile-contract list and the host-owned assembly list.
- Docs and records: `docs/dev/architecture.md`, `AGENTS.md`, `docs/extensions/README.md`,
  `adr/0045-decision-model-package.md`.
- No new NuGet packages. No behaviour change in the decision layer itself; the wire, the results and
  `IDecisionService` are unchanged.
