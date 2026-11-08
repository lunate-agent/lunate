# Design — Reasoning parameters follow the resolved API family

## Context

`ReasoningParameters` sits between the agent loop (`AgentHarness.BuildOptions`) and the chat
client pipeline. It receives a `ModelInfo` and the session's `ThinkingLevel`, and writes either
`ChatOptions.Reasoning` (effort) or an Anthropic `MessageCreateParams` with a thinking budget
into `ChatOptions.RawRepresentationFactory`.

Today the choice is made from `ModelInfo.Reasoning` alone (`src/Lunate.Ai/ReasoningParameters.cs:38`).
The model's resolved API family is already available on the same record — `ModelInfo.ApiFamily`
holds the wire name a `ModelProfileResolver` resolved, and a catalog/discovery entry may carry it
directly — and `ChatClientFactory` already resolves the family the same way for its pipeline
decorators (`ResolvesToAnthropicMessages`, `src/Lunate.Ai/ChatClientFactory.cs:165`).

```text
+-------------------+        +------------------------+        +----------------------+
| AgentHarness      |        | ReasoningParameters    |        | Resolved model facts |
| BuildOptions      |------->| Apply/Describe         |<-------| ModelInfo            |
+-------------------+        +-----------+------------+        |  ApiFamily (wire)    |
                                         |                     |  Provider (preset)   |
                                         |                     |  Reasoning (declared)|
                                         v                     +----------------------+
                              +------------------------+
                              | ProviderApiFamilies    |
                              | ProviderPresets        |
                              +------------------------+
```

Assumptions: the model reaching `Apply` has, where the rest of the pipeline knows it, either an
`ApiFamily` annotation or a provider whose preset serves exactly one chat family. The diagram is
lightweight C4 (component-level) because the change is local to one static class.

## Goals / Non-Goals

**Goals:**

- One resolution point decides the wire shape: Anthropic family -> thinking budget; every other
  chat family -> reasoning effort.
- A deliberately misdeclared `ModelInfo` (declared style disagreeing with the resolved family)
  sends the family's parameter.
- `Describe` reports the same decision as `Apply`.
- Keep the existing behaviour for correctly declared models and for callers that cannot resolve
  a family.

**Non-Goals:**

- A Google-specific thinking budget (the family keeps the MEAI reasoning-effort shape it already
  receives today).
- Applying the profile's validated `reasoningBudget` (unchanged: the session level is the
  parameter).
- Changing the `ModelInfo` public record or adding a new dependency.

## Decisions

1. **A shared private `WireStyle(ModelInfo)` helper feeds both `Apply` and `Describe`.**
   Two independent style decisions would let the `/thinking` notice describe a request the
   pipeline never sends. Alternatives considered: changing the public `Apply` signature to take a
   `ProviderApiFamily`; rejected because it is a public API change for a resolution the record
   already carries, and because callers without a family would need a fallback type anyway.

2. **Resolution precedence: declared `ApiFamily` -> provider preset's single chat family ->
   declared `Reasoning` style.** This mirrors `ProviderFamilyResolver`/`ChatClientFactory`: an
   explicit annotation wins, a single-family preset answers, ambiguity defers. The declared style
   is the last resort, so a weird or unregistered provider keeps today's behaviour.

3. **`inline` and `null` are decided before the family.** They are model metadata, not wire
   parameters: an `inline` model embeds thinking in its stream, and a `null` style declares no
   reasoning support. Sending a budget to either would be wrong even on the Anthropic family.

4. **`systemone` is not a chat family.** If it ever reaches `Apply`, it falls through to the
   declared style; the factory refuses chat usage of it before a request is built.

5. **Non-Anthropic chat families get effort.** `openai-chat`, `openai-responses` and `google` use
   the MEAI `ChatOptions.Reasoning` effort shape. Google has no budget wiring in this layer today
   and inventing one is a separate feature.

## Risks / Trade-offs

- **A user-declared `budget` on an OpenAI-compatible provider now silently sends effort.**
  -> Intended: the family decides, and the spec scenario pins it. The `/thinking` notice names
  effort, so the session shows what is sent.
- **A future fifth family maps to effort by default.**
  -> The mapping is an explicit switch with an Anthropic case and a default; a fifth family gets
  reviewed when it is added, and `ChatClientFactory` already refuses unwired families at load time.
- **Preset lookup makes `ReasoningParameters` depend on `ProviderPresets`.**
  -> Both live in `Lunate.Ai`; `ChatClientFactory` already uses the same lookup for decoration.

## Migration Plan

No data or session-format migration. The change is a runtime selection fix; roll back by
reverting the commit. Correctly tagged catalog entries produce the same requests as before.

## Open Questions

None. No in-force ADR constrains this selection; ADR-0003 (the loop runs tools) is unaffected.
