## 1. Prove the symptom and pin the new sequence

- [ ] 1.1 Add a `SetupWizardEntryMenuTests` driver test that edits the decisions layer, changes the approval gate and then the decide tool in one pass, requires the list to return after the first answer, leaves through Done, and asserts both settings in the summary and the written settings file.
- [ ] 1.2 Run that test before the fix and record that it fails because the step leaves for the summary after the first answer.
- [ ] 1.3 Update the existing decisions-step tests that assumed the step closes after one answer (the one-pass edit test, the routing cases and the defaults case) so they choose Done explicitly.

## 2. Implement the repeated list

- [ ] 2.1 In `InteractiveSession.SetupWizard.cs`, loop the decisions list after every answered switch, keeping the collected switches and routing descriptions in the pass.
- [ ] 2.2 Add the first-row **Done — leave the decisions settings** entry; on Done stage the collected answers, or the defaults when no switch differs.
- [ ] 2.3 Keep the routing behaviour: re-answering routing replaces its profiles and descriptions; turning it off clears them.

## 3. Documentation and verification

- [ ] 3.1 Update `docs/first-run.md` to say the decisions list stays open until Done.
- [ ] 3.2 Run the changed test classes by filter and the Release build; run CSharpier after the last edit.
- [ ] 3.3 Run `openspec validate decision-settings-stay-open --strict` and `openspec validate --all --strict`.
- [ ] 3.4 Run `bash scripts/check-openspec-skills.sh` and archive the change as the final commit.
