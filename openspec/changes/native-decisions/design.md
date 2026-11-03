# Design: native decisions (System One)

## 1. Verified protocol (fixtures in `fixtures/`)

Nine live calls on 2026-11-02, from this machine, with the configured OpenRouter key and the OpenCode
Go key; the responses are stored verbatim. TypeSafe direct was **not** verified — no key.

| | OpenRouter | OpenCode (Console/Zen) | TypeSafe direct |
|---|---|---|---|
| URL | `POST https://openrouter.ai/api/v1/systemone` | `POST https://opencode.ai/inference/systemone/v1/systemone` | `POST https://api.typesafe.ai/v1/systemone` (docs only) |
| Auth | `Authorization: Bearer` | `Authorization: Bearer` (Go key worked, billed to balance) | Bearer per SDK docs |
| Models | `typesafe/jev-1.14`, alias `~typesafe/jev-latest` | `jev-1.13` (1.14 → "Model is unavailable") | `jev-1.14` per docs |
| Images | accepted without a special header | requires `TypeSafe-Danger-Zone-Images: true` | unknown |

Request, identical on both verified providers:

```json
{ "model": "…", "state": <string | object | {"parts":[…]}>,
  "questions": { "<id>": { "type": "…", "instructions": "…", "criteria": … } } }
```

Question kinds and their **per-kind criteria shapes** (a real trap: the score kind takes an array,
choice and noul take records):

- `noul` — `criteria: { "true": "…", "false": "…" }` → `{ "type": "noul", "noul": 0.90 }`
- `choice` — `criteria: { "<option>": "…" }` → `{ "choice": "frontend", "probabilities": {…}, "confidence": 0.9 }`
- `score` — `criteria: ["level 0", "level 1", …]` (array) → `{ "score": 2, "legend": {"0":…}, "probabilities": {…}, "confidence": 1 }`
- `box` — no criteria, `instructions` only → `{ "box": { "top": 0.15, "bottom": 0.43, "left": 0.44, "right": 0.54 }, "confidence": 0.43 }`

Response:

```json
{ "model": "typesafe/jev-1.14-20261014",
  "answers": { "<id>": { "type": "…", … } },
  "usage": { "input_tokens": 346, "output_tokens": 35, "cost": 0.000014532 },
  "id": "gen-dec-…", "provider": "TypeSafe" }
```

OpenCode adds `usage.opencode { billing_source, list_cost, tokens { input, output, reasoning,
cache_read, cache_write_5m, cache_write_1h } }`. No rate-limit headers were returned.

Errors seen: unknown question type → 400 with `options: ["noul","choice","score","box"]`; unknown
model → 400 "Model … does not exist"; expired key → 401 `api_key_expired`; images without the header
→ 400 "Images are experimental. Send the header …"; unavailable model → 400 "Upstream request
failed: Model is unavailable".

**Contract implications pinned by the fixtures:** the client must send `score.criteria` as an array
and `choice.criteria` as a record; it must map the response by kind, not by a single shape; and it
must be able to add the OpenCode image header conditionally.

## 2. The family

`systemone` joins the four chat families. A provider entry lists its SystemOne base URL in
`baseUrls.systemone`; a model carries `apiFamily: systemone`; discovery marks it when the provider's
metadata says so; otherwise the catalog declares it. `ChatClientFactory` does **not** build an
`IChatClient` for it — the family selects the decision transport.

Presets:

- `openrouter`: adds `systemone: https://openrouter.ai/api/v1/systemone`; same key.
- `opencode`: adds `systemone: https://opencode.ai/inference/systemone/v1/systemone` and
  `imageHeaders: { "TypeSafe-Danger-Zone-Images": "true" }`; this is the Console/Zen subscription,
  not Go — the preset doc comment says so and the Go key is not assumed.
- `typesafe`: new preset, `api.typesafe.ai/v1/systemone`, bearer.

## 3. The client

`SystemOneDecisionClient` is **not** an `IChatClient`:

```csharp
sealed class SystemOneDecisionClient : INativeDecisionClient, INativeDecisionTransport
{
    Task<NativeDecisionResult> GetDecisionAsync(DecisionRequest, CancellationToken);
}
```

- Builds the body: `model` from the resolved id; `state` from the request's `JsonElement` (string,
  object and `parts` all pass through unchanged); `questions` mapped from our kinds, with the
  per-kind criteria shape.
- Adds `imageHeaders` only when the state contains an image part.
- Parses `answers` into the raw `results` JSON the existing validator already expects:
  - `noul` → `{answer: "yes"|"no", probabilities:{yes,noul,no:1-noul}}`
  - `choice` → answer + `probabilities` map
  - `score` → `legend[index]` as the answer, `probabilities` keyed by the legend level
  - `box` → `{answer:"", box:{…}, confidence}`; no distribution
- Throws a typed `DecisionTransportException` carrying the provider's 400 message on error, so the
  decision layer reports "failed, naming the model and the reason" instead of a retry loop on a
  deterministic 400.

**Surfacing.** The 0.2 seam asks the chat client's `GetService(typeof(INativeDecisionClient))`. To
keep that seam without pretending a decision model is a chat model, the factory returns a
`SystemOneChatFacade : IChatClient` whose chat calls throw `NotSupportedException` ("model X is a
decision model; it cannot chat") and whose `GetService` returns the client. This keeps the harness
resolver, usage recording and provenance untouched; the alternative — teaching the harness a second
resolution shape — is a larger change for the same behaviour and can follow.

## 4. Discovery, profiles, pinning

- Discovery: a provider that lists a model with a decisions capability gets a catalog entry with
  `apiFamily: systemone`; providers that don't (OpenRouter's chat model list omits `jev-1.14`) rely on
  the catalog. Both paths end in the same resolver.
- The `decision` usage accepts a `systemone` profile; a `chat` usage refuses it by capability, naming
  the family.
- **Profiles pin an exact version.** An alias (`~typesafe/jev-latest`) or an id ending in `latest` is
  refused at profile validation with the alias and the version it currently resolves to, because an
  upgrade can change probabilities. Discovery may show aliases; profiles may not use them.
- A native profile records `native`; an adapter profile records `adapter`; the existing
  "adapter cannot pass for native" rule is unchanged.

## 5. Contract 1.16.0 (lockstep minor)

Additive on the stable contract:

1. `DecisionQuestionKind.Box`.
2. `DecisionAnswer.Box` — `DecisionBox(Top, Bottom, Left, Right, Confidence)`; `Answer` is empty and
   `Distribution` empty for a box answer. `Probability` carries the confidence so existing consumers
   that read a single number keep working.
3. Image state validation: a state containing an image part is accepted only when the resolved
   model's input modalities include images; otherwise the request fails before any call, naming the
   model and the missing capability.
4. `INativeDecisionTransport` and `NativeDecisionResult(Text, DecisionUsage?)` — the old
   `INativeDecisionClient` stays and forwards, so a consumer built against 1.15.0 still works.

Validator additions: box fractions are finite in [0,1], `top < bottom`, `left < right`, confidence in
[0,1]; a box answer on a non-box question (or vice versa) is off-contract.

## 6. Cost

`usage.cost` is provider-reported and real (OpenRouter and OpenCode both return it; OpenCode also
returns `list_cost`). The decision record carries `DecisionUsage(InputTokens, OutputTokens,
CostUsd, ProviderReported)`; `/cost` shows it under the decision usage, with the provider-reported
amount preferred over a catalog estimate and the estimate used only when the field is absent
(TypeSafe direct until verified). Provenance and family stay visible next to the amount.

## 7. Adapter path: box on vision chat models

The adapter already asks a chat model for decision JSON. It gains box: the schema returns four
fractions + confidence, validated like a native box. It is offered only when the model's input
modalities include images; otherwise the decision fails with "this model cannot decide: box questions
need image input". No streaming, no new tool surface.

## 8. Tests

- Fixture replay for both verified providers: noul/choice/score, box, both error shapes, OpenCode's
  `usage.opencode`, and the OpenCode image header being added only when images are present.
- Contract: box validation (edges, ordering, confidence, wrong-kind), image gating by modality,
  1.15.0 consumers still compile and run through the forwarding method.
- Decision layer: the existing suite runs with a native profile in place of the adapter; provenance
  reads `native`; a chat usage on the systemone profile is refused naming the family; an alias
  profile is refused naming the resolved version.
- Cost: provider-reported cost recorded; absent cost falls back to the catalog estimate.
- Live smoke: OpenRouter and OpenCode, skipped without a key; TypeSafe direct once a key exists.

## 9. Risks

- **OpenCode serves only `jev-1.13` today**, and 1.14 returns "unavailable". Profiles pin 1.13 there;
  the preset does not advertise 1.14.
- **Box and images are experimental** at the provider (the header says so). They sit behind the
  capability check and are fixture-tested, not relied on by default decisions.
- **TypeSafe direct is unverified.** First implementation task: probe it and record the fixture; if
  its shape differs, it becomes a preset variant, not a second family.
- The per-kind criteria asymmetry is a provider contract quirk; the client owns it in one mapper with
  a fixture per kind so a future provider change fails a test, not a user's decision.
