# ADR-0030: Native decisions are a provider family, not a chat model

Status: accepted — 2026-11-03
Completes: ADR-0027 (provider store) and ADR-0028 (decision layer); builds on ADR-0029

## Context

ADR-0028's decision layer has two paths: an **adapter** that asks a chat model for decision JSON, and
a **native** path for providers that offer a decision API. 0.2 shipped the adapter and the native seam
(`INativeDecisionClient` read from the model's client) with no implementation behind it. TypeSafe's
System One is the decision API the primitive was modelled on, and it is served by OpenRouter, OpenCode
and TypeSafe itself.

A System One exchange is not a chat exchange: `state` plus typed `questions` in, typed `answers` with
probabilities out, no messages, no generated token, no streaming. It cannot be expressed by any of the
four chat families without lying about the wire contract, and the protocol has real per-provider
differences (verified live, fixtures in `openspec/changes/native-decisions/fixtures/`): score criteria
are an array while choice criteria are a record, OpenCode needs an image header, and model ids differ.

## Decision

1. Add a fifth provider family, **`systemone`**, to the provider model. A provider entry lists its
   SystemOne base URL per family; a model carries the family; discovery or the catalog supplies it.
2. Serve it with a **native decision transport**, not an `IChatClient`. The transport implements the
   decision layer's native seam, returns raw `answers` JSON to the existing validator, and reports
   provider `usage`. A thin `IChatClient` facade fails chat calls by name so the 0.2 seam and its
   provenance are untouched.
3. Keep provider differences as **preset data**: criteria shape overrides, image-only headers, model
   ids. One client, one mapper, fixture per provider. Two presets ship in 0.3 — OpenRouter
   (`jev-1.14`) and OpenCode Console/Zen (`jev-1.13`, no box questions). A Go key on the OpenCode
   systemone URL bills to the pay-as-you-go balance, not the Go plan. TypeSafe direct is deferred
   until a live probe with a key exists; no unverified preset ships.
4. Extend the decision contract **minor to 1.16.0**: a fourth kind, box (four fractions + confidence),
   and image parts in the state, gated by the model's input modalities. The adapter supports box only
   on vision-capable chat models.
5. **Profiles pin an exact version.** Aliases resolve at discovery only; a profile that names one is
   refused, because an alias upgrade can change probabilities.
6. **Cost is real.** The provider's `usage.cost` is recorded under the decision usage and preferred
   over a catalog estimate in `/cost`.
7. Jev Router on OpenRouter is an ordinary chat model; it stays a documented example.

## Consequences

- A decision profile on a decision model records provenance `native`; a chat profile on the same model
  is refused by capability instead of failing at request time.
- The `score`/`choice` criteria asymmetry and the OpenCode image header are encoded once and guarded by
  fixtures; a provider change fails a test.
- The contract bump is additive: 1.15.0 consumers keep compiling and the old native method forwards.
- One more package is **not** introduced: the transport is plain HTTP through the shared header and
  credential handlers.
- OpenCode serves only `jev-1.13` today; the preset advertises only what it serves.

## Alternatives rejected

- **Route System One through the `openai-chat` family with a custom adapter.** It would fake messages
  and streaming the API does not have, and every consumer would see a chat-shaped lie.
- **Build on OpenRouter's `/api/alpha/decisions`.** It is an alpha surface with its own shape; the
  System One API is the cross-provider contract the SDKs target.
- **One family per provider.** Three families would multiply the profile validation, the wizard and
  the preset audit for differences that are data, not protocol.
- **Let profiles use the `jev-latest` alias.** Convenient, and wrong: upgrades silently change
  probabilities, which is exactly what a pinned decision profile must not allow.

## Addendum — 2026-11-03: the MEAI swap path

The client surface is deliberately shaped like `Microsoft.Extensions.AI` — `IDecisionClient` with
`GetDecisionAsync(request, options, ct)`, `GetService`, a delegating client, a builder and
OpenTelemetry — and the internals are split into `Lunate.Ai.Decisions` (the abstraction plus the chat
adapter) and `Lunate.Ai.SystemOne` (the vendor transport), neither packed. If MEAI ships a decision
abstraction, `IDecisionClient` becomes an adapter onto it (or is replaced by it) and System One
implements theirs; the public `Lunate.Contracts` request/result types are revisited only at the next
contract major, so the swap stays mechanical and nothing users depend on moves.
