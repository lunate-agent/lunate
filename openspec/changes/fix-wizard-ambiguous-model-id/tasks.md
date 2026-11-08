## 1. Red: pin the failing behaviour

- [ ] 1.1 Add `SetupWizardAmbiguousModelTests` and prove the reported symptom: a custom-endpoint walk whose discovery listing offers `kimi-k2.7-code` aborts with `setup failed: Model id 'kimi-k2.7-code' is ambiguous` (test fails before the fix).
- [ ] 1.2 Add the single-entry case (a preset whose own catalogue entry answers the bare id) and the several-entry case (the picker offers the qualified ids and the chosen one is written), and the `lunate setup --model` regression; all red before the fix.
- [ ] 1.3 Add a `ModelProfileResolutionTests` case for a profile whose bare model id its own provider entry owns; red before the fix.

## 2. Green: provider-scoped resolution

- [ ] 2.1 Add `ModelCatalog.FindFor(provider, id)` with its candidate list (exact provider id, exact id, single wire-id entry, otherwise candidates) and keep `Find` unchanged.
- [ ] 2.2 Add the wizard's lookup helper that treats a custom endpoint's id as its own catalogue name, and use it in the model step (with the picker when several unrelated entries hold the id).
- [ ] 2.3 Take the same resolution in `WizardModelCheck.ModelFor`, `RunAsync`, `DecisionModelFor`, `SetupCommand`'s family resolution and the `/setup` entry prefill, so no walk step aborts on an ambiguous id.
- [ ] 2.4 Make `ModelProfileResolver` read the profile's model in its provider's scope before the unqualified lookup.
- [ ] 2.5 Run the targeted classes, `dotnet csharpier format .` then `dotnet csharpier check .`, and a Release build.

## 3. Records

- [ ] 3.1 Update the documents the change invalidates (`docs/first-run.md` if the model step's behaviour is described there).
- [ ] 3.2 Run `openspec validate --all --strict` and `bash scripts/check-openspec-skills.sh`; answer the card's acceptance checks by number on the PR.
- [ ] 3.3 Sync the delta specs and archive the change as the final commit of the branch.
