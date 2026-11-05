# Native decisions: the System One family behind the decision layer

## Problem

The decision layer already has a native seam — `DecisionEngine` asks the model's client for an
`INativeDecisionClient` and labels provenance `native` when it finds one — and **no provider
implements it**. 0.2 shipped the adapter path only: a chat model is asked to return decision JSON.
That is the layer's design half-finished, not a new feature: ADR-0027 and ADR-0028 define a decision
primitive whose native path is "the provider offers a decision API". TypeSafe's System One is that
API, and OpenRouter, OpenCode and TypeSafe itself serve it.

The chat provider model cannot express it: a System One call is `state` + typed `questions` in, typed
`answers` + probabilities out, with no messages, no streaming and no generated token. Any of the
chat families would misrepresent it.

## What changes

1. **A fifth provider family, `systemone`.** It joins `openai-chat`, `openai-responses`,
   `anthropic-messages` and `google` in the provider model (#474). A model carries the family;
   discovery marks it; a profile pins it.
2. **A native decision transport**, not an `IChatClient`. `SystemOneDecisionClient` implements the
   decision layer's native seam, returns raw `answers` JSON for the existing validator, and reports
   the provider's `usage` so `/cost` can show decision cost.
3. **Presets** carry the SystemOne URL: OpenRouter (`/api/v1/systemone`), OpenCode Console/Zen
   (`/inference/systemone/v1/systemone`; Console/Zen, and a Go key there bills to the pay-as-you-go
   balance, not the Go plan). Both share one client; the small per-provider differences (score
   criteria shape, image header, model ids) are data in the preset, not forks. No TypeSafe-direct
   preset ships in 0.3 — an unverified endpoint does not get a preset.
4. **Contract minor bump to 1.16.0** (lockstep): a fourth question kind, **box** (instructions in;
   `top`/`bottom`/`left`/`right` fractions + confidence out), and **image parts in the state**, gated
   by the model's input-modality capability. The adapter path supports box only on a vision-capable
   chat model; otherwise the decision fails naming the missing capability.
5. **Native provenance completes the routing rule**: a `systemone` profile is accepted by the
   decision usage as `native`, and the same profile used as a chat profile is refused by capability.
6. **The layer is split by concern, MEAI-shaped** (before any System One code): `Lunate.Ai.Decisions`
   holds `IDecisionClient` (delegating client, builder, OpenTelemetry) and the chat adapter;
   `Lunate.Ai.SystemOne` holds the vendor transport; `Lunate.Agent` depends only on the interface;
   `Lunate.Contracts` is unchanged until the 1.16.0 bump. Neither new project is packed.
7. **Confidence is one definition** computed from the distribution (noul/choice/score formulas), used
   by routing — below `decisions.routingMinConfidence` (default 0.5) the configured chat profile stays
   — and exposed at 1.16.0. The single `choice` route switches to one noul per profile on a
   `choiceOrderSensitive` model (jev-1.13), so a positional bias cannot decide it. Approvals use one
   `decisions.approvalsThreshold` in confidence (`2t − 1` of the old probability value).
8. **The approval gate asks four atomic questions in one request** (writes outside the workspace,
   network, irreversible deletion, credential exposure) over a structured state with untrusted content
   in its own field; shadow records every answer, and no answer approves a tool call.
9. **States stay small**: warn near the 32k-token per-question budget, never enable long context
   implicitly, send tool results as head and tail; the client honours the provider's request limits
   and validates its fixtures against the published OpenAPI.

Jev Router (`typesafe/jev-router`) is not part of this: it is an ordinary chat model on OpenRouter
that works through the `openrouter` chat preset today; it is documented as an example only.

## Verified before design

Nine live calls recorded as fixtures in `fixtures/` (provenance in `fixtures/README.md`): the request
shape, all four question kinds, the response and `usage` shape on OpenRouter; the OpenCode variant
with its `usage.opencode` block; the error shapes. Two provider differences found and designed for:
`score.criteria` is an **array** while `choice.criteria` is an **object**; and OpenCode requires the
header `TypeSafe-Danger-Zone-Images: true` for image content. TypeSafe direct is **not** verified live
(no key) and is therefore out of 0.3; Jev stays reachable through OpenRouter and OpenCode.

## Scope

In: the family, the transport, presets, discovery marking, profile pinning, box + image contract,
decision cost, fixtures, live smoke and the decision layer's existing tests running with a native
profile.

Out: `typesafe/jev-router` code (docs only), OpenRouter's `/api/alpha/decisions` alpha surface, any
change to the four chat families' clients, and the chat provider slices (#484 and the four after it).

## Impact

- Specs: `decision-layer` only. The provider-side delta (`systemone` in the family list, preset URLs)
  lands with the implementation PR, rebased on `provider-api-families` once its slices are applied —
  writing it now would collide with a change that is still in flight.
- ADR: `adr/0030-native-decisions.md`.
- Contract: `Lunate.Contracts` 1.16.0 (box kind, image state validation, native usage).
- Cost: provider-reported `usage.cost` recorded under the decision usage.
