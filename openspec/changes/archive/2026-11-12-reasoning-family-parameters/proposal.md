# Reasoning parameters follow the resolved API family

## Why

`ReasoningParameters.Apply` chooses the wire parameter from the model's declared `reasoning`
style alone. A model declared `budget` that resolves to an OpenAI-compatible provider family
receives Anthropic `thinking` parameters, and a model declared `effort` that resolves to
`anthropic-messages` receives none. Correctly tagged catalog entries are unaffected, which is
why the 0.3.0-rc.1 review (#601) recorded this as a hardening item rather than a release
blocker — but a user override, a discovery hint or a custom catalog entry can produce exactly
that mismatch.

## What Changes

- The resolved API family decides the wire shape: a model on `anthropic-messages` gets the
  extended-thinking budget, and a model on `openai-chat`, `openai-responses` or `google` gets
  the reasoning effort.
- The declared `reasoning` style stays metadata: `null` still means no reasoning support,
  `inline` still sends no provider parameter, and `effort`/`budget` remain the fallback when
  the family cannot be resolved from the model or its provider preset.
- `Describe` (the `/thinking` notice) follows the same resolution, so it names what the request
  will actually carry.
- **BREAKING** (behavioural, not API): a deliberately misdeclared model now receives the
  parameter for its family instead of the parameter for its declared style.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `ai-layer`: the "Reasoning options from the session level" requirement chooses the wire
  parameter from the resolved API family and uses the declared style as metadata/fallback.

## Impact

- `src/Lunate.Ai/ReasoningParameters.cs` — family resolution for `Apply` and `Describe`.
- Tests in `tests/Lunate.Ai.Tests/ReasoningParametersTests.cs` gain the misdeclared-model cases;
  the existing budget cases move to an Anthropic model.
- `tests/Lunate.Agent.Tests/AgentThinkingLevelTests.cs` declares its budget model on the
  Anthropic family, the provider that actually accepts that wire shape.
- `docs/models-and-providers.md` states that the resolved family decides the wire parameter.
- No public API change, no new package, no session format change, no wire protocol change.
