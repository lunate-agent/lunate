# Tasks: skills-description-spec-limit

## 1. Implementation

- [ ] 1.1 Scanner validation: a `SKILL.md` description longer than `SkillIndex.MaxDescriptionLength` (1,024) is invalid — the reason names the limit and the actual length, warns once per session, is not loaded, not indexed and leaves the read allowance

## 2. Tests

- [ ] 2.1 Rewrite `SkillIndexBudgetTests`/`SkillIndexTests` tests pinning the trim/200 behaviour to the 1,024 whole-carry (sentence trim, abbreviation, 200 cap, surrogate slice) — each fails before the scanner/spec alignment and passes after; never delete or loosen
- [ ] 2.2 Add a scanner-level test for a description over 1,024 characters: invalid, warned once, no read root, `/skills` view shows `(invalid)` with the reason's first sentence
- [ ] 2.3 Keep the soft-notice and hard-ceiling tests on a synthetic pack and a small window; add the test against the real `.agents/skills`: all skills indexed whole, no notice on a 128,000-token window

## 3. Verification

- [ ] 3.1 `docs/guide.md` and any other `docs/` wording of the description trim/200 cap updated to the 1,024-spec-limit wording
- [ ] 3.2 Gates: `dotnet csharpier format .` on edited files, `openspec validate skills-description-spec-limit --strict`, `dotnet build Lunate.slnx -c Release`, `dotnet test tests/Lunate.Coding.Tests -c Release`, `bash scripts/check-openspec-skills.sh`
- [ ] 3.3 PR #633 updated (body + findings comment); the hand-off's remaining-work list marked resolved or its gaps stated
