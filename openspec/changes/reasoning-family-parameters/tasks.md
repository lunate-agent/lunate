# Tasks — reasoning-family-parameters

## 1. Family-driven selection

- [x] 1.1 Add a shared wire-style resolution to `ReasoningParameters`: declared `inline` and
      `null` keep their meaning; otherwise the declared `ApiFamily` decides, then the provider
      preset's single chat family, then the declared `reasoning` style as fallback.
- [x] 1.2 Route `Apply` through the resolution: `anthropic-messages` sends the thinking budget,
      `openai-chat` / `openai-responses` / `google` send the reasoning effort, `off` sends nothing.
- [x] 1.3 Route `Describe` through the same resolution so the `/thinking` notice names what the
      request carries.

## 2. Tests

- [x] 2.1 Move the existing budget cases to an Anthropic model; keep effort, inline, off and
      no-style cases.
- [x] 2.2 Add a misdeclared `budget` model resolved to `openai-chat`: the request carries effort
      and no Anthropic thinking request.
- [x] 2.3 Add a misdeclared `effort` model resolved to `anthropic-messages`: the request carries
      the level's thinking budget.
- [x] 2.4 Add fallback coverage: an unresolved model (no family annotation, unknown provider)
      keeps the declared style; `Describe` follows the family.

## 3. Docs

- [x] 3.1 State in `docs/models-and-providers.md` that the resolved family decides the wire
      parameter and the declared style is metadata/fallback.

## 4. Verify and archive

- [x] 4.1 Run the focused test classes, CSharpier and a Release build; confirm `git status` clean.
- [x] 4.2 Run `openspec validate --all --strict` and `bash scripts/check-openspec-skills.sh`.
- [x] 4.3 Sync the delta into `openspec/specs/ai-layer/spec.md` and archive as the final commit.
