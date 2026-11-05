# decision-layer Specification

## Purpose
One shared decision primitive for the agent harness — a mirror of the Jev-compatible decision API that carries a state plus named questions through propose, validate, decide and record — and the contract governing its callers: the three question kinds, the single technical validator, the extension-facing `IDecisionService` surface, the four opt-in consumers (model routing, the escalate-only approval gate, display-only question suggestions and the information-only `decide` tool), shadow-mode guarantees, and the invariants that a decision may only tighten the user's allow envelope and is always observable with its provenance through the agent event channel.

## Requirements

### Requirement: Decision primitive contract

The decision layer SHALL expose one primitive with four phases — propose, validate, decide, record —
owned by the agent harness and modelled on the Jev-compatible decision API. Its request, question,
answer and result DTOs SHALL live in `Lunate.Contracts`; a request SHALL carry a
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

### Requirement: Contract validity

One validator SHALL check every response against the question that produced it: the chosen answer
SHALL be one of the question's defined answers, every defined answer SHALL carry one finite
probability in [0, 1], the probabilities SHALL sum to 1 within the contract tolerance (one part in a
million), and the chosen answer's probability SHALL be its own. A malformed response SHALL be retried
once and then reported as failed, naming the model and the reason. No consumer SHALL validate a
response itself.

#### Scenario: A well-formed response validates

- **GIVEN** a yes/no question and a response naming `yes` with probability 0.7 and `no` with 0.3
- **WHEN** the validator runs
- **THEN** the decision validates with the chosen answer and its own probability

#### Scenario: An undefined answer fails the contract

- **GIVEN** a response naming an answer outside the question's defined answers
- **WHEN** the validator runs
- **THEN** the response is rejected as off-contract

#### Scenario: Probabilities must form a distribution

- **GIVEN** per-answer probabilities that do not sum to 1 within tolerance, or a probability that is
  not a finite value in [0, 1]
- **WHEN** the validator runs
- **THEN** the response is rejected as off-contract before any consumer sees a probability

### Requirement: Extension-facing service

The primitive SHALL be available to extensions as a service: `IDecisionService` SHALL live in
`Lunate.Extensibility.Abstractions`, the evaluator SHALL live in `Lunate.Agent`, and
`Lunate.Extensibility` SHALL bridge and register the service so an extension can resolve it. The
Abstractions assembly SHALL reference `Lunate.Contracts` and SHALL NOT reference `Lunate.Agent`.
Front ends SHALL keep observing decisions through the agent event channel
(`DecisionEvaluated`, `DecisionUnavailable`, `DecisionFailed`). The public shape SHALL land before
#237 publishes `Lunate.Extensibility.Abstractions` as stable.

#### Scenario: An extension requests a decision

- **GIVEN** an extension that resolved `IDecisionService` while a `decision` profile is configured
- **WHEN** it proposes a request with a state and one question
- **THEN** it receives the validated per-question result, and the decision appears in the run's event
  stream like any other

#### Scenario: The extension service preserves the assembly boundary

- **GIVEN** the decision service in `Lunate.Extensibility.Abstractions` and decision DTOs in `Lunate.Contracts`
- **WHEN** the layering test runs
- **THEN** Abstractions references Contracts but not Agent, and no decision contract type names a
  provider, a tool or the loop

### Requirement: Decision availability and provenance

The decision layer SHALL be available only when a `decision` usage profile is configured, and SHALL
report itself unavailable and stay off otherwise — it SHALL NOT borrow the chat profile or any other
usage's model. Every proposed decision SHALL emit a channel event, and every validated decision SHALL
record provenance: the model id and the method label (`native` for a provider decision API, `adapter`
for a chat-model path), so an adapter decision cannot pass for a native one in `/cost` or the
transcript.

#### Scenario: No decision profile keeps the layer off

- **GIVEN** a session with no `decision` usage profile configured
- **WHEN** a consumer proposes a decision
- **THEN** the layer emits an unavailable event and proposes nothing, and no other usage's model is
  called

#### Scenario: Provenance is recorded on every validated decision

- **GIVEN** a validated decision from an adapter-profiled model
- **WHEN** the decision is recorded
- **THEN** the record names the resolved model id, the `adapter` method label, the answer and its
  probability

### Requirement: Consumers

Decision consumption SHALL be opt-in per consumer, and every consumer SHALL use the same primitive.
This scope SHALL define exactly four consumers: **model routing** (acting), the **approval gate**
(acting, escalate-only), **question suggestions** (display only), and the **`decide` tool**
(information only). Observers SHALL receive decisions as channel events. Shadow mode SHALL be
available for each acting consumer and SHALL record what it would have done without acting.

#### Scenario: Activation is opt-in and off by default

- **GIVEN** a fresh configuration that did not opt into decision consumption
- **WHEN** the session asks for tool approvals
- **THEN** the decision layer proposes nothing and no consumer applies any decision

#### Scenario: A front end observes through events

- **GIVEN** a configured decision whose validated result is recorded
- **WHEN** a front end (TUI, print mode, export) reads the event stream
- **THEN** it sees the answer, probability, model id and method label without calling the model or
  reading the primitive's internals

#### Scenario: Nothing chosen through a question approves a call

- **GIVEN** a decision surfaced as a suggestion inside a question the user is answering
- **WHEN** the user's answer or an auto-answer is applied
- **THEN** the answer grants no tool approval; approving stays a separate approval prompt

### Requirement: Approval gate escalates, never blocks

The approval-gate consumer SHALL act only by escalation, enforced by the agent harness at the single
approval seam. For a `deny` recommendation on a call the user's allow envelope would permit, the
harness SHALL call an approver capable of prompting a person with an `ApprovalRequest` that carries
the escalation flag, recommendation, probability, model id and `native`/`adapter` method. This SHALL
force the ordinary user prompt even when the approver ignores the flag; the TUI prompt SHALL show
`decision model recommends deny (<probability>, <model>)`. If the active approver cannot prompt a
person, the harness SHALL deny the call, fail-closed, and report that the decision escalated and no
user was present. This rule includes print mode, unattended queue runs, ACP without a client prompt
and `--yolo`: when the opt-in decision gate is enabled with `--yolo`, calls are auto-approved except
that a decision `deny` is denied because no user is present. Plain `--yolo` SHALL remain unchanged.
Calls that do not enter the approval seam, including `ask_user`, SHALL NOT be evaluated by the
approval-gate consumer.
An `approve` recommendation SHALL NOT skip a prompt, waive a denial, or authorize a call the tool
rules exclude from any decision model. Failed or unavailable decisions SHALL leave the ordinary
approval flow unchanged.

#### Scenario: A deny recommendation becomes a user ask

- **GIVEN** an allowed call and a decision that recommends `deny` while a user is present
- **WHEN** the approval seam resolves the call
- **THEN** the user is asked, the decision and its probability are shown, and the user's answer decides

#### Scenario: Unattended escalation fails closed

- **GIVEN** the same recommendation with no user present
- **WHEN** the seam resolves the call
- **THEN** the call is denied, the session continues, and a notice records that the decision escalated
  with no user present

#### Scenario: An approver that ignores the flag still prompts

- **GIVEN** a prompt-capable approver that does not inspect the escalation flag
- **WHEN** the harness passes it an escalation request
- **THEN** the approver is called and its ordinary user decision determines whether the call runs

#### Scenario: `--yolo` with the decision gate fails closed on deny

- **GIVEN** `--yolo` and the opt-in decision gate are both active
- **WHEN** the decision recommends `deny`
- **THEN** the call is denied with a notice that no user was present; without the decision gate,
  `--yolo` keeps its existing behavior

#### Scenario: An approve recommendation grants nothing

- **GIVEN** a call that requires the user's approval and a decision that recommends `approve`
- **WHEN** the seam resolves the call
- **THEN** the approval prompt still runs

### Requirement: The decide tool is information only

The chat model SHALL be able to call a `decide` tool that hands the primitive a state and one or more
named questions and returns the per-question results, batched in a single call. The tool SHALL be
offered only when a `decision` usage profile is configured, its cost SHALL appear under the `decision`
usage in `/cost`, and its result SHALL be information for the model — never an approval, never an
answer to a question the user was asked, and never a substitute for the approval prompt.

#### Scenario: One call returns several answers

- **GIVEN** a configured `decide` tool and a call with a state and three questions
- **WHEN** the tool returns
- **THEN** the model receives three keyed results, each with its answer, probability and distribution

#### Scenario: A decision taken through the tool approves nothing

- **GIVEN** a model that calls `decide` and receives an `approve` answer for a state describing a tool
  call
- **WHEN** the model then calls that tool
- **THEN** the ordinary approval path runs exactly as it would have without the decision, and no
  approval is granted or skipped

#### Scenario: The tool is absent without a decision profile

- **GIVEN** a session with no `decision` usage profile configured
- **WHEN** the tool list is assembled
- **THEN** the `decide` tool is not offered

### Requirement: Shadow-mode guarantees

A shadow consumer SHALL observe every decision it registered for and record what it would have done,
and SHALL NOT change execution: it cannot stop, delay or redirect a tool call, change an approval
outcome, change routing, suppress a prompt, or alter the session's recorded history beyond its own
decision records and would-have records. Shadow output SHALL be presentable per method label so an
adapter's shadow record cannot pass for a native one.

#### Scenario: Shadow records instead of acting

- **GIVEN** shadow-mode consumption is opted in for the approval gate
- **WHEN** an ask the decision model would escalate arrives
- **THEN** the ask runs under the ordinary approval flow, and the record shows the would-have
  escalation with its provenance

#### Scenario: Shadow changes nothing observable

- **GIVEN** a session with shadow consumption active and a `decision` profile configured
- **WHEN** the run's event stream and session file are compared against the same run with decision
  consumption off
- **THEN** they differ only by decision events, usage attribution and would-have records

#### Scenario: Shadow failures do not change the flow

- **GIVEN** the decision layer is unavailable or a contract is invalid while shadow consumption is
  active
- **WHEN** the ask continues
- **THEN** the ordinary approval flow runs unimpeded and the failure is recorded for the observer

### Requirement: Tighten-only enforcement

The invariant that a decision may only tighten the user's allow envelope SHALL be enforced in one
place — the harness's approval seam — not once per consumer. A decision may escalate an ask the
envelope would allow; it SHALL never override a denial, never waive a prompt, and never authorize a
call the tool rules exclude from any decision model. No consumer, acting or shadow, can weaken the
invariant from its own code.

#### Scenario: A failed decision may not loosen the gate

- **GIVEN** an acting consumer whose decision failed, was unavailable or was off-contract
- **WHEN** the seam resolves the ask
- **THEN** the ordinary approval flow answers the ask, and the failure cannot widen the envelope

#### Scenario: A decision cannot authorize an excluded call

- **GIVEN** a call the tool rules exclude from any decision model
- **WHEN** a decision recommends `approve` for it
- **THEN** the call still requires the user's explicit approval

### Requirement: Boundary between the loop and the executor

The harness loop SHALL own proposing, validating, deciding and recording, and the executing tool SHALL
be reached only after the decision has resolved. A tool SHALL NOT observe another tool's decision
records or turn its own error into a decision failure. A decision failure reaching the loop SHALL
surface as an instructing error result produced by the harness, never as an exception out of the
stream.

#### Scenario: The executor sees a resolved call

- **GIVEN** a tool call whose decision was resolved
- **WHEN** the tool executes
- **THEN** it receives only approved arguments and no decision request, result or failure

### Requirement: Question suggestions attach only where a person answers

The question-suggestions consumer SHALL be proposed only in the interactive session, when the user
has enabled `decisions.questions: suggest` and the question is displayed to and answered by that
user. Print mode, unattended queue runs and ACP SHALL NOT propose a question decision for
`ask_user`: the tool SHALL degrade to its no-user result without suggested options, and enabling
`suggest` there — even with a `decision` profile available — SHALL NOT cause any question decision
exchange or event. An unattended run that needs a judgement SHALL receive it through the `decide`
tool, whose result stays information-only per its own requirement.

#### Scenario: A print run with suggestions enabled gets no suggestion exchange

- **GIVEN** print mode configured with `decisions.questions: suggest` and a `decision` profile
- **WHEN** the model calls `ask_user`
- **THEN** no question decision is proposed, and the tool result is the no-user degrade with none
  of the options marked as suggested

#### Scenario: An ACP session with suggestions enabled shows no question and no suggestion

- **GIVEN** an ACP session configured the same way
- **WHEN** the model calls `ask_user`
- **THEN** the editor is never shown a question or a suggestion, the tool returns the no-user
  result, and no question decision event reaches the event stream

#### Scenario: The decide tool stays the unattended route to a judgement

- **GIVEN** an unattended run with the `decide` tool offered
- **WHEN** the model needs a judgement
- **THEN** it gets the answer with its probability and provenance through `decide`, independently
  of `ask_user`, and no suggestion is attached to a question nobody answers

### Requirement: Four question kinds

The primitive SHALL support four question kinds: **noul** (a probability that the answer is yes, the
no probability being its complement), **choice** (between 2 and 255 named options, one probability per
option), **score** (an ordered rubric of at least two named levels, one probability per level, where
the order belongs to the question) and **box** (instructions in; four fractions of the image —
`top`, `bottom`, `left`, `right` — plus a confidence out, with no answer set and no distribution).
A question MAY target a JSON path inside a JSON state; the target SHALL be resolved against that
state before the exchange and SHALL NOT be persisted. Box questions SHALL be offered only to a model
whose input modalities include images.

#### Scenario: A choice question reports per-option probabilities

- **GIVEN** a choice question with options `small`, `medium` and `large`
- **WHEN** the decision validates
- **THEN** the result names the chosen option, its probability, and all three option probabilities

#### Scenario: An option count outside the contract is refused

- **GIVEN** a choice question with one option, or with 256 options
- **WHEN** the request is proposed
- **THEN** it is refused as malformed before any model call is made

#### Scenario: A score question preserves rubric order

- **GIVEN** a score question with ordered levels `reject`, `weak`, `strong`
- **WHEN** the result is presented
- **THEN** the levels appear in the question's declared order, and the chosen level carries its own
  probability

#### Scenario: A box question returns four fractions

- **GIVEN** a box question against a state with an image
- **WHEN** the answer validates
- **THEN** the result carries `top`, `bottom`, `left`, `right` each in [0, 1] with `top < bottom` and
  `left < right`, plus a confidence in [0, 1], and no distribution

#### Scenario: A box answer is refused on another kind

- **GIVEN** a box answer returned for a choice question, or a distribution returned for a box question
- **WHEN** the validator runs
- **THEN** the response is rejected as off-contract

#### Scenario: A JSON-path question evaluates one field

- **GIVEN** a JSON state and a question targeting `/diff/header`
- **WHEN** the decision is proposed
- **THEN** the exchange is about that field, and the request records no path beyond the exchange

### Requirement: Native System One transport

A provider entry MAY offer the `systemone` family at a base URL; a model whose family is `systemone`
SHALL be served by a native decision transport, not by an `IChatClient`. The transport SHALL send the
provider's request shape — model, state, questions with the per-kind criteria shape the provider
requires — and SHALL return raw `answers` JSON to the existing validator. Provider differences SHALL
be data on the preset, not separate families: a criteria-shape override where a provider differs and
image-only headers applied when the request contains image content. A deterministic 4xx SHALL surface
as a typed transport failure naming the provider's message and, for a 422, the offending field, not be
retried as an off-contract response. A chat usage SHALL refuse a `systemone` model, naming the family.

The transport SHALL enforce the provider's request rules before sending where it can do so cheaply:
a UTF-8 JSON body of at most 32 MiB (413 maps to a named error), at most 64 nesting levels in `state`,
`instructions` and `criteria`, finite numbers only, and identifiers or exact large quantities sent as
strings. It SHALL gzip bodies above a threshold, retry `429` and `529` with exponential backoff and
jitter honouring `retry-after` (at most three attempts), and surface `422` fields and the
`max_tokens_exceeded` 400 as "state too large for this model".

Discovery SHALL read the provider's models endpoint and fall back to the catalog; recorded response
fixtures SHALL be validated against the provider's published OpenAPI snapshot in a contract test that
fails on schema drift.

#### Scenario: A native decision does not go through chat

- **GIVEN** a decision profile on a `systemone` model
- **WHEN** a consumer proposes a decision
- **THEN** the transport is called and no chat client is constructed

#### Scenario: A deterministic provider error is not retried

- **GIVEN** a provider 400 for an unknown question kind
- **WHEN** the decision runs
- **THEN** it fails once with the provider's message, without a second call

#### Scenario: Per-provider differences stay in the preset

- **GIVEN** a provider that requires an image header and a provider that does not
- **WHEN** each sends the same image-bearing request
- **THEN** only the first sends the header, and both parse through the same response mapper

#### Scenario: A decision model cannot chat

- **GIVEN** a chat usage profile on a `systemone` model
- **WHEN** the profile is validated
- **THEN** it is refused by name, and no chat request is attempted

### Requirement: Decision cost

A native decision response's `usage` SHALL be recorded: input tokens, output tokens and the
provider-reported cost, marked as provider-reported. `/cost` SHALL show the decision usage separately
from chat usages, with the provider-reported amount preferred over a catalog estimate and the
estimate used only when the provider reports no cost. The record SHALL keep the model id and the
method label next to the amount.

#### Scenario: Provider-reported cost reaches /cost

- **GIVEN** a native decision whose response reports `usage.cost`
- **WHEN** the decision is recorded
- **THEN** `/cost` shows that amount under the decision usage, marked provider-reported

#### Scenario: A missing cost falls back to the estimate

- **GIVEN** a native response without a cost field
- **WHEN** the decision is recorded
- **THEN** the catalog estimate is used and marked as an estimate

### Requirement: Decision profile pinning

A decision profile on a `systemone` model SHALL name an exact model version. An alias — a `~`-prefixed
id or an id ending in `latest` — SHALL be refused at profile validation, naming the alias and the
version it currently resolves to, because a provider upgrade can change probabilities behind the
alias. Discovery MAY display aliases; a profile SHALL NOT persist one.

#### Scenario: An alias profile is refused

- **GIVEN** a profile naming `~typesafe/jev-latest`
- **WHEN** the profile is validated
- **THEN** it is refused, naming the alias and the exact version it resolves to

#### Scenario: An exact version is accepted

- **GIVEN** a profile naming `typesafe/jev-1.14-20261014`
- **WHEN** it is validated
- **THEN** it is accepted and the decision records that model id

### Requirement: Confidence

The layer SHALL compute confidence from the answer's own distribution, for native and adapter results
alike, using one definition: noul `|2p − 1|`; choice `(p_max − 1/n) / (1 − 1/n)`; score
`max(0, 1 − Σ_i p_i·|i − m| / MAD_unif)` with `MAD_unif = (1/n)·Σ_i |i − (n−1)/2|`; box the provider's
confidence. The computed value SHALL be recorded in provenance next to the distribution and SHALL be
the value the public contract exposes.

#### Scenario: Confidence matches the published formulas

- **GIVEN** a choice answer `(0.6, 0.3, 0.1)`, a score answer `(0, 0.57, 0.43)` and a noul `p = 0.5`
- **WHEN** confidence is computed
- **THEN** the values are `0.4`, `0.35` and `0`, matching the provider's documented examples

#### Scenario: Native and adapter agree

- **GIVEN** the same distribution from a native answer and from an adapter answer
- **WHEN** both results are recorded
- **THEN** their confidence values are equal

### Requirement: Confidence-gated routing

When a decision routes to a chat profile, a result below `decisions.routingMinConfidence` (default
`0.5`) SHALL NOT take the argmax: the configured chat profile stays in force. For the single-choice
strategy the gate SHALL apply to the chosen option's confidence; for the per-profile noul strategy it
SHALL apply to the winner's margin over the runner-up. At or above the gate the winning profile SHALL
be used. The transcript SHALL record the distribution, the gate value and the outcome. Each routing
option SHALL carry a short user-written description used as its criteria; a profile without a
description is allowed with a one-time warning.

The router SHALL pick its strategy from the decision model's catalog entry: a single `choice` question
by default, and one `noul` question per routing profile in the same request when the entry is
`choiceOrderSensitive` (jev-1.13), because sorting options by name only moves a positional bias to the
alphabetically first profile. Provenance SHALL name the strategy used.

#### Scenario: Low confidence keeps the chat profile

- **GIVEN** a routing decision with confidence below the configured minimum
- **WHEN** the route is applied
- **THEN** the configured chat profile stays in force, and the transcript says why

#### Scenario: A close call keeps the chat profile

- **GIVEN** a `choiceOrderSensitive` model, four profiles with per-profile noul answers, and the two
  leading probabilities `0.8` and `0.55`
- **WHEN** the margin gate is applied at `0.5`
- **THEN** the configured chat profile stays in force, and provenance records the `noul` strategy and
  both probabilities

#### Scenario: A positional bias is not mistaken for a decision

- **GIVEN** a model whose catalog entry is `choiceOrderSensitive`
- **WHEN** the router builds its questions
- **THEN** it sends one noul question per profile in one request, not one choice question, and records
  the strategy as `noul-per-profile`

#### Scenario: A missing description warns once

- **GIVEN** routing enabled with a profile that has no description
- **WHEN** the session starts
- **THEN** one transcript warning says routing quality depends on the description, and the session
  continues

### Requirement: Atomic approval questions

The approval gate SHALL ask several atomic noul questions about one structured state in one request,
covering at least writes outside the workspace, network access, irreversible deletion and credential
exposure. The state SHALL carry only the fields those questions need — tool, workspace root, risk
tier, and the call's arguments exactly once, as untrusted data in a single `untrusted_content` field
— and untrusted content SHALL NOT appear in any instruction or criteria. Code SHALL combine the
atomic answers against one threshold, `decisions.approvalsThreshold`, expressed in confidence
(default `0`, the `2t − 1` mapping of the former `0.5` probability threshold), so the shipped
behaviour is unchanged. Any signal at or above it SHALL escalate; no risk-tier table is a design
default. Shadow mode SHALL record every atomic answer. No answer SHALL approve a tool call; a
decision can only tighten the gate.

#### Scenario: Four signals, one request

- **GIVEN** a tool call that writes outside the workspace and reaches the network
- **WHEN** the approval gate decides
- **THEN** one request carries all four atomic questions, the two relevant signals are recorded as
  true with their probabilities, and the call escalates

#### Scenario: Untrusted content stays data

- **GIVEN** a tool argument containing text that instructs the model to answer yes
- **WHEN** the gate questions are built
- **THEN** that text appears once, only in the state's `untrusted_content` field, and in no
  instruction or criteria

### Requirement: Decision state size

Decision states SHALL stay small and structured. The transcript SHALL warn when a state is estimated
above the model's default per-question budget (about 28k tokens of the 32k limit, estimated at four
characters per token), because beyond it the provider's long-context mode is slower and less accurate.
Long-context mode SHALL NOT be enabled implicitly; it is an explicit per-provider option. The
tool-result consumer SHALL send head and tail with a truncation marker rather than a whole output.

#### Scenario: A large state warns

- **GIVEN** a state estimated above the per-question budget
- **WHEN** the decision is proposed
- **THEN** the transcript warns that accuracy and latency degrade beyond the default budget and that
  long context is opt-in, and the request is still sent

#### Scenario: A tool result is truncated

- **GIVEN** a tool output larger than the consumer's budget
- **WHEN** it becomes decision state
- **THEN** only its head and tail are sent, and the state says it was truncated
