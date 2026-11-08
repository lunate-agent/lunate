## ADDED Requirements

### Requirement: Provider-scoped catalogue lookup

The catalogue SHALL offer a lookup scoped to one provider entry: the entry's own catalogue id (`<provider>/<model>`), then the exact catalogue id, then the single entry whose wire id the id is. A bare id that matches several entries' wire ids across the catalogue SHALL come back as the full candidate list rather than an exception, so the caller that knows the provider can resolve it or ask. `ModelCatalog.Find` SHALL keep its current behaviour, including its ambiguity refusal, for callers that have no provider context.

#### Scenario: The provider's own id wins

- **GIVEN** a catalogue that holds `moonshotai/kimi-k2.7-code`, `opencode/kimi-k2.7-code` and `opencode-go/kimi-k2.7-code`
- **WHEN** `kimi-k2.7-code` is resolved in the scope of a provider entry named `opencode`
- **THEN** the lookup answers `opencode/kimi-k2.7-code`

#### Scenario: An unknown id comes back with its candidates

- **GIVEN** a provider entry named `local` and the same catalogue
- **WHEN** `kimi-k2.7-code` is resolved in its scope
- **THEN** no entry is answered and the three wire-id candidates are returned

#### Scenario: The unscoped lookup still refuses an ambiguous bare id

- **GIVEN** the same catalogue
- **WHEN** `ModelCatalog.Find` is called with `kimi-k2.7-code`
- **THEN** it throws with every candidate named, exactly as before
