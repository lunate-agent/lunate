## MODIFIED Requirements

### Requirement: Per-tool concurrency
`ITool` SHALL gain a `Concurrency` member (`Parallel`, default, or `Sequential`) pinning the tool's scheduling intent for the parallel executor. The loop SHALL honour it when it schedules a turn's all-read-only batch: `Parallel` calls may run concurrently under the loop's cap, while one `Sequential` call makes the whole batch serial and in call order.

#### Scenario: The default is parallel
- **GIVEN** a tool without a concurrency declaration
- **WHEN** its concurrency is read
- **THEN** it is `Parallel`

#### Scenario: A sequential tool keeps the batch in order
- **GIVEN** a turn whose calls are all read-only but one tool declares `Concurrency = Sequential`
- **WHEN** the loop schedules the batch
- **THEN** it runs the calls one at a time in call order
