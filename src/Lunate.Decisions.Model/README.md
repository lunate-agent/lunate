# Lunate.Decisions.Model

The one decision model of Lunate: `DecisionRequest`, `DecisionQuestion` and its kinds,
`DecisionAnswer`, `DecisionBox`, `DecisionResult` and `DecisionUsage`, plus the two seams
`INativeDecisionClient` and `IAgentDecisionService`. It is the type `IDecisionService` in
[`Lunate.Extensibility.Abstractions`](https://www.nuget.org/packages/Lunate.Extensibility.Abstractions)
is built from, and the package every decision consumer on the extension surface shares.

The types keep the `Lunate.Ai.Decisions` namespace: the namespace is the compiled identity the
contract's `PublicAPI` records, and a move never renames a type or a namespace (ADR-0045).

## Installation

```sh
dotnet add package Lunate.Decisions.Model --version 1.16.0
```

It depends on [`Lunate.Contracts`](https://www.nuget.org/packages/Lunate.Contracts) (same version) for
the `DecisionProbability` record the event channel publishes, and on
`Microsoft.Extensions.AI.Abstractions` for the usage types. `Lunate.Extensibility.Abstractions` brings
it in as a package dependency, so an extension that references the contract resolves the model without
a second reference.

## The model, not the engine

The engine — the chat adapter, the client builder, the OpenTelemetry wrapper, the response parser and
the validator — stays in `Lunate.Ai.Decisions`, which is not a package of its own. Only
`Lunate.Agent`, the harness that needs it, ships that assembly in its `lib/`. The model package holds
no engine type, no provider type and no loop type, so the decision surface an extension compiles
against cannot grow because the engine changed.

## On stability

This package is part of the **stable extension contract** and versioned in lockstep with
`Lunate.Contracts` and `Lunate.Extensibility.Abstractions` under `ExtensionApi.Current`. Its public
surface is tracked by `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt`: within a major version the
surface only grows, and removal or reshaping happens at the next major `ExtensionApi` bump together
with the other two assemblies.

## Documentation

- [ADR-0045](https://github.com/lunate-agent/lunate/blob/main/adr/0045-decision-model-package.md) — why the model has its own package.
- [ADR-0044](https://github.com/lunate-agent/lunate/blob/main/adr/0044-decision-model-ownership.md) — the one-model decision this amends.
- [The decision layer spec](https://github.com/lunate-agent/lunate/blob/main/openspec/specs/decision-layer/spec.md) — the behaviour the model carries.
