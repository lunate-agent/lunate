## MODIFIED Requirements

### Requirement: Extension-facing service

The primitive SHALL be available to extensions as a service: `IDecisionService` SHALL live in
`Lunate.Extensibility.Abstractions`, the evaluator SHALL live in `Lunate.Agent`, and
`Lunate.Extensibility` SHALL bridge and register the service so an extension can resolve it. The
decision model — the request, the question kinds, the answer and the result — SHALL live in
`Lunate.Decisions.Model`, its own package and the one home of the model, and SHALL be the type the
service, the transport, the parser and every consumer use. The engine — the chat adapter, the
builder, the OpenTelemetry wrapper, the parser and the validator — SHALL live in
`Lunate.Ai.Decisions`, which is not a package of its own. The dependency SHALL run from
`Lunate.Decisions.Model` to `Lunate.Contracts` and never back, and from `Lunate.Ai.Decisions` to both
and never back: neither the contract assembly nor the model package SHALL contain a decision engine
type, and the event channel SHALL keep publishing the one distribution record
(`Lunate.Contracts.DecisionProbability`) that `DecisionEvaluated` exposes.
`Lunate.Extensibility.Abstractions` SHALL reference `Lunate.Decisions.Model` and
`Lunate.Contracts`, and SHALL NOT reference `Lunate.Agent` or `Lunate.Ai.Decisions`. Front ends SHALL
keep observing decisions through the agent event channel (`DecisionEvaluated`, `DecisionUnavailable`,
`DecisionFailed`).

#### Scenario: An extension requests a decision

- **GIVEN** an extension that resolved `IDecisionService` while a `decision` profile is configured
- **WHEN** it proposes a request with a state and one question
- **THEN** it receives the validated per-question result, and the decision appears in the run's event
  stream like any other

#### Scenario: The extension service preserves the assembly boundary

- **GIVEN** the decision service in `Lunate.Extensibility.Abstractions` and the model in
  `Lunate.Decisions.Model`
- **WHEN** the layering test runs
- **THEN** Abstractions references `Lunate.Decisions.Model` and `Lunate.Contracts` but not Agent or
  `Lunate.Ai.Decisions`, the model package references `Lunate.Contracts` and nothing above it,
  `Lunate.Ai.Decisions` references the model and `Lunate.Contracts` and nothing above it, and no
  decision type names a provider, a tool or the loop

#### Scenario: One request type reaches the transport

- **GIVEN** a caller's request, a chat adapter and a native transport
- **WHEN** the engine evaluates the request
- **THEN** the same request type is handed to the transport, parsed back into the same result type and
  returned to the caller, with no second request or answer type in between
