## Why

The skills index currently treats a description longer than the Agent Skills specification's 1,024-character limit as an *invalid skill*: discovery warns, the skill is not loaded, not indexed and grants no read allowance. The maintainer sign-off on #633 superseded that: a long description must never stop a skill loading. The description is operational metadata, not a load gate, so a soft warning and a truncated index line are the honest degradation — a hard invalidation hides a working skill from the model and the user.

## What Changes

- **A description over 1,024 characters no longer invalidates a skill.** Discovery loads such a skill normally (valid, listed by `/skills`, indexed, read allowance), and carries the first 1,024 characters of the description in the index with the existing trailing `…`.
- **Discovery emits one warning per such skill**, in the shape `skills: <name> description exceeds the 1024-char spec limit; truncated in the index`.
- **ADR-0032 is amended** to record the load-truncate-warn semantics instead of the invalid-skill semantics it currently states.
- **A skills-specific print-mode test** pins that the skills notice and the truncation warning go to stderr while stdout stays a pure answer/event stream (generic print-mode coverage exists; nothing skills-specific did).

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `skills`: "Skill format and validation" — an over-limit description is a soft warning plus an index truncation, not an invalid skill; "Model exposure and budget" — the index truncation is the path a scanned over-limit description takes (no validation rejection ahead of it).
- `print-mode`: "Diagnostics never reach stdout" — a scenario naming the skills notice and the truncation warning on stderr with stdout unchanged.

## Impact

- `src/Lunate.Coding/SkillScanner.cs` — drop the invalid-skill rule for over-limit descriptions; add the truncation warning.
- `adr/0032-addendum-skill-description-spec-limit.md` — Decision 2 rewritten; consequences updated.
- Tests: `tests/Lunate.Coding.Tests/SkillScannerTests.cs` (rewrite the invalid-skill test to load-truncate-warn), `tests/Lunate.Coding.Tests/SkillsPrintModeTests.cs` (new stderr/stdout assertions) or a new `SkillsPrintModeNoticeTests` class.
- Documentation: `openspec/specs/skills/spec.md`, `openspec/specs/print-mode/spec.md` (via deltas); `docs/` wording checked for the invalid-skill claim.
