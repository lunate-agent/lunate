# Tasks: skills-budget-advice-and-unknown-window

## 1. Unknown window disables the budgets

- [ ] 1.1 Test-first (unit): `SkillsBudget.FromSettings` with a non-positive window returns `SkillsBudget.Unlimited` (no thresholds) — rewrite the pin that asserted the 128k default; red before, green after
- [ ] 1.2 `InteractiveSession.SkillsIndexContextWindow` resolves unknown as `0` (drop the inline `CompactionReducer.DefaultContextWindow`); `PrintMode` already passes the resolved window (unknown = non-positive)
- [ ] 1.3 Test (end-to-end): a trusted print run whose model declares no context window over a pack far above any default-window ceiling sends the full index with no skills-index notice and no hard-ceiling warning

## 2. Notices name the setting only

- [ ] 2.1 Test-first: the soft notice, the hard-ceiling warning and the `/context` segment contain `skills.indexBudget` and never "Disable unused skills or packages" (or any disable command); unit assertions red before the wording change, green after
- [ ] 2.2 Rewrite `SkillIndex.SoftNotice`/`HardCeilingWarning` text and `InteractiveSession.SkillsContextSegment`'s advice to name `skills.indexBudget` (a token budget or `unlimited`, user scope)

## 3. Print mode: stderr only

- [ ] 3.1 Test (print mode): a run triggering a skills notice — the notice text appears in stderr and stdout stays byte-pure (the scripted response only, no `lunate:` skills lines)

## 4. Gates and closure

- [ ] 4.1 `docs/` sweep: guide/usage/configuration wording that advises disabling unused skills or packages (settings-schema rendering) states the setting instead
- [ ] 4.2 Gates: `dotnet csharpier format .` on edited files, `openspec validate skills-budget-advice-and-unknown-window --strict`, `dotnet build Lunate.slnx -c Release`, `dotnet test tests/Lunate.Coding.Tests -c Release`, `bash scripts/check-openspec-skills.sh`
- [ ] 4.3 PR #633 updated (body + comments); the sign-off's two fixes confirmed and the print-mode stderr-only test recorded; then `openspec archive` as the final commit
