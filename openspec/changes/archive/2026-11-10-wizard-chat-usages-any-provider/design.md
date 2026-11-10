## Context

`/setup` on a configured install opens an entry menu. "Change the chat profile" currently walks only the chat-profile step and shows `Profile to use for chat`, a list of profile names; the prefill binds the walk to the current chat profile's provider, and the model step is not walked. A user therefore cannot move `usages.chat` to a model on another configured provider without adding a profile first and switching to it.

The fast and decision steps already build a flat `provider/model` list in `WizardUsageOptions.Build`, from the run's staged provider, inline additions, and every configured provider's catalog models, excluding decision-only families for chat usages.

## Goals / Non-Goals

**Goals:**

- One chat usage list: configured profiles plus `provider/model` rows across every configured provider, with the inline provider row.
- Choosing a model moves the current profile to that provider and model under the same name, writes that profile and `usages.chat`, and touches nothing else.
- The inline route returns to the same list with the new provider's models visible.
- The first-run full walk keeps its provider/model/chat-profile order.

**Non-Goals:**

- Changing the full walk's steps or the `lunate setup` flags.
- Renaming or deleting profiles beyond the one being moved.
- Running a model check on a chat move; the existing entry does not check, and the issue asks only for the profile and usage write.

## Decisions

- **Reuse the flat list.** `ChatUsageStepAsync` builds the profile rows from the settings and appends `WizardUsageOptions.Rows(Build(..., decision: false))`, then the inline row. The list is one picker, matching the S-8 grammar.
- **The current profile carries the change.** A picked model is not a new profile name: `MoveChatProfileAsync` keeps the current chat profile's name, asks only the thinking level the chosen model declares, and records a `WizardProfileChoice` that names the model's provider and family. `WizardProfileChoice` gains optional `ProviderId`/`ApiFamily` so the commit can write that provider without pretending the walk's own provider answer changed.
- **The commit uses the choice's provider.** `WriteProfileAndUsages` writes `chat.ProviderId ?? model.Provider.Id` and derives the family from the choice when it names a provider; the full walk's path (no provider id) is unchanged. `RecordChatProfile` binds the chat usage for `ChangeChatProfile` as well as `WalkEverything`.
- **The existing refusal stays.** Choosing a `systemone` profile from the list, or a model whose family is decision-only, goes through `UsageProfileValidation.RequireChatFamily`, so the message and the `/setup` repair stay one.

## Risks / Trade-offs

- [The list mixes profile names and model ids] -> Each row is typed in the grammar it answers: a profile name rebinds the usage, a `provider/model` moves the profile. The current profile is marked `(current)`.
- [A moved profile overwrites its old model] -> That is the requested change; the other profiles are untouched and the summary names the current profile, and the written store is the assertion.
- [The catalog may not list a custom provider's models] -> `WizardUsageOptions` adds the staged model, and a custom provider already appears through the configured entries; an unlisted model can still be set up by adding a profile.
