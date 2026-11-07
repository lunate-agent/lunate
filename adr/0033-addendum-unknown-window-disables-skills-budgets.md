# ADR-0033 (addendum): An unknown context window disables the skills index budgets

Status: accepted — 2026-11-07
Supersedes: the final fallback clause of ADR-0031 (addendum, skills index budget) Decision 2 — the skills index no longer derives its 2%/5% budgets from a 128,000-token default when the model's context window is unknown. ADR-0031's other decisions (soft 2%/hard 5% on a known window, `AGENTS.md` beats the ceiling, `skills.indexBudget` override) and its budget fractions stay in force; ADR-0032 supersedes its Decision 1's description half separately.

## Context

ADR-0031 made `SkillsBudget.FromSettings` fall back to `CompactionReducer.DefaultContextWindow` (128,000) when no window could be resolved, so a run whose model's window is unknown silently received a 128k-derived budget: a pack over 5% of 128k could be cut to a names-only footer although the real model may have far more room — truncation justified by a number nobody declared. Compaction, the other consumer of that fallback, warns visibly when it uses the default; the skills budget degraded silently. The review on PR #633 required the honest degradation instead: unknown window means the budget is disabled.

## Decision

1. **An unknown context window (non-positive, no declared or catalog window) disables both skills-index budgets**: the complete index is sent, no soft notice fires, no hard-ceiling warning fires, and the composition never derives thresholds from a default-window assumption. A `skills.indexBudget` setting overrides regardless — an explicit user value does not depend on knowing the window.
2. **The soft notice, hard-ceiling warning and `/context` advice name only `skills.indexBudget`** (a positive estimated-token budget or `unlimited`, user scope): no "disable unused skills or packages" advice exists, because no command or setting in this repository disables a skill or a package.

## Consequences

- A pathological pack on an unknown-window run composes unbounded in token terms — bounded by the discovery caps (256 skills/scope) and the description limit instead; users who need a cap set `skills.indexBudget`.
- The unknown-window case is the same state compaction warns about; skills add no second notice, they simply stop claiming a budget.
