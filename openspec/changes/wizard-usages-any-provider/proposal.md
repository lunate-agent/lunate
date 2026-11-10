## Why

The setup wizard's `fast` and `decision` usages can only draw from the provider the walk
configured in that run: the fast step asks "also use the chat profile as fast?" and the decision
step lists only the current provider's Jev version or chat model. A user who already configures a
second provider — or who wants the OpenCode Go decision path while chatting through another
provider — has to leave the wizard and re-enter it. OpenCode `/models` and Pi `/model` list every
connected provider; Lunate does not (issue #684, refined by the S-8 comparison, PR #690).

## What Changes

- The **fast** step becomes a model picker over a flat `provider/model` list across the provider
  configured in this run, every other already-configured provider, and any provider added inline.
  Choosing the chat model keeps today's behaviour (the chat profile also serves `fast`); choosing
  another model writes a profile for the chosen provider and binds `fast` to it.
- The **decision** step offers the same flat list, with each provider's native Jev version labelled
  native and adapter models labelled adapter, so a decision profile can come from a provider other
  than the one the chat profile uses.
- **Set up another provider inline…** runs the provider/credential/connection-test/model sub-walk,
  returns to the same usage step with the answer it already had, and commits the new provider (with
  its credential) in the same run's one write pass and the same review.
- The check for the selected model runs after the selection, states its estimated cost first and
  stays marked unverified on a skip — unchanged in principle, now also covering a model that
  belongs to another provider.
- No new package, public API or provider schema: the change is inside `Lunate.Coding`'s wizard, its
  commit and the setup-wizard spec.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `setup-wizard`: the full walk's step 7 (fast and decision profiles) now selects a model from any
  configured provider or a provider set up inline, and the review/commit names the added provider.

## Impact

- `src/Lunate.Coding/SetupWizardModel.cs` — usage answers carry the node that serves them, and
  inline providers are staged on the same model.
- `src/Lunate.Coding/InteractiveSession.SetupWizard.cs` — the fast/decision pickers and the inline
  provider sub-walk.
- `src/Lunate.Coding/WizardModelCheck.cs`, `SetupWizardCommit.cs`,
  `InteractiveSession.SetupWizardSummary.cs` — checks, commit and review resolve a model on the
  provider that owns it.
- `tests/Lunate.Coding.Tests/SetupWizard*` — three route tests (same provider, other configured
  provider, inline new provider).
- No behaviour change to `lunate setup`'s flags: `--fast` still binds the chat profile as fast and
  `--decision` still uses the run's provider.
