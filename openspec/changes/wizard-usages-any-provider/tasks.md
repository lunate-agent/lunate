# Tasks — fast and decision usages from any provider

## 1. Staged shapes

- [x] 1.1 `SetupWizardModel`: `WizardModelRef`, `WizardUsageChoice` and `WizardInlineProvider`
  records; `FastUsage` + computed `FastProfile`, `FastCheck`, `RecordFastUsage`, `RecordFastCheck`,
  `InlineProviders` + `RecordInlineProvider`, `HasStagedWrites` includes them; `RecordFastProfile`
  keeps the flag path's reuse answer.
- [x] 1.2 `WizardDecisionChoice` replaced by `WizardUsageChoice` at every call site (driver, flag
  path, checks, commit, summary, tests).

## 2. The usage pickers

- [x] 2.1 New `InteractiveSession.SetupWizardUsages.cs`: the flat option list from the run's
  provider, its discovery listing or catalog entries, inline providers and every configured
  provider; native/adapter split for the decision step; `provider/model` rows with family and
  thinking.
- [x] 2.2 `FastStepAsync` picks a model (reuse the chat profile, a distinct fast profile, or skip)
  and runs the fast check after the cost prompt; `DecisionStepAsync` picks across providers and
  keeps the native/adapter labels.
- [x] 2.3 `Set up another provider inline…`: the provider/credential/connection-test/model sub-walk
  on a throwaway model, `_listing` saved and restored, Back moves inside the sub-walk, Esc ends the
  wizard, a skipped model stages nothing.

## 3. Checks, review, commit

- [x] 3.1 `WizardModelCheck`: provider-aware `RunForAsync`/`RunDecisionForAsync` over a
  `WizardModelRef` (entry + credential override), existing overloads delegate; per-entry
  `ProviderExposesSystemOne`/Jev helpers.
- [x] 3.2 Summary: fast usage/profile line with provider and model, fast check line, decision
  profile with its provider/model, inline providers with credential, check and model.
- [x] 3.3 Commit: `StagedEntry(provider, credential, model)`, inline provider entries and typed keys
  in the same transaction, fast profile when it is not the chat profile, decision profile naming
  the model's own provider, `apiFamily` only when it differs from that provider's default.

## 4. Tests

- [x] 4.1 `SetupWizardUsageRoutesTests`: same provider, another configured provider, inline new
  provider — each reaches a passing check and writes the profile/usage; the inline route adds the
  provider to the review and the commit; no route loses an earlier answer.
- [x] 4.2 `WizardModelCheck` tests for a ref on another provider (adapter and native), and a commit
  test for an inline provider plus a distinct fast profile.
- [x] 4.3 Summary test naming a fast usage and a decision profile from another provider.
- [x] 4.4 Update the existing decision-picker tests to navigate the flat list by filter, and the
  `WizardDecisionChoice` construction sites.

## 5. Verification and close-out

- [x] 5.1 Test-first proof recorded in the change's `notes.md`: the route tests and the record
  changes fail before the implementation, pass after.
- [x] 5.2 `dotnet csharpier format .`, `git status --short` empty, Release build of
  `Lunate.Coding` and `Lunate.Coding.Tests`, the test classes run by filter listed in the PR.
- [x] 5.3 `openspec validate --all --strict` and `bash scripts/check-openspec-skills.sh` pass.
- [x] 5.4 Docs: the setup guide's wizard section names the three routes; the spec delta is synced
  and the change archived as the last commit.
