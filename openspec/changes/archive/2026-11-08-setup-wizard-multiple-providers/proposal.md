## Why

One wizard run configures exactly one provider. A user who wants a second provider — a second
account, a local endpoint beside a hosted one, or a provider kept for decisions — has to finish the
walk, restart `/setup`, and walk the same steps again. OpenCode's `/connect` has the same repetition;
this change puts the repetition inside one walk (S-8 comparison, `docs/spikes/S-8/report.md`).

## What Changes

- The provider step becomes a loop: after a provider's credential, connection test, model, model
  check and profile steps, the walk returns to a list of every provider collected so far with
  **Add another provider…** and **Done**. The first entry still starts at the preset list.
- Each provider owns its credential answer. A later provider may reuse an already stored
  `env:`/`auth:` reference or store its own key; the summary names which.
- A provider name already collected is refused, and the step asks for another name for the new
  entry instead of silently overwriting the first.
- The summary groups each provider and its credential under the file the commit will write, and
  lists every provider. The commit writes every provider, credential, profile and usage in one
  transaction, so an abort leaves all three store files byte-identical.

## Capabilities

### New Capabilities

- None — the change extends the existing setup-wizard capability.

### Modified Capabilities

- `setup-wizard`: the full walk collects one or more providers in one run; the provider step's
  list, duplicate-name refusal, credential reuse and multi-provider summary/commit are specified.

## Impact

- Affected code: `SetupWizardModel` (per-provider answers and the collected list),
  `InteractiveSession.SetupWizard` (the provider loop and rename prompt),
  `SetupWizardCommit` (one pass over every provider), `InteractiveSession.SetupWizardSummary`
  (grouped review), and their fixture tests.
- No new NuGet packages, no public API change and no new store schema: the provider entry,
  credential, profile and usage shapes are unchanged; the wizard writes more than one of them in a
  single commit.
