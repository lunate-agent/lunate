## MODIFIED Requirements

### Requirement: A profile's model resolves in its provider's scope

A profile names one provider entry and one model. Resolution SHALL read the model id in that provider's scope first — the entry's own catalogue id `<provider>/<model>`, then the exact catalogue id, then the single catalogue entry whose wire id the id is.

An entry whose name is not a provider preset id is a custom entry: its kind is only the adapter that speaks its API family's wire. A profile on a custom entry that names a model the catalogue holds under another provider SHALL resolve its model through the entry itself — its resolved id is `<provider id>/<model>` and its wire id is what the endpoint is sent — and the catalogue SHALL NOT be a reason to refuse it: the matching catalogue entry supplies model metadata only. A bare id no catalogue entry matches at all SHALL resolve through the entry with unknown limits. A model the catalogue already holds under the entry's own kind SHALL keep that catalogue entry, exactly as a preset entry's model does.

A profile on a preset entry SHALL keep the existing rule: the catalogue entry SHALL belong to the preset's kind, and a bare id that several catalogue entries hold SHALL keep the refusal that names every candidate.

A profile SHALL be refused when the model's API family is not one the entry serves. The refusal SHALL name the entry, the families it serves and the family the model speaks.

#### Scenario: A custom endpoint's profile names a bare id the catalogue holds under another provider

- **GIVEN** a custom entry `local` of kind `openai` whose endpoint is an OpenAI-compatible server, and a catalogue that holds `kimi-k2` only as `opencode/kimi-k2`
- **WHEN** a profile naming `local` with model `kimi-k2` resolves
- **THEN** it resolves as `local/kimi-k2` with wire id `kimi-k2` and the entry's endpoint, and nothing refuses it

#### Scenario: A custom endpoint's metadata comes from a matching catalogue entry

- **GIVEN** a custom entry `local` of kind `openai` and a catalogue that holds `opencode/kimi-k2` with a context window and tool support
- **WHEN** a profile naming `local` with model `kimi-k2` resolves
- **THEN** the resolved model carries that context window and capability, and its provider is the entry's kind `openai`

#### Scenario: A custom endpoint's profile is refused for another family

- **GIVEN** a custom entry `local` of kind `openai` and a catalogue entry `claude-sonnet-4-5` whose provider is `anthropic`
- **WHEN** a profile naming `local` with model `claude-sonnet-4-5` resolves
- **THEN** it is refused with a message naming `local`, the family it serves (`openai-chat`) and the family the model speaks (`anthropic-messages`)

#### Scenario: A custom provider's bare model id resolves

- **GIVEN** a provider entry `local` whose catalogue entry is `local/kimi-k2.7-code` with wire id `kimi-k2.7-code`, and a profile naming `local` with model `kimi-k2.7-code`
- **WHEN** the profile resolves
- **THEN** it resolves `local/kimi-k2.7-code`, keeps the entry's endpoint, and sends the wire id `kimi-k2.7-code`

#### Scenario: A custom endpoint naming the vendor's own model keeps the catalogue entry

- **GIVEN** a custom entry `local` of kind `openai` and a profile naming it with model `gpt-4o`, whose catalogue entry belongs to `openai`
- **WHEN** the profile resolves
- **THEN** it resolves the catalogue entry `gpt-4o` exactly as a preset profile does

#### Scenario: A preset profile keeps a bare id that its provider owns

- **GIVEN** a `moonshotai` provider entry and a profile naming it with model `kimi-k2.7-code`
- **WHEN** the profile resolves
- **THEN** it resolves `moonshotai/kimi-k2.7-code` instead of failing as ambiguous

#### Scenario: A preset profile refuses a model of another provider

- **GIVEN** a `moonshotai` provider entry and a profile naming it with model `gpt-4o`, whose catalogue entry belongs to `openai`
- **WHEN** the profile resolves
- **THEN** it is refused naming the entry's kind and the model's provider, as before

#### Scenario: A bare id no provider owns still names its candidates

- **GIVEN** a `groq` provider entry and a profile naming it with a model id that matches several catalogue entries and no entry of `groq`
- **WHEN** the profile resolves
- **THEN** it is refused with every candidate named, as before
