## Context

Four independent, small defects in the setup wizard's user-facing text and in the answer types the
wizard stages (review finding N-5, `src/Lunate.Coding/SetupWizardModel.cs:71`). Three of them are
places where the shipped specs already say what should happen and the code says something else; one
(the profile-name clash) is a case no requirement covered.

The wizard has one driver per entry point and one shared set of answer types:

```
lunate setup (flags)                    /setup, first run (interactive)
  SetupCommand.Walk ─┐                 ┌─ InteractiveSession.SetupWizard (steps)
                     ├─> SetupWizardModel (staged answers) ─> SetupWizardCommit (one write pass)
  WizardModelCheck ──┘        │                     │
  WizardDiscovery             └─> SetupWizardSummary (the summary both entry points print)
```

The four defects sit on three of those seams: the answer types' own text (`WizardCredentialChoice`),
the step drivers (`DecisionStepAsync`, the model-check notice), and the shared failure detail
(`WizardModelCheck` → `WizardCheckResult.Detail` → summary).

## Goals / Non-Goals

**Goals:**
- A typed credential can never reach text through the staged answers.
- The usage line `lunate setup` prints names every value `--thinking` accepts.
- A full walk cannot commit a configuration in which `profiles.decisions` is the decision profile
  while `usages.chat` points at it as a chat profile.
- A failed model check names the model, the family and the status, in the same words in both entry
  points.

**Non-Goals:**
- Making the decision profile's name configurable: the name is part of the wizard's behaviour the
  profile and `decisions.profile` binding share, and a rename is a feature, not this fix.
- Reworking where the credential step's masking happens (done under #617/#644).
- Changing the session format, the settings schema or the provider store (nothing here writes a
  different shape).

## Decisions

**D1 — The answer's own `ToString()` is the fix, not a call-site guard.** `WizardCredentialChoice` is
a positional record, so the compiler generates a `ToString()` that prints every positional property —
`Key` included. Overriding `ToString()` on the record renders the reference and `Key = [redacted]` for
a typed key, which fixes every present and future consumer at once. Alternatives: a call-site scrub
(rejected — there is no single call site, and the next one added would leak again) and removing `Key`
from the positional list (rejected — it changes the constructor shape every test and driver uses for
no behavioural gain).

**D2 — The decision profile's name becomes one constant, and the decision step guards on it.** The
name `decisions` was written twice (the decision step and the flag path's default) and the chat profile
step could not see it: its existing "already configured" warning compares against `_settings.Profiles`,
and the decision profile is not staged yet when the chat profile is asked. The chat profile step also
cannot refuse the name outright — a configured install keeping a profile called `decisions` through
`/setup`'s "change the chat profile" entry must stay possible, and that entry never walks the decision
step. So the guard belongs where the collision would be created: at the start of the decision step,
which compares this walk's chat profile name with `SetupWizardModel.DecisionProfileName`, names the
clash, records no decision profile (the step is then skipped, exactly like the skip answer) and
advances. Nothing is written for the decisions layer, so the commit cannot replace the chat profile and
`usages.chat` keeps pointing at a chat profile.

**D3 — The failure detail carries the three facts; the surfaces print it unchanged.** `WizardModelCheck`
owns the one place a check failure becomes text (`WizardCheckResult.Detail`), so it composes
`<model id> on family <api family>: <status or response shape>` there, after the existing credential
redaction. `InteractiveSession.SetupWizard`'s notice stops repeating the model and family itself and
prints the detail, so it and the `lunate setup` summary (one `SetupWizardSummary` line) cannot drift.
The decision check keeps its own wording: its analogous fact is native-versus-adapter, which its notice
already names.

**D4 — The usage line copies the spec, it does not invent a new list.** `agent-config`'s "Setup command
line" requirement already lists `--thinking <off|low|medium|high>` and the flag parser already accepts
`off` (treating it as "no level"); only the `Usage` constant lagged. The line is corrected to the spec's
order, and the unknown-level refusal keeps the same four words.

## Risks / Trade-offs

- [A user who wanted both a chat profile and a decision profile named `decisions`] → impossible by
  design; the notice says so and names the rename as the way out. The alternative (silently replacing
  one of them) is the defect being fixed.
- [The decision step skipping itself removes the decision check from that walk] → intended: the check
  verifies the profile that was never staged. The summary names the step skipped.
- [Callers that asserted on the old detail text] → the interactive notice's wording changes; the
  existing model-check tests are updated in the same change and still assert model, family and status.

## Migration Plan

Behaviour-only; no migration. Rollback is reverting the four commits.

## Open Questions

- None.
