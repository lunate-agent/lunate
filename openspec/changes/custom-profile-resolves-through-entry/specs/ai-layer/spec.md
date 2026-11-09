## ADDED Requirements

### Requirement: A custom entry's profile model is checked by family

Profile resolution SHALL treat a provider entry whose name is not a provider preset id as a custom entry, because such an entry's kind names only the adapter that speaks its API family's wire. The model such a profile resolves SHALL be the entry's own model: catalogue id `<provider id>/<model>`, the entry's kind as provider, and the profile's model id — or, when a catalogue entry matched, that entry's wire id — as the wire id.

The check that the model belongs to the entry SHALL compare API families, not adapter kinds: the matched catalogue entry's declared `apiFamily`, or its provider preset's default family when it declares none, SHALL be one of the families the entry serves. A mismatch SHALL be refused, naming the entry, the families it serves and the family the model speaks.

A matched catalogue entry under a different provider SHALL contribute model facts only — context window, tool and vision support, reasoning style, maximum output, capabilities, declared family and wire id. It SHALL NOT contribute pricing: the rates of the provider that bills are not the rates of this endpoint. When several catalogue entries hold a bare id in the entry's family and none of them is the entry's own id, resolution SHALL take facts from none of them, because none of them is the model this endpoint serves.

A preset entry SHALL keep the existing kind check and ambiguity refusal unchanged.

#### Scenario: The reported case resolves through the entry

- **GIVEN** a custom OpenAI-compatible entry `local` and the shipped catalogue, in which `kimi-k2` is `opencode/kimi-k2`'s wire id
- **WHEN** a profile naming `local` with model `kimi-k2` resolves
- **THEN** it resolves as `local/kimi-k2`, its provider is `openai`, its wire id is `kimi-k2`, and its context window is the catalogue entry's

#### Scenario: Selected facts come across, pricing does not

- **GIVEN** the same custom entry and catalogue entry, whose pricing and maximum output are recorded
- **WHEN** the profile resolves
- **THEN** the resolved model carries the maximum output, capabilities and reasoning style, and carries no pricing

#### Scenario: A model of another family is still refused

- **GIVEN** a custom entry `local` of kind `openai` and the catalogue entry `claude-sonnet-4-5` of provider `anthropic`
- **WHEN** a profile naming `local` with model `claude-sonnet-4-5` resolves
- **THEN** it is refused with a message naming `local`, `openai-chat` and `anthropic-messages`

#### Scenario: Several catalogue entries in the family contribute no facts

- **GIVEN** a custom entry `local` and a bare id that three catalogue entries hold, all of them in `openai-chat`
- **WHEN** a profile naming `local` with that id resolves
- **THEN** it resolves through the entry with unknown limits instead of failing as ambiguous

#### Scenario: A preset entry keeps the kind check

- **GIVEN** a `moonshotai` entry and a profile naming it with a model whose catalogue entry belongs to `openai`
- **WHEN** the profile resolves
- **THEN** it is refused exactly as before, naming the entry's kind and the model's provider
