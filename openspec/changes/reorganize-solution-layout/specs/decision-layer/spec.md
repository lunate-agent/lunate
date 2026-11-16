## MODIFIED Requirements

### Requirement: Extension-facing service

The primitive SHALL be available to extensions as a service: `IDecisionService` SHALL live in
`Lunate.Extensibility.Abstractions`, the evaluator SHALL live in `Lunate.Agent`, and
`Lunate.Extensibility` SHALL bridge and register the service so an extension can resolve it. The
decision model — the request, the question kinds, the answer and the result — SHALL live in
`Lunate.Ai.Decisions`, one model with one home, and SHALL be the type the service, the transport,
the parser and every consumer use. The dependency SHALL run from `Lunate.Ai.Decisions` to
`Lunate.Contracts` and never back: the contract assembly SHALL NOT contain a decision request or
result type, and the event channel SHALL keep publishing the one distribution record
(`Lunate.Contracts.DecisionProbability`) that `DecisionEvaluated` exposes.
`Lunate.Extensibility.Abstractions` SHALL reference `Lunate.Ai.Decisions` and `Lunate.Contracts`, and
SHALL NOT reference `Lunate.Agent`. Front ends SHALL keep observing decisions through the agent event channel
(`DecisionEvaluated`, `DecisionUnavailable`, `DecisionFailed`). The public shape SHALL land before
#237 publishes `Lunate.Extensibility.Abstractions` as stable.

#### Scenario: An extension requests a decision

- **GIVEN** an extension that resolved `IDecisionService` while a `decision` profile is configured
- **WHEN** it proposes a request with a state and one question
- **THEN** it receives the validated per-question result, and the decision appears in the run's event
  stream like any other

#### Scenario: The extension service preserves the assembly boundary

- **GIVEN** the decision service in `Lunate.Extensibility.Abstractions` and the model in
  `Lunate.Ai.Decisions`
- **WHEN** the layering test runs
- **THEN** Abstractions references `Lunate.Ai.Decisions` and `Lunate.Contracts` but not Agent,
  `Lunate.Ai.Decisions` references `Lunate.Contracts` and nothing above it, and no decision type
  names a provider, a tool or the loop

#### Scenario: One request type reaches the transport

- **GIVEN** a caller's request, a chat adapter and a native transport
- **WHEN** the engine evaluates the request
- **THEN** the same request type is handed to the transport, parsed back into the same result type and
  returned to the caller, with no second request or answer type in between
