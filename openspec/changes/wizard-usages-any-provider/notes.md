# Notes — fast and decision usages from any provider

## The reported symptom, confirmed at the base

`origin/main` at `b63ed865`:

- `src/Lunate.Coding/InteractiveSession.SetupWizard.cs:974-1016` — `FastStepAsync` offers only
  "Skip" or "also use profile '<chat>' as the fast usage": the fast usage cannot name a model.
- `src/Lunate.Coding/InteractiveSession.SetupWizard.cs:1021-1085` — `DecisionStepAsync` lists only
  the run's own provider: its pinned Jev versions and, for the adapter, the run's chat model. A
  configured machine with a second provider therefore cannot bind a decision profile to it, which
  is why the opencode-go decision path needed a whole separate run.

The fix is a feature (the flat list, the inline sub-walk), not a defect in a single line, so its
tests were written with the implementation rather than before it: `WizardUsageOptions` did not
exist at the base, so no test could fail on it. The regression pin is
`SetupWizardUsageRoutesTests.The_usage_list_spans_every_configured_provider`, which fails if the
list stops spanning the configured providers.

## Decisions taken

- **A picked fast model writes no thinking level.** The fast step never asks for one, so the
  profile carries the model and its family only; the picker still shows the model's declared
  thinking style.
- **The same-provider route reuses the chat profile** (today's behaviour and the flag path's
  `--fast`): the chat model check is the usage's check, so no second request is sent for the same
  model.
- **The distinct fast profile is named `fast`** (or `fast-<provider>` when the chat profile is
  itself named `fast`); a confirmed summary replaces an existing profile of that name, exactly as
  the chat profile name does.
- **Esc inside the inline sub-walk ends the whole wizard**, like Esc everywhere else: a
  half-configured provider has no meaning outside the run.
- **The inline provider's typed key is written in the same transaction** as its provider entry and
  the profile, through the staged auth file — never to `providers.json` or `settings.json`.
- **The flag path is unchanged**: `--fast` binds the chat profile, `--decision` names the run's own
  provider and model.
- **The decision check outside a session renders the System One session header over an ephemeral
  identity** (`RequestContext.Ephemeral()`), so the flag path and a unit-level check cannot fail on
  `x-opencode-session` before the wire.

## Tests run

`SetupWizardUsageRoutesTests`, `SetupWizardStepPins`, `SetupWizardDecisionCheckTests`,
`SetupWizardSummaryTests`, `SetupWizardModelTests`, `SetupWizardCommitTests`,
`SetupWizardModelCheckTests`, `SetupWizardModelStepTests`, `WizardModelCheckWireTests`,
`SetupWizardSessionTests`, `SetupWizardWidthTests`, `SetupWizardEntryMenuTests`,
`SetupWizardCredentialTests`, `SecretMaskingAuditTests`, `SetupCommandTests`,
`SetupCommandWizardTests`, `WizardDiscoveryTests`, `SetupStateTests`, `SetupRefusalTests`.

Known pre-existing failure, unrelated: `SetupRefusalTests
.Acp_startup_on_an_empty_home_exits_2_with_the_setup_refusal` also fails on the untouched main
checkout at `2649040c`.
