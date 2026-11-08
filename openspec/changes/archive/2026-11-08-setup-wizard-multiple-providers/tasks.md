# Tasks

One change, landed in one PR as ordered slices; each slice compiles and its focused tests pass
before the next starts.

## 1. Model: one active run plus the collected list

- [x] 1.1 Add `WizardProviderRun` and `AllProviderRuns`/`ProviderIds`/`SealProvider()` to
      `SetupWizardModel`, keeping the existing active-run properties for the check helpers and
      single-run callers; per-run provider-scoped skipped steps are snapshotted on seal.
- [x] 1.2 `HasStagedWrites` counts sealed runs and the active run; sealing clears the active
      answers and the provider-scoped skips for the next run.
- [x] 1.3 Unit tests: two recorded runs, sealing preserves the first, duplicate-name detection.

## 2. Driver: the provider loop and the rename prompt

- [x] 2.1 Provider step renders the collected providers plus **Add another provider…** and **Done**
      after a run is ready; **Done** continues to the decision-profile step (full walk) or summary
      (**Add a provider**); the first entry still renders the preset list.
- [x] 2.2 `WalkAsync` re-enters the provider step after `FastProfile` (full walk) or
      `ConnectionTest` (**Add a provider**) only for entries that collect providers.
- [x] 2.3 A duplicate provider name is refused: a preset asks for a new entry name and records
      `PresetId`; a custom id is refused through its existing validation loop.
- [x] 2.4 `PresetId` resolves the preset in discovery, the model/decision checks and the commit,
      while the entry name stays the staged `Id`.
- [x] 2.5 Driver tests: two providers in one walk through the real component stack; duplicate name
      prompts for a rename; a second provider reuses an `env:`/`auth:` reference.

## 3. Commit: every provider in one transaction

- [x] 3.1 `SetupWizardCommit` stages every run's typed key and entry, then writes every profile and
      usage the runs collected; the transaction order and rollback stay unchanged.
- [x] 3.2 Commit test: two providers and two credentials are in the written home; a typed custom
      entry still uses the `auth login --custom` writer; an abort after two runs writes nothing.
- [x] 3.3 The writer pinning test still holds with two runs (the wizard formats nothing).

## 4. Summary: group each provider under its written file

- [x] 4.1 `SetupWizardSummary.Render` prints one group per run naming the entry and credential
      files, then the run's values; decisions and global skips follow once.
- [x] 4.2 Summary tests: two providers' groups name both files and both credentials, including a
      reused reference; a single run keeps today's content.
- [x] 4.3 Width pins: 60 and 80 columns render the multi-provider summary with no overflow.

## 5. Docs and gate

- [x] 5.1 `docs/first-run.md` (and any quick-start text it inherits) says one walk can add more
      than one provider, and how **Done** ends the loop.
- [x] 5.2 `openspec validate setup-wizard-multiple-providers --strict` passes.
- [x] 5.3 The changed test classes pass in Release; CSharpier check is clean; PR lists the classes.
