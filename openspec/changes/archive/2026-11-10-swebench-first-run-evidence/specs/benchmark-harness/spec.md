## MODIFIED Requirements

### Requirement: One cost cap per run

A published run SHALL have exactly one cost cap in US dollars. The orchestrator SHALL pass the
remaining budget to each Lunate invocation as `--max-cost` so a single instance cannot spend past
the run's remaining budget, and SHALL stop starting new instances when the cap is reached. A cost
limit stop SHALL be reported as a limit stop with the cap and the spend reached, not as an agent
failure. The committed summary SHALL report the final spend for every arm and the total against the
cap. When an instance's spend cannot be priced, the run SHALL record that instance as unpriced,
SHALL treat the cap as reached, and SHALL report the run total as unknown instead of adding a
substitute amount. A reported total SHALL never exceed the cap on paper. A run whose model has no
known price SHALL NOT start with a cap unless the operator explicitly allows unpriced usage.

#### Scenario: The cap stops the run exactly at the limit

- **GIVEN** a run with a cost cap and a remaining budget smaller than the next instance could spend
- **WHEN** an invocation reaches the cap
- **THEN** the agent exits with the cost-limit code, the orchestrator records the spend and the
  limit stop, and it starts no further instance

#### Scenario: The final spend is reported

- **GIVEN** a completed run
- **WHEN** the orchestrator writes the summary
- **THEN** it sums the priced usage from each trajectory (or the limit event's spend) and reports
  the per-arm and total spend against the cap, with `CultureInfo.InvariantCulture`-style decimal
  formatting in the published table

#### Scenario: An unpriced instance does not inflate the total

- **GIVEN** a run where one instance's trajectory yields no price
- **WHEN** the orchestrator writes the summary
- **THEN** it marks that instance as unpriced, stops before starting another instance, and reports
  the run total as unknown rather than as the cap or a value above it
