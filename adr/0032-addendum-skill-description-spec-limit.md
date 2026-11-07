# ADR-0032 (addendum): Skill descriptions carry whole to the Agent Skills spec limit

Status: accepted — 2026-11-07
Supersedes: Decision 1's description half of ADR-0031 (addendum, skills index budget) — the index no longer trims a description to its first sentence or caps it at 200 characters. ADR-0031's budget decisions (soft 2%, hard 5%, `skills.indexBudget`, `AGENTS.md` protection) and every other half of ADR-0017 stay in force.

## Context

ADR-0031 kept the index compact per skill by trimming each description to its first sentence and the pre-existing 200-character cap. The description is the trigger the model matches a task against, and the "use when …" clause that decides a load is usually in the second sentence: the trim hid exactly the text a match needs. The Agent Skills specification bounds a description at 1,024 characters; the repository's own 21-skill pack with full descriptions composes to ~1,537 estimated tokens, under the 2% soft threshold of a 128,000-token window, so the whole-carry is affordable without page-budget regressions.

Decision 2 was revised on 2026-11-07, after the #633 sign-off review. It originally made an over-1,024-character description an *invalid skill* — one-time warning, no load, no index line, no read allowance, on the reasoning that a description without its head or tail is no longer the declared trigger. That read the limit as validation of the skill. The review reversed it: a description is operational metadata, not a load gate, and a long one must never stop a skill loading or hide it from the model. The current decision is below; the invalid-skill semantics are superseded and no longer in force.

## Decision

1. **The index carries each description whole up to 1,024 characters** — the Agent Skills specification limit — with no sentence trim; only above that limit is the description sliced at 1,024 code units, surrogate-safe, with a trailing `…`.
2. **The limit is a soft warning, never a hard cap.** A skill whose description exceeds 1,024 characters loads normally — valid, listed by `/skills`, indexed with the first 1,024 characters of its description, with its read allowance — and discovery emits one warning in the shape `skills: <name> description exceeds the 1024-char spec limit; truncated in the index`.

## Consequences

- The soft-notice and hard-ceiling budgets of ADR-0031 remain the degradation path for oversized packs; nothing in the index composition is newly fixed-size.
- A long description costs index space (at most 1,024 characters plus the marker) and one warning line, never a missing skill.
- `docs/` and the `skills` spec's "Model exposure and budget" and "Skill format and validation" requirements name 1,024, not 200, and describe the load-truncate-warn path; sessions whose model window is tighter than the pack see the existing budget machinery, not per-sentence loss.
