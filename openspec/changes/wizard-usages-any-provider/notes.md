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

- **A decision adapter row says "adapter" in the label** (`provider/model (adapter, family)`); the
  fast rows say the family only, because a fast usage never binds the decision family.
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

## Review round 2 (head `f12ebeda` returned)

- **The #694 chat-usage move was not this change.** It reverted out of the branch together with
  its archived change (`wizard-chat-usages-any-provider`), so this pull request carries only the
  fast/decision usages. Two fixes that commit had made are kept because they belong to this
  change: the wrapped-live-area waits name the fast step's real title (`fast usage`), and the
  decision-check session test finds the flat list's Jev row by its text instead of an assumed
  offset.
- **The archive of this change was not the branch's final commit** because the #694 work landed
  after it. Reverting that archive restores the change folder here; the archive is the last commit
  again.
- **The delta spec was stale against `main`.** Its MODIFIED "The full walk" text predated the
  branch's rebase, so archiving it would have clobbered main's newer wording (the visible prompt,
  the decisions-settings loop, the custom-endpoint scenario's place). The delta now carries main's
  current requirement text with only this change's two additions (step 7 draws from any configured
  provider; step 9 names the usage profiles and the inline providers).
- **Wizard-driven route coverage** (`SetupWizardUsageWalkTests`): all three routes now drive the
  wizard's own pickers through selection → check → commit. The inline route is the one the review
  asked for: it selects `Set up another provider inline…`, walks the provider/credential/model
  sub-walk, returns to the same decision picker with the chat answer, the model check and the
  decisions settings intact, then sends the decision check and confirms.
- **`SetupWizardEntryMenuTests.A_first_run_keeps_the_full_walk` could not be reproduced.** At
  `df96df2e` (macOS/arm64, SDK 10.0.403) it passed 10/10 isolated, 11/11 in its class, 3/3 under
  eight busy-loop CPU hogs, and 3/3 on a clean `main` worktree at `2929211f`; its 60 s wait budget
  was never approached (runs take about 0.8 s). The test is unchanged on this branch, so a failure
  there is inherited rather than caused here. Recorded on the pull request with the measurements.

