# Design: native decisions (System One)

## 1. Verified protocol (fixtures in `fixtures/`)

Nine live calls on 2026-11-02, from this machine, with the configured OpenRouter key and the OpenCode
Go key; the responses are stored verbatim. TypeSafe direct was **not** verified — no key — so it is
deliberately **out of scope for 0.3**: no unverified preset ships.

| | OpenRouter | OpenCode (Console/Zen) |
|---|---|---|
| URL | `POST https://openrouter.ai/api/v1/systemone` | `POST https://opencode.ai/inference/systemone/v1/systemone` |
| Auth | `Authorization` header, bearer token | same (a Go key is accepted and bills to the pay-as-you-go balance) |
| Models | `typesafe/jev-1.14`, alias `~typesafe/jev-latest` | `jev-1.13` (1.14 → "Model is unavailable") |
| Images | accepted without a special header | requires `TypeSafe-Danger-Zone-Images: true` |

Request, identical on both verified providers:

```json
{ "model": "…", "state": <string | object | array | {"parts":[…]}>,
  "questions": { "<id>": { "type": "…", "instructions": "…", "criteria": … } } }
```

Question kinds and their **per-kind criteria shapes** (a real trap: the score kind takes an array,
choice and noul take records):

- `noul` — optional `criteria: { "true": "…", "false": "…" }` → `{ "type": "noul", "noul": 0.90 }`
- `choice` — `criteria: { "<option>": "…" }` → `{ "choice": "frontend", "probabilities": {…}, "confidence": 0.9 }`
- `score` — `criteria: ["level 0", "level 1", …]` (array) → `{ "score": 1.7, "legend": {"0":…}, "probabilities": {…}, "confidence": 0.9 }`
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
must be able to add the OpenCode image header conditionally. The fixtures are checked against
TypeSafe's published OpenAPI (`api.typesafe.ai/openapi.json`, stored as
`fixtures/typesafe-openapi.json`) in a contract test, so a provider drifting from the published shape
fails a test rather than a user's decision.

## 2. Project layout: MEAI-shaped, split by concern

Shaped like `Microsoft.Extensions.AI` so a future MEAI decision abstraction is a mechanical swap.

- **`Lunate.Ai.Decisions`** (new project, internal): `IDecisionClient`, shaped like MEAI's clients —
  `GetDecisionAsync(DecisionRequest, DecisionOptions?, CancellationToken)`, `GetService(Type)` /
  `GetService<T>()`, `DelegatingDecisionClient` for middleware, a `DecisionClientBuilder`, and
  OpenTelemetry instrumentation following MEAI's activity-source conventions. The chat-model
  **adapter** lives here as an `IDecisionClient` over an `IChatClient`. Depends on
  `Microsoft.Extensions.AI.Abstractions` and nothing vendor-specific.
- **`Lunate.Ai.SystemOne`** (new project, internal): the System One HTTP client implementing
  `IDecisionClient`, plus its preset quirk data and protocol fixtures. Loaded only when a decision
  profile on the `systemone` family is used.
- **`Lunate.Agent`**: the decision primitive and validator depend only on `IDecisionClient`, the same
  way the agent loop depends only on `IChatClient`.
- **`Lunate.Contracts`**: the public request/result types extensions see stay unchanged. Mapping
  between them and the internal `IDecisionClient` types happens at the agent boundary.
- Neither new project is packed, so nothing new is frozen.

`LayeringChecker` gains the edges: `Decisions → MEAI.Abstractions`; `SystemOne → Decisions`;
`Agent → Decisions`; nothing references `SystemOne` except composition. A test asserts the graph, and
a second asserts `Lunate.Contracts` references neither new project.

## 3. The family and the presets

`systemone` joins the four chat families. A provider entry lists its SystemOne base URL in
`baseUrls.systemone`; a model carries `apiFamily: systemone`; discovery marks it when the provider's
metadata says so; otherwise the catalog declares it. `ChatClientFactory` does **not** build an
`IChatClient` for it — the family selects the decision transport.

Presets:

- `openrouter`: adds `systemone: https://openrouter.ai/api/v1/systemone`; same key. Advertises
  `typesafe/jev-1.14` (box supported).
- `opencode`: adds `systemone: https://opencode.ai/inference/systemone/v1/systemone` and
  `imageHeaders: { "TypeSafe-Danger-Zone-Images": "true" }`. This is the Console/Zen surface: a Go
  key is accepted there but **bills to the pay-as-you-go balance, not the Go plan** — the preset docs
  and the wizard say so. It advertises **`jev-1.13` only**, which rejects box questions.

No TypeSafe-direct preset in 0.3. Jev is reachable through OpenRouter and OpenCode; a direct preset is
added only after someone verifies `api.typesafe.ai/v1/systemone` live with a key and records the
fixture.

Discovery uses the provider's models endpoint (`GET /v1/models` on TypeSafe, the preset's models URL
elsewhere) and falls back to the catalog. An alias is displayable; a **profile pins an exact
version**, and an alias (`~typesafe/jev-latest`, anything ending in `latest`) is refused at profile
validation with the resolved version named.

## 4. The client: one interface, two transports

```csharp
public interface IDecisionClient
{
    Task<DecisionResult> GetDecisionAsync(
        DecisionRequest request, DecisionOptions? options = null, CancellationToken ct = default);
    object? GetService(Type serviceType);
}
```

- `DecisionRequest` carries the state, the questions and the model id; `DecisionOptions` carries a
  model override, a timeout and a request context (`SessionId`, `RunId`) for headers.
- `DelegatingDecisionClient` forwards and is the base for middleware (usage recording, redaction,
  higher-level retries).
- `DecisionClientBuilder` assembles adapter or native; `UseOpenTelemetry()` follows MEAI's naming and
  tags model, family, question count and outcome — never state or question text.
- The **adapter**: `ChatDecisionClient` implements `IDecisionClient` over an `IChatClient`, asking for
  decision JSON and validating it with the same validator. `box` is offered only on a vision-capable
  chat model; otherwise it fails with "this model cannot decide" naming the missing capability.
- The **native transport**: `SystemOneDecisionClient` implements `IDecisionClient`. It is not an
  `IChatClient`; a `SystemOneChatFacade : IChatClient` exists only so the 0.2 resolution seam keeps
  working — chat calls on it throw "model X is a decision model; it cannot chat", and `GetService`
  returns the decision client.
- The decision result carries the chosen answer, the full distribution, **confidence** (below), the
  resolved model id, the method (`native`/`adapter`) and usage. The public `Lunate.Contracts` result is
  mapped at the agent boundary and only changes in the 1.16.0 bump (§9).

## 5. Confidence: computed uniformly, used for routing

Native answers carry `confidence`, but it must be **one definition** across native and adapter, so the
layer computes it from the distribution with TypeSafe's published formulas (a test pins each against
the documented examples):

- **noul**: `|2p − 1|`, where `p` is the yes probability (0 at p=0.5, 1 at p=0 or 1).
- **choice** with `n` options and top probability `p_max`: `(p_max − 1/n) / (1 − 1/n)`.
- **score** with `n` ordered levels, most likely level `m`:
  `max(0, 1 − Σ_i p_i·|i − m| / MAD_unif)`, `MAD_unif = (1/n)·Σ_i |i − (n−1)/2|`.
- **box**: the provider's own `confidence` (there is no distribution to derive it from).

The computed value — not the provider's field where they differ — is recorded in provenance next to
the distribution, for native and adapter results alike.

**Routing gates on confidence.** A routing decision below `decisions.routingMinConfidence` (default
`0.5`) does not take the argmax: the configured chat profile stays in force. For the single-choice
strategy the gate applies to the chosen option's confidence; for the per-profile noul strategy it
applies to the winner's margin over the runner-up (§7). The transcript records the distribution, the
gate value and the outcome, so a wrong route is explainable.

**Approvals stay on the existing threshold.** Approval gates are binary; the existing probability
threshold `p ≥ t` maps exactly to `confidence ≥ 2t − 1` for `t ≥ 0.5`, so the shipped approval
behaviour is unchanged. The docs show the mapping and recommend expressing new gates in confidence.

## 6. The approval gate: several atomic questions, one request

The single approve/deny question becomes **four atomic noul questions over one structured state**, sent
in one request (the model evaluates them in parallel):

- `writes_outside_workspace` — does the call modify anything outside the workspace root?
- `network_access` — does it reach the network?
- `irreversible_deletion` — does it delete or overwrite something that cannot be recovered?
- `credential_exposure` — does it read, print or transmit a credential?

The state holds only what the questions need, in named fields: `tool`, `arguments`, `workspace_root`,
`risk_tier`, and `untrusted_content` for anything that came from the model or a file. Untrusted
content never goes into the question text or criteria — it is referenced by field name, which also
keeps the questions short and literal (jev-1.13 reads what is written, not what is meant).

Code combines the four answers into the escalate decision against **one threshold**,
`decisions.approvalsThreshold`, expressed in confidence; its default maps the existing probability
threshold `t` to `2t − 1`, so shipped behaviour is unchanged. Any of the four signals at or above the
threshold escalates. Shadow mode records **every** atomic answer, so an escalation can be traced to
the signal that drove it. If one signal later proves to need its own threshold, that is a follow-up
with data behind it, not a design default. The profile carries no per-signal threshold and no
risk-tier table. A decision can only tighten the gate; no answer here approves a tool call.

## 7. Routing options carry their own descriptions

Each profile in `decisions.routingProfiles` gets a short user-written `description` — what it is for,
what it is not, and one or two examples — sent as that option's `criteria`. A profile without a
description is still allowed, with a one-time transcript warning that routing quality depends on it.
The wizard (#475) asks for the description when routing is enabled; a custom provider's profile can
inherit the provider's description.

Option order is not meaningful to the model, and jev-1.13 leans toward the first option: sorting by
name only moves the bias to the alphabetically first profile, so it does not solve it. The router has
two strategies, chosen by the decision model's catalog entry:

- **Default** — one `choice` question over the routing profiles, as before.
- **`choiceOrderSensitive` (jev-1.13)** — one `noul` question per routing profile in the same request,
  each asking "does this request fit <profile>: <description>?"; code picks the highest probability.
  The confidence gate applies to the winner's margin over the runner-up
  (`p_max − p_second ≥ decisions.routingMinConfidence`), so a runaway winner passes and a close call
  keeps the configured chat profile.

The provenance records which strategy ran, with the distribution or the two leading probabilities
behind the decision. Both paths are tested: the single-choice path (including a below-threshold
result) and the noul-per-profile path (including a close call that keeps the chat profile), with the
strategy asserted in provenance.

## 8. State size and request limits

- **Keep states small and structured.** The default per-question context is **32k tokens** (the
  per-request context is 64k); the state counts against both. The transcript warns when a state is
  estimated above ~28k tokens (~4 chars/token), because beyond the default budget the provider's
  long-context mode is slower and less accurate. Long context is never switched on implicitly: the
  `TypeSafe-Danger-Zone-Long-Context` header is opt-in per provider, and the warning says so.
- The tool-result consumer sends **head and tail**, never a whole output, and says in the state that
  it was truncated.
- Request rules, enforced before the call where cheap: UTF-8 JSON, at most **32 MiB** body (413),
  at most **64** nesting levels in `state`/`instructions`/`criteria` (flatten deeper), finite numbers
  only, and identifiers or exact large quantities sent as strings.
- `Content-Encoding: gzip` above 64 KiB of body, one layer only.
- `429`/`529` retried with exponential backoff and jitter, honouring `retry-after`, at most three
  attempts; other 4xx are not retried.
- `422` surfaces the offending field name; `400 max_tokens_exceeded` maps to "state too large for this
  model"; `413` to "state exceeds the 32 MiB request limit".

## 9. Contract 1.16.0 (lockstep minor)

Additive on the stable contract:

1. `DecisionQuestionKind.Box`.
2. `DecisionAnswer.Box` — `DecisionBox(Top, Bottom, Left, Right, Confidence)`; `Answer` is empty and
   `Distribution` empty for a box answer. `Probability` carries the confidence so existing consumers
   that read a single number keep working.
3. `DecisionResult.Confidence` — the computed confidence from §5, so extensions see the same number the
   router and the gate use.
4. Image state validation: a state containing an image part is accepted only when the resolved model's
   input modalities include images; otherwise the request fails before any call, naming the model and
   the missing capability.
5. `INativeDecisionClient` stays and forwards to `IDecisionClient`, so a consumer built against 1.15.0
   still works.

Validator additions: box fractions are finite in [0,1], `top < bottom`, `left < right`, confidence in
[0,1]; a box answer on a non-box question (or vice versa) is off-contract; a distribution that does
not sum to 1 within tolerance is off-contract.

## 10. System One transport details

- Body: `model`, `state` (string, object, array or parts, passed through), `questions` mapped per kind
  — `score.criteria` array, `choice`/`noul` records.
- Response mapping: `noul` → yes/no from `p ≥ 0.5` with `{yes: p, no: 1−p}`; `choice` → `choice` plus
  `probabilities`; `score` → the highest-probability level's `legend` as the answer, the distribution
  keyed by level, and the expected `score` recorded as a number; `box` → the four fractions plus the
  provider confidence.
- A deterministic 4xx raises a typed `DecisionTransportException` carrying the provider's message and,
  for 422, the field — no retry, no fabricated answer.
- Images: `imageHeaders` from the preset are added only when the state contains an image part.
- Fixtures are replayed per provider (success and error shapes) and validated against the stored
  OpenAPI snapshot in a contract test.

## 11. jev-1.13 jaggedness, as preset data or doc notes

Read from TypeSafe's page (last reviewed 2026-10-02) and folded in where it changes our shapes:

- **Literal reading** — instructions state the exact condition; boundary cases live in criteria; when
  interpretation is unavoidable, two literal questions combined in code.
- **No counting, arithmetic or date maths** — those stay in code. Extraction becomes a choice over
  enumerated options (months, days), with an explicit "not stated" option.
- **Indirection** — reduce hops; questions name state fields.
- **Large/irrelevant state** — filtered before sending; the warning in §8 is the guard.
- **Adversarial content** — untrusted content is a separate named field (§6), never spliced into
  instructions; criteria are explicit.
- **Contradictory instructions/criteria** — criteria extend the instruction instead of restating it;
  a test rejects a noul whose `true` criterion reads negative.
- **Choice order bias** — jev-1.13 leans toward the first option; the router switches to one noul per
  profile when the catalog entry sets `choiceOrderSensitive: true` (§7), rather than pretending that
  sorting removes the bias.
- **No generation** — the layer never asks Jev to write text; open-ended output stays with the chat
  model.

The adapter prompt gets the same treatment: atomic questions, structured state, no arithmetic, and low
confidence means "unknown", not "no".

## 12. The decide tool's description

The `decide` tool's description teaches the chat model the same rules, because the model writes the
questions:

> Ask several atomic yes/no, choice, score or box questions about one small structured state. Prefer
> many narrow questions in one call over one broad question; put the data in named state fields and
> refer to them by name, never splice untrusted content into the question. Do not ask the model to
> count, compute or compare dates — do that in code. Act on confident answers (≥ 0.5 unless the
> profile says otherwise); treat low confidence as "unknown", not as "no".

## 13. Tests

- Fixture replay for both verified providers: noul/choice/score, box, both error shapes, OpenCode's
  `usage.opencode`, and the OpenCode image header being added only when images are present.
- Contract test: every recorded fixture validates against `fixtures/typesafe-openapi.json`; a schema
  hash mismatch fails loudly with the diff.
- Confidence: the three formulas against the documented examples (choice 3-option, score
  `(0, 0.57, 0.43) → 0.35`, noul 0.5/0/1), and native-vs-adapter equality for one distribution.
- Routing: both strategies — the single choice question by default, one noul per profile when the
  catalog entry is `choiceOrderSensitive`; below-threshold keeps the chat profile (chosen-option
  confidence, or winner margin for noul), at/above takes the winner; provenance names the strategy.
- Approval gate: four atomic questions in one request against the single `approvalsThreshold`; every
  answer recorded in shadow; an untrusted string in state never appears in any instruction or
  criteria; escalation driven by each single signal in turn.
- Limits: 32 MiB/413, 64 nesting, non-finite number, gzip above the threshold, 429/529 backoff with
  `retry-after`, 422 field surfacing, `max_tokens_exceeded` message.
- Decision layer: the existing suite runs with a native profile in place of the adapter; provenance
  reads `native`; a chat usage on the systemone profile is refused naming the family; an alias profile
  is refused naming the resolved version.
- Layering: the project graph above, plus `Lunate.Contracts` references neither new project.
- Cost: provider-reported cost recorded; absent cost falls back to the catalog estimate.
- Live smoke: OpenRouter and OpenCode, skipped without a key.

## 14. Risks

- **OpenCode serves only `jev-1.13`**, and 1.14 returns "unavailable". Profiles pin 1.13 there; box is
  refused with a message naming the version.
- **Box and images are experimental** at the provider (the header says so). They sit behind the
  capability check and are fixture-tested, not relied on by default decisions.
- **TypeSafe direct is deferred.** It is not a 0.3 preset: unverified endpoints do not ship.
- **The 32k budget is tokens, not bytes.** The warning uses a chars/4 estimate and says so; a state
  near the boundary should be measured, not guessed.
- The per-kind criteria asymmetry is a provider contract quirk; the client owns it in one mapper with
  a fixture per kind so a future provider change fails a test, not a user's decision.
