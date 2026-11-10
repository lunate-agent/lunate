## Why

The S-8 spike's model-choice row (#684, with #694 folded in) says every usage picker lists `provider/model` across **all** configured providers in one list. The fast and decision steps follow it; the chat usage does not. On a configured install, `/setup`'s "change the chat profile" entry can only repoint `usages.chat` at an already-configured profile, so moving chat to another provider's model needs a walk that prefill ties to the current provider.

## What Changes

- "Change the chat profile" offers one list: the configured profiles with the current binding marked, and every configured provider's model as `provider/model` with the model's family, plus `Set up another provider inline…`.
- Choosing an existing profile keeps today's behaviour: it repoints `usages.chat` and writes nothing else.
- Choosing a model moves the current profile to that provider and model under the same name, asks the thinking level the model declares, and writes only that profile and `usages.chat`.
- The inline row runs the provider, credential, connection-test and model sub-walk and returns to the same list, so the added provider's models are offered without a separate step.
- The first-run full walk is unchanged: it walks the provider and model steps and binds the chat usage it collected.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

None. `setup-wizard` gains one requirement, "The chat usage moves across configured providers"; the existing "Fast and decision usages from any provider" requirement is unchanged.

## Impact

- `src/Lunate.Coding/InteractiveSession.SetupWizardEntries.cs` (the chat usage list)
- `src/Lunate.Coding/InteractiveSession.SetupWizardChat.cs` (the moved profile's name, thinking level and binding)
- `src/Lunate.Coding/SetupWizardModel.cs` (`WizardProfileChoice` names its provider and family)
- `src/Lunate.Coding/SetupWizardCommit.cs` (the chat profile writes that provider and family)
- Tests under `tests/Lunate.Coding.Tests/` for the option list, the commit and one `/setup` entry walk
- No public API, dependency, settings-format or flag change.
