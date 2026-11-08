## Context

`SetupWizardModel` stages exactly one provider, credential, connection test, model, model check,
chat profile and fast flag. The driver walks the ten steps once; `SetupWizardCommit` writes that one
run; `SetupWizardSummary.Render` prints it. `WizardDiscovery`, `WizardModelCheck` and
`SetupWizardCommit.StagedEntry` all read the model's single active answers.

The provider step already has two modes in the entry menu: the full walk starts at the preset list,
and **Add a provider** starts there too. This change makes the list itself repeat after each
provider, so one walk can collect several.

## Goals

- Collect N provider runs in one walk, each with its own credential and profile answers.
- Keep the store shapes, writers and transaction unchanged: one commit, N entries.
- Refuse a duplicate provider name with a rename prompt.
- Let a later run reuse an `env:`/`auth:` reference or type its own key, and say which in the
  summary.
- Keep every existing step contract (masking, go-back, skip, abort, one writer).

## Non-goals

- The non-interactive `lunate setup` flag path still configures one provider per run.
- No new profile/usage semantics: each provider run may write its own chat profile, and the last
  chat usage binding wins exactly as the store's read-modify-write already makes it.
- No new picker or prompt primitive.

## Decisions

### The model keeps one active run plus a completed list

`SetupWizardModel` gains `WizardProviderRun`, an immutable snapshot of the per-provider answers,
and `_providers`, the sealed runs. The existing `Provider`, `Credential`, `Model`, `ChatProfile`,
`FastProfile`, `ConnectionTest` and `ModelCheck` properties stay the **active** run, so the check
helpers and every single-run test keep their shape. `AllProviderRuns` is the sealed list plus the
active run when a provider is set.

`SealProvider()` moves the active answers into `_providers` and clears them only when the user
chooses **Add another provider…**. Choosing **Done** leaves the last run active so the decision
profile and decisions steps still have a provider context, and the commit writes sealed + active.

Each snapshot also carries the provider-scoped skipped steps, so a skip in one provider run is not
reported for another. The shared decisions skip stays on the model.

### The driver loops through the provider step

After the last per-provider step (`FastProfile` for the full walk, `ConnectionTest` for the
**Add a provider** entry), `WalkAsync` marks the active run ready and re-enters `Provider` when the
entry collects providers. The provider step then renders the collected list:

1. every collected provider as an informational row,
2. **Add another provider…**,
3. **Done** (selected by default).

**Done** returns a new `WizardMove.ProviderLoopDone`; `WalkAsync` continues at `DecisionProfile`
for the full walk, or at the summary for **Add a provider**. The first entry into the step still
renders the preset list directly, so the first provider's flow and frame names are unchanged.
After **Add another provider…**, `SealProvider()` runs and the preset list renders again.

### A renamed preset keeps its preset identity

A duplicate is detected by name against all runs. For a preset, the rename prompt asks for a new
entry name and the staged `WizardProviderChoice` records `PresetId` beside the new `Id`. Discovery,
the model check and the commit resolve the preset by `PresetId ?? Id`, while the entry name, auth
key and profiles use `Id`. A custom duplicate is refused by the existing custom-id loop, which adds
the duplicate test to its validation. No store schema changes: the entry is still a normal preset
entry with a different name.

### The summary groups by written file

For each run, the summary prints a provider line that names the entry's target file
(`providers.json`, or `settings.json` for a typed custom entry) and the credential file
(`auth.json`), followed by that run's credential, check, model, profile and fast lines. The
decisions and global skipped lines follow once. The content stays flat, one value per line, so the
width pins keep their layout contract.

### The commit loops over runs inside the existing transaction

`SetupWizardCommit` builds the typed-secret list from every run, seeds the three staged files once,
writes every provider entry and every profile/usage through the same writers, and replaces the
files in the same auth → providers → settings order. A failure therefore still restores all three
files byte for byte. `CommitTypedCustomEntry` runs per run; `SettingsStore.WriteUsage("chat", …)`
is called for each run that binds it, so the last collected provider's profile is the chat profile,
which matches "Done" meaning the run is finished.

## Risks

- A provider run with no chat profile (the **Add a provider** entry) only writes its entry and
  credential; the profile loop must skip empty runs.
- Reusing a profile name across runs overwrites the earlier profile through `SettingsStore`. The
  walk already permits that for a re-run; this change does not add a second name rule.
- The session tests pin the step order. They are updated to pass the new provider list, and the
  width pins are regenerated only where the summary content changes.
