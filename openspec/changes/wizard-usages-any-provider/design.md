# Design — fast and decision usages from any provider

Binding implementation detail. Where this file pins a behaviour, apply follows it verbatim; a
contradiction between this file and the spec delta is a stop-and-report, not a choice.

## 1. Shapes

Two records carry the new answers; both stay internal to `Lunate.Coding`.

```csharp
// A model the fast/decision picker can offer: the provider answer that owns it, the entry the
// check resolves against, the credential the walk holds (null for an already-configured provider,
// whose entry names its own reference), and the model choice.
internal sealed record WizardModelRef(
    WizardProviderChoice Provider,        // staged provider choice (preset row or custom fields)
    WizardCredentialChoice? Credential,   // null for a configured entry
    ProviderEntry Entry,                  // the entry the check and the profile resolve through
    WizardModelChoice Model,
    bool Native = false,                  // decision only: the provider's System One family
    string? PinnedVersion = null);

// A usage answer: the profile it writes and the model it binds.
internal sealed record WizardUsageChoice(
    string ProfileName,
    string ProviderId,
    string ModelId,
    string ApiFamily,
    string? ReasoningLevel = null,
    bool Native = false,
    string? PinnedVersion = null,
    bool ReuseChatProfile = false);       // fast only: the chat profile already holds the model
```

`WizardDecisionChoice` is replaced by `WizardUsageChoice`: the decision usage is one more usage
answer with `Native`/`PinnedVersion`. `SetupWizardModel` keeps `DecisionProfile` as the property
name and gains `FastUsage`; `FastProfile` stays a computed `FastUsage is not null` so the flag path
and the summary keep their current call sites. `RecordFastProfile(bool)` (flag path) stages a
reuse answer from the chat profile; `RecordFastUsage` stages a picker answer.

An inline provider is staged on the same model:

```csharp
internal sealed record WizardInlineProvider(
    WizardProviderChoice Provider,
    WizardCredentialChoice Credential,
    WizardCheckResult ConnectionTest,
    WizardModelChoice Model,
    IReadOnlyList<WizardModelListing> Listing);
```

`SetupWizardModel.RecordInlineProvider` appends it; `InlineProviders` reads the list. A staged
inline provider is a write, so `HasStagedWrites` includes a non-empty list.

## 2. What the pickers list

`InteractiveSession.SetupWizardUsages.cs` (new partial file) builds the option list once per step
opening:

1. **The provider configured in this run** — its discovery listing (`_listing`) when the connection
   test ran, otherwise the catalog's entries for that provider; plus the chosen model when it is not
   in that list. A provider that exposes `systemone` additionally contributes its catalog Jev
   models, labelled native, in the decision step only.
2. **Every provider added inline in this run** — the same two sources from the inline record.
3. **Every already-configured provider** (`_providerEntries`) other than the run's own — the
   catalog's models for that entry's kind. The decision step splits them into native Jev rows
   (family `systemone`) and adapter rows (chat families); the fast step lists chat families only,
   because a `systemone` profile is refused for a chat usage.
4. `Set up another provider inline…` and `Skip`.

Rows are `provider/model` with the family and, where the model declares one, its reasoning style:

```text
> openrouter/typesafe/jev-1.14 (native Jev)
  anthropic/claude-sonnet-5-5 (anthropic-messages, thinking effort)
  Set up another provider inline…
  Skip
```

The step titles stay the current ones ("fast usage…", "Decision profile (the provider exposes the
native Jev…" / "…(this provider serves no native Jev profile; the adapter label applies)") so the
existing fixture pins keep their meaning. The picker value is a key that never collides with a model
id (a leading NUL plus the provider index), and every row keeps the model ref beside it.

Selecting a model:

- **Fast, same model as the chat profile** (`ReuseChatProfile`) — the chat profile also serves
  `fast`; the fast check result is the chat model check's (passed, failed, or unverified when it was
  skipped). No second request is sent for the same model.
- **Fast, any other model** — profile name `fast` (or `fast-<provider>` when the walk's chat profile
  is itself named `fast`), `usages.fast` binds it, and one short chat check runs after the cost
  prompt.
- **Decision, native** — profile `decisions`, family `systemone`, pinned version as today.
- **Decision, adapter** — profile `decisions`, family the model's (or entry's default), labelled
  adapter, and the one-tiny-noul decision check runs after the cost prompt.

A check failure keeps today's loop: the usage answer is cleared and the step asks again; a skip
stages the usage with its check marked `Skipped`, and the summary names it unverified.

## 3. Inline provider sub-walk

`SetupInlineProviderAsync` runs the existing step drivers — `ProviderStepAsync`,
`CredentialStepAsync`, `ConnectionTestStepAsync`, `ModelStepAsync` — on a throwaway
`SetupWizardModel` until it leaves the model step, saves and restores the session's `_listing`
around the sub-walk, and on completion records a `WizardInlineProvider` and returns to the same
usage step (the driver loops inside the usage step, so the outer answer and every other answer are
untouched).

- **Back** inside the sub-walk moves to the previous sub-walk step; Back at the provider step stays
  there (there is no earlier step inside the entry).
- **Esc** at any sub-walk prompt ends the wizard with nothing written, exactly like Esc at any other
  step: a half-configured provider has no meaning outside the run.
- **A skipped model** cannot serve a usage: the sub-walk ends with a warning and returns to the
  usage step without staging anything.
- A duplicate provider name is refused by the credential/provider path as today (the commit's
  provider writer replaces the entry by name; the review names the added provider).

## 4. Checks across providers

`WizardModelCheck` gains one provider-aware entry point per check:

```csharp
RunForAsync(WizardModelRef model, ModelCatalog, IChatClientFactory, AuthStore,
           Func<string,string?> environment, RequestContext?, CancellationToken)
RunDecisionForAsync(WizardModelRef model, ModelCatalog, ...)
```

Each builds the `ResolvedModelProfile` over `model.Entry` and the model's staged `ModelInfo`, then
calls the shared `DecisionUsageModels.CreateUsageClient` with `model.Credential?.Key` as the
credential override. A configured provider therefore resolves its own `env:`/`auth:` reference
through the auth store and environment; an inline provider's typed key stays in memory until the
commit. The existing `RunAsync`/`RunDecisionAsync` overloads stay and delegate with the walk's own
provider/entry, so the flag path and step 5 are unchanged. Failure detail redacts the credential
as today.

`ProviderExposesSystemOne`/`JevVersions` become per-provider-entry helpers so an inline or
configured provider gets the same native/adapter treatment.

## 5. Review and commit

- Summary adds `fast usage: <profile> (<provider>/<model>)` (with the reasoning level when the model
  declares one) and a `fast check:` line, and repeats the decision profile with its provider/model;
  it lists each inline provider with its credential source, connection test and model.
- `SetupWizardCommit.StagedEntry(model)` is split into `StagedEntry(provider, credential, model)`
  plus the existing one-argument wrapper. The commit writes the walk's own provider as today, then
  every inline provider's entry through the same `ProviderStore.Save` and its typed key through the
  staged auth file, in the same transaction.
- `WriteProfileAndUsages` writes the fast usage's own profile when it is not the chat profile, naming
  the provider that owns the model and recording `apiFamily` only when it differs from that
  provider's declared default. The decision profile does the same with the model's own provider.
- No new file, key or schema: `usages.fast`, `profiles.<name>` and `decisions.profile` are the
  existing settings shapes.

## 6. Tests

New `SetupWizardUsageRoutesTests` (interactive host, scripted transports) with one test per route:

1. **Same provider** — fast picks the chat model; the usage binds the chat profile and the summary
   records the chat check.
2. **Another configured provider** — a configured `anthropic` entry beside the run's `openai`; fast
   picks `anthropic/claude-sonnet-5-5`, the check targets the Anthropic host with the configured
   credential, and `settings.json` carries `profiles.fast` + `usages.fast`.
3. **Inline new provider** — the decision step picks `Set up another provider inline…`, the sub-walk
   adds `opencode-go` with a typed key, the native Jev profile binds it, and one commit writes
   `providers.json`, `auth.json` (the key), `profiles.decisions` and `decisions.profile`.

Unit tests: `WizardModelCheck` resolves a ref on another provider (adapter and native), the commit
writes inline providers and a distinct fast profile, and the summary names a usage from another
provider. Existing tests that constructed `WizardDecisionChoice` move to `WizardUsageChoice`, and
the decision-picker tests navigate with the filter rather than a fixed number of Up presses.

## 7. Out of scope

The provider list inside the walk's own first step, manage/rename/remove (#692), the chat usage
picker on a configured install (#694), the review-as-file-manifest (#685) and quick/detailed modes
(#682) are other changes. This change does not alter the flag path's answers.
