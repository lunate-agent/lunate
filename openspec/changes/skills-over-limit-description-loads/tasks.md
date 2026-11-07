## 1. Implementation

- [x] 1.1 `SkillScanner.DirectoryStep`: an over-1,024-character description loads normally (valid candidate, indexed, read root) and emits one warning `skills: <name> description exceeds the 1024-char spec limit; truncated in the index`; the invalid-skill rule and its reason are gone
- [x] 1.2 `adr/0032-addendum-skill-description-spec-limit.md`: Decision 2 records load-truncate-warn instead of the invalid-skill semantics; Context records what the decision said before the amendment, and the consequences follow
- [x] 1.3 Check the `docs/` wording for the invalid-skill claim and update any occurrence to load-truncate-warn

## 2. Tests

- [x] 2.1 Rewrite the over-limit scanner test to the load-truncate-warn behaviour as `A_description_over_the_spec_limit_still_loads_and_warns_once` (valid, warning shape, read root); red before 1.1, green after
- [x] 2.2 Keep `A_description_at_the_spec_limit_is_valid` green: the limit itself never warns
- [x] 2.3 Add `SkillsPrintModeNoticeTests`: a print run whose pack is above the soft threshold and includes an over-limit description writes the skills notice and the truncation warning to stderr as `lunate: skills: …` lines, and stdout carries only the final answer (no skills line)

## 3. Verification

- [x] 3.1 `dotnet build Lunate.slnx -c Release`, `dotnet test tests/Lunate.Coding.Tests -c Release --filter "FullyQualifiedName~SkillScanner|FullyQualifiedName~SkillsPrintMode|FullyQualifiedName~SkillIndex"`, `dotnet csharpier check .`
- [x] 3.2 `openspec validate skills-over-limit-description-loads --strict` and `bash scripts/check-openspec-skills.sh`
- [ ] 3.3 PR #641 updated: body current, and one comment answering decision A and check 1 by number with `file:line`/test-name evidence
