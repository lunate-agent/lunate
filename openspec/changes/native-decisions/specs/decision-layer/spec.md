## MODIFIED Requirements

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

## REMOVED Requirements

### Requirement: Three question kinds

**Reason**: replaced by Four question kinds; a decision model may answer box questions, which have no
answer set and therefore no probability distribution.

**Migration**: existing questions keep their behaviour — yes/no, choice and score are unchanged. The
enum gains `Box`; switches on the kind must handle it or use the default branch, and the contract
version moves 1.15.0 → 1.16.0 (lockstep minor).

## ADDED Requirements

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
exposure. The state SHALL carry only the fields those questions need — tool, arguments, workspace
root, risk tier — with untrusted content in its own named field, and untrusted content SHALL NOT
appear in any instruction or criteria. Code SHALL combine the atomic answers against one threshold,
`decisions.approvalsThreshold`, expressed in confidence and defaulting to the mapping `2t − 1` of the
existing probability threshold, so the shipped behaviour is unchanged. Any signal at or above it SHALL
escalate; no risk-tier table is a design default. Shadow mode SHALL record every atomic answer. No
answer SHALL approve a tool call; a decision can only tighten the gate.

#### Scenario: Four signals, one request

- **GIVEN** a tool call that writes outside the workspace and reaches the network
- **WHEN** the approval gate decides
- **THEN** one request carries all four atomic questions, the two relevant signals are recorded as
  true with their probabilities, and the call escalates

#### Scenario: Untrusted content stays data

- **GIVEN** a tool argument containing text that instructs the model to answer yes
- **WHEN** the gate questions are built
- **THEN** that text appears only in the state's `untrusted_content` field and in no instruction or
  criteria

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
