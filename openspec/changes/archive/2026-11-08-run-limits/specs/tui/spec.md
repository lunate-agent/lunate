# tui delta — run limits

## ADDED Requirements

### Requirement: Run-limit notices

The interactive session SHALL consume `CostLimitReached` and `TimeLimitReached` as warning notices.
Each notice SHALL name the limit, its configured value and the spend or elapsed time reached,
formatted with the invariant culture. The interactive session SHALL NOT set a cost or wall-clock
limit of its own; the limits come only from the resolved settings.

#### Scenario: A cost limit is a notice

- **GIVEN** a run that emits `CostLimitReached`
- **WHEN** the interactive session handles it
- **THEN** one warning notice names the cost limit and the spend reached

#### Scenario: A time limit is a notice

- **GIVEN** a run that emits `TimeLimitReached`
- **WHEN** the interactive session handles it
- **THEN** one warning notice names the time limit and the elapsed time reached
