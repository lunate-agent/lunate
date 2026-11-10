## ADDED Requirements

### Requirement: The chat usage moves across configured providers

On a configured install, the "change the chat profile" entry SHALL offer one list: the configured profiles with the current binding marked, and every configured provider's models as `provider/model` with the model's family, plus `Set up another provider inline…`. Choosing an existing profile SHALL repoint `usages.chat` and write nothing else. Choosing a model SHALL move the current profile to that model's provider and model under the same name, ask the thinking level the model declares, write that profile and `usages.chat`, and touch no other profile, provider or credential entry. The inline row SHALL run the provider, credential, connection-test and model sub-walk and return to the same list with the added provider's models offered. A model whose family is decision-only SHALL be refused with the shared chat-binding message naming `/setup` and `lunate setup`. The first-run path is unchanged: it walks the provider and model steps and binds the chat usage it collected.

#### Scenario: Move chat to another configured provider's model

- **GIVEN** a configured install with an `openai` chat profile and an `anthropic` provider
- **WHEN** "change the chat profile" picks `anthropic/claude-sonnet-5-5`
- **THEN** the chat profile names `anthropic` and that model, `usages.chat` still binds it, and no other profile with its provider and credential entries changes

#### Scenario: An existing profile still rebinds the usage

- **GIVEN** a configured install with two profiles and `usages.chat` on the first
- **WHEN** "change the chat profile" picks the second profile's row
- **THEN** `usages.chat` names the second profile and no profile entry changes

#### Scenario: The first-run walk keeps its order

- **GIVEN** a fresh machine with one provider
- **WHEN** the full walk reaches the chat profile step
- **THEN** it asks the profile name for the model the model step collected, not a provider/model list
