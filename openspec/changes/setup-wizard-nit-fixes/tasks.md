# Tasks — setup-wizard-nit-fixes

Four fixes, four groups, one branch; each group is a red-green cycle plus the tests that fail before
it. The archive commit stays last (design D1-D4).

## 1. The credential answer's text

- [ ] 1.1 Test-first: `SetupWizardModelTests` renders a `WizardCredentialChoice` holding a typed key and
      asserts its `ToString()` names the reference, marks the key redacted, and does not contain the key
- [ ] 1.2 Override `WizardCredentialChoice.ToString()` so a positional record's generated text can no
      longer print the key

## 2. The usage line lists `off`

- [ ] 2.1 Test-first: `SetupCommandTests` asserts the `lunate setup` usage text lists
      `--thinking <off|low|medium|high>`, the same four words the unknown-level refusal names
- [ ] 2.2 Correct the `SetupCommand.Usage` constant to the `agent-config` spec's value list

## 3. A chat profile named `decisions`

- [ ] 3.1 Test-first: an interactive walk whose chat profile is named `decisions` reaches the decision
      step, names the clash, stages no decision profile, and the confirmed commit keeps
      `profiles.decisions` as the chat profile with `usages.chat` bound to it and no `decisions.profile`
- [ ] 3.2 Name the decision profile once (`SetupWizardModel.DecisionProfileName`) and guard the decision
      step on it: skip with one notice instead of letting the commit replace the chat profile

## 4. The model-check failure detail

- [ ] 4.1 Test-first: `SetupCommandTests` runs `lunate setup` with a rejecting chat client and asserts the
      summary's model-check line names the model id, the API family and the status, with no credential
      text; the interactive notice test is updated to the same words
- [ ] 4.2 `WizardModelCheck.RunAsync` records `<model> on family <family>: <status or shape>` (credential
      redacted) and `InteractiveSession.SetupWizard` prints that detail unchanged

## 5. Gate

- [ ] 5.1 `dotnet csharpier format .` after the last edit, then `dotnet csharpier check .` green and
      `git status --short` empty
- [ ] 5.2 Release build of `Lunate.Coding` and the focused `Lunate.Coding.Tests` classes green
- [ ] 5.3 `openspec validate setup-wizard-nit-fixes --type change --strict`, then
      `openspec validate --all --strict` and `bash scripts/check-openspec-skills.sh`
- [ ] 5.4 Docs where behaviour is described (`docs/first-run.md`) still match; archive as the last commit
      (`openspec archive setup-wizard-nit-fixes --yes`)
