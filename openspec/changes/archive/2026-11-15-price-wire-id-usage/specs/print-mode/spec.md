## MODIFIED Requirements

### Requirement: Cost in JSON output

With `--json`, a `usage_updated` object SHALL carry a numeric `costUsd` when the record's model is in the catalog and every token lane with a positive count has a known price. The record's model id SHALL be resolved in the scope of the provider the run selected before it is priced: the id a provider echoes on the wire is priced as that provider's own catalog entry when the entry's wire id matches, and an id that names no entry, or whose bare wire id several presets serve outside that scope, SHALL be treated as unknown rather than guessed. The value SHALL be the record's cost in US dollars, computed with `decimal` and invariant arithmetic. A record whose model is unknown or unpriced SHALL omit the field rather than emit `0`; a zero-token priced record MAY carry `0`. The existing `usage` object and the cache fields SHALL keep their shape. The field SHALL be absent from the non-JSON output, which still carries only the final answer on stdout.

#### Scenario: A priced record carries its cost

- **GIVEN** a `usage_updated` record for a priced model with input, output and cache tokens
- **WHEN** `--json` serializes it
- **THEN** the line carries `costUsd` beside the unchanged `usage` object

#### Scenario: An unpriced model omits the field

- **GIVEN** a `usage_updated` record whose model has no pricing
- **WHEN** `--json` serializes it
- **THEN** the line has no `costUsd` field

#### Scenario: An unknown cache rate suppresses a used lane's cost

- **GIVEN** a record with positive cache-read tokens and no cache-read rate
- **WHEN** `--json` serializes it
- **THEN** the line has no `costUsd` field

#### Scenario: The default output is unchanged

- **GIVEN** print mode without `--json`
- **WHEN** a run reports priced usage
- **THEN** stdout still carries only the final assistant text and no cost field

#### Scenario: A provider's wire id prices as the selected preset

- **GIVEN** a run whose selected model has a catalog id different from its wire id, a second preset serving the same wire id at another price, and a `usage_updated` record naming only that wire id
- **WHEN** `--json` serializes the record
- **THEN** the line carries `costUsd` computed from the selected preset's prices

#### Scenario: A wire id outside the selected provider stays unpriced

- **GIVEN** a `usage_updated` record naming a bare wire id that no entry of the selected provider serves and that several other presets serve
- **WHEN** `--json` serializes the record
- **THEN** the line has no `costUsd` field
