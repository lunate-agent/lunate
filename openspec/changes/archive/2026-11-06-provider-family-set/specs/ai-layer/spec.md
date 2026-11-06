## MODIFIED Requirements

### Requirement: Provider API families

A model SHALL resolve exactly one API family from the closed set `openai-chat`, `openai-responses`, `anthropic-messages`, `google` and `systemone`. Resolution precedence SHALL be: the profile's explicit `apiFamily` override; then discovery metadata; then the catalog entry's `apiFamily`; then the provider's single declared family. When the provider declares more than one family and no higher source answered, resolution SHALL fail with an error naming the provider, the model and the families to choose from. A family SHALL select the adapter: `openai-chat` the OpenAI chat client, `openai-responses` the OpenAI Responses client, `anthropic-messages` the Anthropic SDK adapter, `google` the Google.GenAI SDK's Microsoft.Extensions.AI client (streaming, function calling, system instructions, images, thinking with thought signatures), and `systemone` the native System One decision transport rather than a chat client. A model with no family and a provider with exactly one family SHALL behave as that family, so existing entries keep working.

#### Scenario: A profile override wins

- **GIVEN** a model whose catalog entry says `openai-chat` and a profile that says `openai-responses`
- **WHEN** the pipeline is built
- **THEN** the Responses adapter is used

#### Scenario: An ambiguous provider fails loudly

- **GIVEN** a provider with `openai-chat` and `anthropic-messages` and a model that declares no family
- **WHEN** the pipeline is built
- **THEN** it fails with an error naming both families and how to choose

#### Scenario: An unknown family is refused by name

- **GIVEN** a catalog entry with `apiFamily: openai-realtime`
- **WHEN** the catalog is loaded
- **THEN** loading fails with an error naming the family and the supported set

#### Scenario: The Google family uses the native client

- **GIVEN** a model whose family is `google`
- **WHEN** the pipeline is built
- **THEN** the Google.GenAI `AsIChatClient` client is used against the Google family base URL
- **AND** a streamed tool call preserves the thought signature in returned reasoning content
