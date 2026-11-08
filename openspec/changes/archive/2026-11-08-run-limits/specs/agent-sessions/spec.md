# agent-sessions delta — run limits

## ADDED Requirements

### Requirement: Run-limit records and resumed spend

A sessioned run that ends because it reached its cost or wall-clock limit SHALL append one
extension entry to the session before the run's terminal event. The entry SHALL name the limit
(`cost` or `time`), its configured value and the spend or elapsed time reached, with invariant
number formatting. The entry SHALL use the namespaced `ext/lunate/run-limit` type and SHALL NOT
change the session schema.

When a harness is built over a loaded session, it SHALL seed its cost accumulator from every
recorded message and model-usage usage in that session: a provider-reported `cost` is used as
recorded, otherwise the catalog prices the record's model and token lanes. A record that cannot be
priced is not counted as zero. The seeded total is the spend a configured cost limit is checked
against, so a resumed session keeps the spend it had before the restart.

#### Scenario: A limit is recorded before the terminal event

- **GIVEN** a sessioned run that reaches its cost limit
- **WHEN** the session entries are read
- **THEN** one `ext/lunate/run-limit` entry names `cost`, the limit and the spend, and it precedes the run's terminal event

#### Scenario: A resumed session keeps its spend

- **GIVEN** a session whose recorded usages price to a known total
- **WHEN** a harness resumes it with a cost limit equal to that total
- **THEN** the resumed run ends with `cost_limit` before a model request, and the event's spend equals the recorded total

#### Scenario: Unpriced history is not zero spend

- **GIVEN** a session whose recorded usage names a model with no catalog price
- **WHEN** the harness seeds its spend
- **THEN** that record contributes no priced amount rather than a synthetic zero
