## ADDED Requirements

### Requirement: A profile's model resolves in its provider's scope

A profile names one provider entry and one model. Resolution SHALL read the model id in that provider's scope first — the entry's own catalogue id `<provider>/<model>`, then the exact catalogue id, then the unqualified lookup — so a profile may carry a bare model id that the catalogue also holds under other providers when its own provider owns it. A bare id no provider entry owns SHALL keep the existing refusal that names every candidate.

#### Scenario: A custom provider's bare model id resolves

- **GIVEN** a provider entry `local` whose catalogue entry is `local/kimi-k2.7-code` with wire id `kimi-k2.7-code`, and a profile naming `local` with model `kimi-k2.7-code`
- **WHEN** the profile resolves
- **THEN** it resolves `local/kimi-k2.7-code`, keeps the entry's endpoint, and sends the wire id `kimi-k2.7-code`

#### Scenario: A preset profile keeps a bare id that its provider owns

- **GIVEN** a `moonshotai` provider entry and a profile naming it with model `kimi-k2.7-code`
- **WHEN** the profile resolves
- **THEN** it resolves `moonshotai/kimi-k2.7-code` instead of failing as ambiguous

#### Scenario: A bare id no provider owns still names its candidates

- **GIVEN** a provider entry `local` and a profile naming it with a model id that matches several catalogue entries across other providers and no entry of `local`
- **WHEN** the profile resolves
- **THEN** it is refused with every candidate named, as before
