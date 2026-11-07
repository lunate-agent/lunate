# Design: skills-budget-advice-and-unknown-window

Refs #631. Follows the PR #633 sign-off review (2026-11-07): two of the three agreed checks were not done, and this change does both.

## Context

The archived `skills-index-context-budget` change shipped a soft notice and a hard-ceiling warning that advise "disable unused skills or packages" — but no mechanism in this repository disables a skill or a package (no command, no setting, no extension API). It also made `SkillsBudget.FromSettings` fall back to `CompactionReducer.DefaultContextWindow` (128,000) when the context window is unknown, so an unknown window silently becomes a 128k-derived 2%/5% budget. A maintainer review on the PR required both corrected:

- the notices may only name a control that exists: `skills.indexBudget` (a positive estimated-token budget or `unlimited`, user scope); no disable phrasing;
- an unknown context window disables the budgets entirely: full index, no notice, no warning, no default-window assumption.

Also required on the same PR: a print-mode test asserting the notices reach stderr and never stdout. `SkillsPrintModeTests` exists on this branch (the review's "no such class" observation is stale — likely read before the rebase landed); its soft-notice test asserted errors but never asserted stdout purity, so the explicit stderr/stdout assertion is added.

## Goals

- `SkillIndex.SoftNotice`/`HardCeilingWarning` and the `/context` segment name `skills.indexBudget` only.
- `SkillsBudget.FromSettings` treats a non-positive window as unknown → `SkillsBudget.Unlimited` (both thresholds `int.MaxValue`); `InteractiveSession.SkillsIndexContextWindow` resolves unknown as `0` instead of inlining `CompactionReducer.DefaultContextWindow`; `PrintMode` already passes the resolved model's window (0 when unknown).
- Tests: red-then-green unit pin for the unknown window, an end-to-end print run over an over-ceiling pack with no resolvable window, stderr/stdout purity, and the notice wording.

## Non-Goals

- No change to the 2%/5% math on a known window, `AGENTS.md` protection, `skills.indexBudget` semantics, or compaction's own 128k default (`CompactionReducer` keeps serving that for compaction — an unknown window there already warns visibly on the log).
- No new disable mechanism for skills or packages (that is a separate feature if ever built).

## Decisions

1. **`skills.indexBudget` is the only named control.** The notices state the setting and what it accepts ("a token budget or 'unlimited'"); the `/context` advice does the same. Alternative — leave a softer "see docs/configuration.md" — says nothing actionable on a terminal, and the review asked for the exact key.
2. **Unknown window = `SkillsBudget.Unlimited`.** `FromSettings` returns unlimited on a non-positive window rather than deriving from a default: a guessed window truncates a complete pack on a model that may have much more room, and the guessed-128k case had already masked over-ceiling packs under a wrong budget. Unlimited is the conservatively honest degradation: everything reaches the model, no claim about the window is made.
3. **Session fallback resolves to `0` (unknown), not to the compaction default.** The session's own precedent for "window unknown" is the `CompactionReducer` log warning — a mechanism that already tells the user the window is unknown. The skills budget just goes unlimited without emitting anything (the spec sets no notice for it); duplicating the compaction warning for skills was declined as noise, and the same caveat as before holds: chat-profile-routed models can see a catalog-declared rather than profile window.

## Risks / Trade-offs

- [A very large pack with an unknown window now composes an unbounded index] -> the pack size is bounded by discovery bounds (256 skills/scope) and the description limit (1,024 chars); a real pathological pack would still fit the core-prompt rule (skills index excluded from that 1,000-token count, its own size a deliberate user choice or `skills.indexBudget` away).
- [Notices' numeric advice changes session-start text] -> the skills prompt-section entry does not embed notice text; only the session log/stderr wording changes, and no golden fixture pins the notice text (grepped).

## Migration Plan

Spec delta → failing tests → code in the same PR; archive lands last.

## Open Questions

None — the review text resolved the semantics directly.
