# Design: skills-over-limit-description-loads

Refs #631 (follow-up on the #633 sign-off; PR #641). Decision A and check 1 from the maintainer's work order.

## Context

The archived `skills-description-spec-limit` change made an over-1,024-character description an *invalid skill*: `SkillScanner.DirectoryStep` sets an invalid reason, the skill is not loaded, absent from the index and no read root. ADR-0032 recorded that as Decision 2. The maintainer's work order supersedes it — a long description must never stop a skill loading — because the description is operational metadata, not a load gate: the spec limit bounds the *index line*, not the skill.

The index side already does the right thing: `SkillIndex.TruncateDescription` carries the first `MaxDescriptionLength` (1,024) code units, surrogate-safe, with a trailing `…`, and `SkillIndexBudgetTests`/`SkillIndexTests` pin that slice. So the code change is the scanner: no invalid reason, one warning instead.

Check 1: generic print-mode tests assert stdout purity for other notices, and `SkillsPrintModeTests.The_skills_notice_prints_to_stderr_and_stdout_stays_byte_pure` covers the soft notice, but nothing pins the skills notice *and* the new truncation warning together as stderr-only with a pure stdout. The work order asks for a class named after what it tests.

## Goals

- A description over 1,024 characters loads normally: valid descriptor, indexed (first 1,024 code units plus `…`), read allowance, listed by `/skills`.
- Exactly one discovery warning per such skill, in the required shape: `skills: <name> description exceeds the 1024-char spec limit; truncated in the index`.
- ADR-0032 states the load-truncate-warn semantics instead of invalidation.
- A skills-specific print-mode test asserting both lines on stderr and stdout unchanged.

## Non-Goals

- No change to the 1,024 slice, the surrogate-safe boundary, per-extension budgets or `skills.indexBudget`.
- No change to how malformed frontmatter, name mismatches or non-conforming names invalidate a skill.
- No diagram: the change stays inside `Lunate.Coding`'s scanner (no boundary, dependency or data-flow change), so a C4 view would add nothing (c4-diagrams skill, level selection).

## Decisions

1. **Warn in `DirectoryStep`, not in composition.** Discovery already owns once-per-session warnings for malformed skills, collisions and bounds, so the warning belongs beside them and reaches both the interactive session and print mode through `SkillCatalog.Warnings` — no new plumbing. Alternative — warn inside `SkillIndex.TruncateDescription` — would re-warn per composition and cannot name "discovery". The warning names the skill and the limit in the shape the work order fixes (`1024-char`, no thousands separator, so no culture risk).
2. **Keep the descriptor's description whole; truncate only in the index line.** `SkillDescriptor.Description` stays the declared text (the `/skills` view and invocation read it), and `SkillIndex.Line` slices for the index. This keeps one definition of the spec limit (`SkillIndex.MaxDescriptionLength`) and keeps the truncation a presentation concern.
3. **Amend ADR-0032 in place rather than supersede it.** ADR-0032 is an addendum that is still in force; only its Decision 2 is now wrong, and the maintainer's order says to update it to say exactly this. Rewriting the decision keeps one document for the description-limit semantics; a superseding ADR would leave a wrong in-force record beside a correct new one.
4. **New test class `SkillsPrintModeNoticeTests` for check 1.** `SkillsPrintModeTests` already carries the behavioural soft-notice/ceiling tests; a separate class named after stderr/stdout purity keeps the check discoverable and lets the existing class stay about composition.

## Risks / Trade-offs

- [A pack of over-limit descriptions grows the index.] -> bounded at 1,024 characters per line and by the ADR-0031 budgets; the truncation warning tells the user what happened.
- [The warning repeats for each over-limit skill.] -> one line per skill is the actionable granularity (the name is in the line); discovery's other warnings are also per-entry.
- [Renaming/rewriting the pinning test could hide a regression.] -> the test is rewritten to the new behaviour, not deleted or loosened, and keeps its archived scenario name in the spec.

## Migration Plan

Spec delta first, failing test, then the scanner change and the ADR amendment, then docs; archive last. Rollback is the single scanner hunk.

## Open Questions

None — the work order fixes behaviour, warning shape and the ADR target. The `grill-me` rule's interview was resolved against the work order and the codebase (unattended run, no one to ask).
