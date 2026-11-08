# agent-loop delta — run limits

## ADDED Requirements

### Requirement: Cost and wall-clock run limits

The loop SHALL enforce an optional cost limit and an optional wall-clock limit beside `MaxSteps`.
Both SHALL be off when unset.

The cost limit SHALL be a positive US-dollar amount. The loop SHALL price every usage record it
accounts — the run's own model calls, routed calls, decision calls and nested calls — from the
catalog when the provider does not report an amount, and from the provider-reported amount when it
does. A usage record that cannot be fully priced SHALL NOT be counted as zero: the record is
unpriced. With a cost limit configured and unpriced accounting present, the run SHALL refuse to
start unless the host set `AllowUnpricedCost`; with that option, unpriced records contribute
nothing and the limit applies to the priced spend only.

The limit SHALL be checked after every accounted usage and before every model request. The run
SHALL end when the cumulative spend is greater than or equal to the limit; the exact boundary
triggers. A limit end SHALL emit the matching limit event and then `RunFinished(cost_limit)`, with
no further model or tool call. The event SHALL carry the limit and the spend reached.

The wall-clock limit SHALL use one monotonic timer per run, started when the run starts. The run
SHALL end when the elapsed run time is greater than or equal to the limit, including while a model
call is in flight; the exact boundary triggers. It SHALL emit `TimeLimitReached` and then
`RunFinished(time_limit)` with no further work. The event SHALL carry the limit and the elapsed time
reached.

When a session is attached, the limit end SHALL be recorded in that session in a form a resume can
read back. A resumed run SHALL seed its spend from the session's recorded usages, so the cost limit
applies to the session's cumulative spend rather than resetting at restart.

#### Scenario: The cost limit triggers exactly at the boundary

- **GIVEN** a harness with a priced model and a cost limit equal to the cost of one recorded usage
- **WHEN** the run accounts exactly that usage
- **THEN** `CostLimitReached` carries the limit and the same spend, `RunFinished(cost_limit)` ends the run, and no further model or tool call happens

#### Scenario: Spend below the boundary does not trigger

- **GIVEN** a harness whose usage costs less than the configured cost limit
- **WHEN** the run finishes normally
- **THEN** no cost limit event is emitted and the stop reason is `stop`

#### Scenario: The wall-clock limit triggers exactly at the boundary

- **GIVEN** a harness with a wall-clock limit and a run whose elapsed time reaches exactly the limit
- **WHEN** the loop next checks the run
- **THEN** `TimeLimitReached` carries the limit and that elapsed time, and `RunFinished(time_limit)` ends the run

#### Scenario: A model call in flight is stopped by the timer

- **GIVEN** a wall-clock limit that expires while a model call is streaming
- **WHEN** the timer fires
- **THEN** the run emits `TimeLimitReached` and `RunFinished(time_limit)` and does not hang

#### Scenario: Unpriced accounting refuses a cost limit

- **GIVEN** a cost limit on a model with no known price and `AllowUnpricedCost` off
- **WHEN** the harness is built
- **THEN** it refuses with a message naming the model and the unpriced option

#### Scenario: The limit is recorded in the session

- **GIVEN** a sessioned run that reaches either limit
- **WHEN** the session file is read
- **THEN** an extension entry names the limit, its value and the spend or elapsed time reached

#### Scenario: A resumed session keeps its spend

- **GIVEN** a session recording a priced usage and a resumed harness whose cost limit equals that spent amount
- **WHEN** the resumed run starts
- **THEN** it ends with `cost_limit` before any model request, and the event's spend equals the recorded spend
