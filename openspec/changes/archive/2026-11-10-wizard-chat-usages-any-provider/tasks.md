## 1. Pin the chat usage list and the write

- [x] 1.1 Add `SetupWizardUsageRoutesTests.The_chat_list_spans_every_configured_provider`: the chat list offers another configured provider's `provider/model` and never a decision-only family.
- [x] 1.2 Add `SetupWizardUsageRoutesTests.Chat_moved_to_another_configured_provider_writes_its_profile_and_usage`: the commit writes the profile's provider/model/reasoning and keeps `usages.chat` on it.
- [x] 1.3 Add `SetupWizardEntryMenuTests.Change_the_chat_profile_moves_it_to_another_configured_provider`: one `/setup` entry walk on a two-provider home moves `chat-main` to `anthropic/claude-sonnet-5-5` and leaves `providers.json` and `auth.json` untouched.

## 2. Offer the flat list in "change the chat profile"

- [x] 2.1 `ChatUsageStepAsync` lists the configured profiles with the current one marked, appends `WizardUsageOptions.Rows(..., decision: false)` and the inline provider row.
- [x] 2.2 `MoveChatProfileAsync` keeps the current profile's name, asks the thinking level the chosen model declares, applies the shared chat-family refusal, and records a `WizardProfileChoice` naming the selected provider and family.
- [x] 2.3 `WizardProfileChoice` carries optional `ProviderId`/`ApiFamily`; `SetupWizardCommit` writes them, and `RecordChatProfile` binds `usages.chat` for the `ChangeChatProfile` entry.

## 3. Verify the change

- [x] 3.1 `dotnet build Lunate.slnx -c Release`; `dotnet csharpier format .`; `dotnet csharpier check .`.
- [x] 3.2 Run the filtered classes: `SetupWizardUsageRoutesTests`, `SetupWizardEntryMenuTests`, `SetupWizardCommitTests`, `SetupWizardSummaryTests`, `SetupWizardDecisionCheckTests`, `SetupCommandWizardTests`, `WrappedLiveAreaTests`.
- [x] 3.3 `openspec validate wizard-chat-usages-any-provider --strict` and `openspec validate --all --strict`.
