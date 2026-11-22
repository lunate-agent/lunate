## MODIFIED Requirements

### Requirement: Decision primitive contract

The decision layer SHALL expose one primitive with four phases — propose, validate, decide, record —
owned by the agent harness and modelled on the Jev-compatible decision API. Its request, question,
answer and result DTOs SHALL live in `Lunate.Decisions.Model` — the one home of the decision model — so
the contract assembly holds no decision request or result type; a request SHALL carry a
**state** (free text, a JSON value, or a list of parts in which an image part is allowed only when
the resolved model's input modalities include images) and **one or more named questions** evaluated
independently against that state. Results SHALL be keyed by question id, and question ids SHALL be
unique within a request. A result SHALL carry the chosen answer, that answer's probability, the full
distribution over the question's answers, the computed **confidence** (one definition across native
and adapter, derived from the distribution), the resolved model id and the method label (`native` or
`adapter`); a box answer SHALL instead carry four image fractions and the provider's confidence. The result SHALL
be the only thing a caller sees of the model exchange: never raw model text or off-contract
probabilities.

#### Scenario: A request carries a state and several named questions

- **GIVEN** a caller that supplies a state and two named questions
- **WHEN** the harness proposes the decision
- **THEN** both questions are evaluated against that same state and each result is returned under its
  own question id

#### Scenario: Callers see validated results only

- **GIVEN** a model response that is off-contract (not valid JSON, an undefined answer, a probability
  outside [0, 1], or probabilities that do not sum to 1 within contract tolerance)
- **WHEN** it is retried once and is off-contract again
- **THEN** the caller receives a failed-decision result naming the model and the reason, never the raw
  response and never a fabricated answer

#### Scenario: A decision is an exchange, not a verdict

- **GIVEN** a decision that failed twice as off-contract
- **WHEN** the result reaches a consumer
- **THEN** it is presented as a failed advice request, not as an approval, denial, answer or routing
  result

#### Scenario: An image state needs a vision-capable model

- **GIVEN** a request whose state contains an image part and a model whose input modalities do not
  include images
- **WHEN** the request is proposed
- **THEN** it fails before any call, naming the model and the missing capability

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
`DecisionFailed`). The public shape SHALL land before #237 publishes
`Lunate.Extensibility.Abstractions` as stable.

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
