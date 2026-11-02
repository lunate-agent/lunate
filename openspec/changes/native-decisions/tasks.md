# Tasks

## 1. Protocol verification (started — fixtures recorded)

- [x] OpenRouter SystemOne: noul/choice/score, box with an image, unknown type, unknown model — live,
      recorded in `fixtures/`.
- [x] OpenCode SystemOne: noul, image header requirement, 1.14 unavailable — live, recorded.
- [ ] TypeSafe direct `api.typesafe.ai/v1/systemone`: probe with a key and record the fixture; if the
      shape differs, add a preset variant, not a family.
- [ ] Rate limits and retry semantics: probe for headers or documented limits; record what is absent.

## 2. Contract 1.16.0 (lockstep)

- [ ] `DecisionQuestionKind.Box`; `DecisionAnswer.Box` (`DecisionBox(Top, Bottom, Left, Right,
      Confidence)`); box validation (fractions in [0,1], `top<bottom`, `left<right`, confidence in
      [0,1], wrong-kind rejected).
- [ ] Image state validation gated by the model's input modalities, refusal naming the capability.
- [ ] `INativeDecisionTransport` + `NativeDecisionResult(Text, DecisionUsage?)`; `INativeDecisionClient`
      forwards so 1.15.0 consumers keep working.
- [ ] Version bump and contract tests at the new version.

## 3. System One client

- [ ] `SystemOneDecisionClient`: request mapping per kind (score criteria array, choice/noul records),
      state pass-through (string, object, parts), response mapping per kind (noul complement,
      choice probabilities, score legend, box fractions), `results` JSON for the existing validator.
- [ ] Typed `DecisionTransportException` for deterministic 4xx with the provider's message; no retry.
- [ ] Image-only headers from the preset (`TypeSafe-Danger-Zone-Images` for OpenCode) applied only
      when the request has image parts.
- [ ] `SystemOneChatFacade : IChatClient`: chat calls fail with "model X is a decision model"; its
      `GetService` returns the native transport, preserving the 0.2 seam.
- [ ] `HttpClient` with the shared header/credential handlers from slices 2 of #474.

## 4. Presets, discovery, profiles

- [ ] `openrouter` gains `systemone: https://openrouter.ai/api/v1/systemone`.
- [ ] `opencode` gains `systemone: https://opencode.ai/inference/systemone/v1/systemone` and the
      image header, documented as Console/Zen (not Go).
- [ ] New `typesafe` preset (`api.typesafe.ai/v1/systemone`, bearer).
- [ ] Catalog entries for `typesafe/jev-1.14` (OpenRouter), `jev-1.13` (OpenCode), TypeSafe direct;
      family `systemone`; context/limits from the model pages.
- [ ] Discovery marks decision models when the provider advertises them; catalog otherwise.
- [ ] Decision usage accepts a `systemone` profile; chat usage refuses it naming the family.
- [ ] Alias profiles refused, naming the alias and the exact version it resolves to.

## 5. Cost

- [ ] Record `usage` (`input_tokens`, `output_tokens`, `cost`) from the response, including the
      OpenCode `usage.opencode` block; mark provider-reported.
- [ ] `/cost`: decision usage line with model, method label and amount; catalog estimate only when the
      provider omits cost.

## 6. Adapter box

- [ ] Adapter schema for box (four fractions + confidence) validated by the shared validator.
- [ ] Offered only on vision-capable chat models; otherwise "this model cannot decide" names the
      missing capability.

## 7. Docs

- [ ] `docs/configuration.md`: `systemone` family, presets, image header, alias rule, cost reporting.
- [ ] `docs/guide.md`: a decision profile example and the `jev-router` chat example (no code).
- [ ] ADR-0030 accepted.

## 8. Proof

- [ ] Fixture replay per provider (success + error shapes), header-only-with-images, `usage.opencode`.
- [ ] Existing decision-layer tests green with a native profile replacing the adapter.
- [ ] Live smoke: OpenRouter and OpenCode (skip without a key); TypeSafe once a key exists.
- [ ] `verify.sh` green and PR CI green.
