## MODIFIED Requirements

### Requirement: Reasoning options from the session level

The factory SHALL resolve the model's API family once and choose the wire parameter from that resolved family: `anthropic-messages` maps the session's thinking level (`off` | `low` | `medium` | `high`) to an extended-thinking budget (2,048 / 8,192 / 32,768 tokens), and `openai-chat`, `openai-responses` and `google` map it to the OpenAI-compatible reasoning effort (low/medium/high). The model's declared reasoning style SHALL be metadata and fallback only: a declared `inline` sends no provider parameter (the model embeds the thinking text in its stream), a model with no declared style sends no reasoning parameters, and a declared `effort` or `budget` decides the wire shape only when the family cannot be resolved from the model or its provider. A level of `off` SHALL send no reasoning parameters for every family and style. The level-to-value mapping and the `/thinking` notice SHALL be culture-invariant, the notice SHALL name what the resolved family will send, and every case SHALL be covered by tests.

#### Scenario: Off sends nothing

- **GIVEN** any resolved API family and any declared reasoning style
- **WHEN** the thinking level is `off`
- **THEN** the request carries no reasoning parameters

#### Scenario: A declared budget on an OpenAI-compatible family still sends effort

- **GIVEN** a model declared `budget` whose resolved API family is `openai-chat`
- **WHEN** the thinking level is `high`
- **THEN** the request carries the high reasoning effort and no Anthropic extended-thinking parameters

#### Scenario: A declared effort on the Anthropic family still sends a budget

- **GIVEN** a model declared `effort` whose resolved API family is `anthropic-messages`
- **WHEN** the thinking level is `high`
- **THEN** the request carries a 32,768-token thinking budget, clamped below the request's max tokens

#### Scenario: Budget maps the level

- **GIVEN** a model whose resolved API family is `anthropic-messages`
- **WHEN** the level is `high`
- **THEN** the request carries a 32,768-token thinking budget, clamped below the request's max tokens

#### Scenario: Effort maps the level

- **GIVEN** a model whose resolved API family is `openai-chat` or `openai-responses`
- **WHEN** the level is `medium`
- **THEN** the request carries the medium reasoning effort

#### Scenario: Inline sends no parameter

- **GIVEN** a model declared `inline`
- **WHEN** the level is `high`
- **THEN** the request carries no reasoning parameters and the inline splitter is active

#### Scenario: A model without a declared style sends nothing

- **GIVEN** a model with no declared reasoning style
- **WHEN** the level is `high`
- **THEN** the request carries no reasoning parameters

#### Scenario: The declared style decides when the family cannot be resolved

- **GIVEN** a model with no API family annotation whose provider resolves to no single chat family
- **WHEN** the thinking level is `high`
- **THEN** the declared `effort` or `budget` style decides the wire shape
