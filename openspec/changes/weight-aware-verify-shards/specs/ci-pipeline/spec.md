# CI pipeline delta — the verify shard partition

## ADDED Requirements

### Requirement: Weight-aware verify shard partition

The test phase SHALL partition the solution's test projects across the shards by recorded wall time,
not by position in the project list. The unit of partition SHALL be one culture pass of one
`lunate.slnx` test project, so a shard runs a project's default-culture pass, its de-AT pass, or both.
The partition SHALL place units largest recorded weight first, each into the currently least-loaded
shard, with a deterministic tie-break, so that both culture passes of a project heavier than a shard's
fair share land in different shards and no shard carries a whole slow project.

The only input to the partition SHALL be `scripts/verify-shard-weights.tsv`: one recorded wall time per
test project, in seconds, for one culture pass. The table SHALL also record the wall time of shard 1's
non-test steps, which SHALL pre-load shard 1's assignment, because that leg pays publish, the startup
budget, the install and tool roundtrips, the formatting, documentation and public-API checks on top of
its tests. The partition SHALL use the same table in every lane, and a shard SHALL compute its own
assignment from it alone, never from another shard, so the shards of a lane agree without coordination.

A test project with no row in the table SHALL be assigned the default weight, so adding a test project
never fails the gate; a row naming a project that is not in `lunate.slnx` SHALL fail
`scripts/gate-tests.sh`, so the table cannot drift away from the solution. Weight errors SHALL affect
the balance only: every culture pass of every test project SHALL be assigned to exactly one shard
whatever the weights say.

The CI matrix SHALL run the Ubuntu and Windows lanes at the manual dispatch's shard count -- four
unless it picks another -- and SHALL keep the macOS lane at its cap of two shards, so macOS never gains
a third. At the Ubuntu and Windows count, no shard SHALL carry more than one third of its lane's summed
recorded weight, counting shard 1's non-test pre-load in both the shard and the lane. A dispatch that
picks fewer shards is a baseline measurement: below four the third cannot be met by any partition,
because two shards always leave one carrying at least half the lane. The macOS lane's acceptance is the
best wall time within two shards instead: its two-shard partition SHALL be no heavier -- in its busiest
shard's summed recorded weight, and in the wall time its heaviest unit per culture pass predicts -- than
the position partition it replaced. At every shard count `n` the busiest shard SHALL stay within the
longest-processing-time bound on the fair share, `(4n-1)/(3n)` of the lane's weight divided by `n`; at
four shards that is 31.25 %, which is stricter than the third. Every shard SHALL own at least one unit
of every culture pass at every shard count the CI matrix runs, because a shard that runs nothing is not
a shard and a shard that runs only one culture pass is not the gate.

`scripts/gate-tests.sh` SHALL enforce the partition: for each shard count in use it SHALL check that
the units are complete and non-overlapping, that no shard is empty, that the partition is
byte-identical across calls, and that the balance bound holds; it SHALL fail naming the shard and the
weight when any of them does not. It SHALL read every lane's shard count from the workflow and fail
when the Ubuntu or Windows lane is below four, where the third cannot hold, or when the macOS lane is
not at its two-shard cap; it SHALL report the Ubuntu and Windows pole against the third and the macOS
two-shard pole against the position partition it replaced.

Every gate run SHALL record the wall time of each test project it runs, next to the per-shard record
`scripts/shard-timing.sh` already writes, one record per shard and culture pass so that a shard which
ran both passes keeps both; `scripts/shard-weights.sh weights --from <dir>` SHALL fold such records
back into `scripts/verify-shard-weights.tsv`, taking a project's slower recorded pass, so a real run
refreshes the weights instead of anyone estimating them.

#### Scenario: A heavy project's culture passes land in different shards

- **GIVEN** a weight table whose heaviest project weighs more than a quarter of the recorded total
- **WHEN** the four-shard partition is computed
- **THEN** that project's default-culture pass and its de-AT pass are assigned to different shards, and the shard holding either one is not the shard holding the other

#### Scenario: No shard exceeds a third on Ubuntu and Windows

- **GIVEN** the recorded weight table
- **WHEN** the four-shard partition the Ubuntu and Windows lanes run is computed
- **THEN** every shard's weight, including shard 1's non-test pre-load, is at most one third of the summed weight of the lane's shards

#### Scenario: macOS keeps its two-shard cap

- **GIVEN** the CI matrix's verify legs
- **WHEN** the plan decides the legs for a push, the nightly or a manual dispatch
- **THEN** `ubuntu-latest` and `windows-latest` each have the dispatch's shard count - four unless the dispatch picks another - and `macos-latest` has two shards, never a third

#### Scenario: macOS's weight partition is no worse than the position partition within two shards

- **GIVEN** the recorded weight table and the two-shard partition the macOS lane runs
- **WHEN** `scripts/gate-tests.sh` compares it with the position partition it replaced
- **THEN** the weight partition's busiest shard carries no more recorded weight, and its heaviest unit per culture pass predicts no more wall time, and the gate reports both numbers

#### Scenario: A lane given the wrong shard count fails the gate

- **GIVEN** a workflow whose macOS lane runs three shards, or whose Ubuntu or Windows lane runs two
- **WHEN** `scripts/gate-tests.sh` reads the lanes' shard counts
- **THEN** it fails and names the lane, because macOS's cap is two and the third leaves no room below four shards

#### Scenario: The partition is complete and non-overlapping for every shard count

- **GIVEN** a shard count of 1, 2, 3 or 4
- **WHEN** the shards' assignments are collected
- **THEN** every culture pass of every `lunate.slnx` test project appears in exactly one shard, and every shard owns at least one unit of each pass

#### Scenario: The partition is deterministic

- **GIVEN** the same weight table and shard count
- **WHEN** the assignment is computed twice
- **THEN** both runs print the same shards in the same order, on every platform

#### Scenario: A new test project gets the default weight instead of failing the gate

- **GIVEN** a test project in `lunate.slnx` with no row in the weight table
- **WHEN** the partition is computed and the gate self-tests run
- **THEN** the project is assigned the default weight, it appears in exactly one shard per culture pass, and the gate does not fail

#### Scenario: A weight row for a project the solution no longer has fails the gate

- **GIVEN** a weight table row naming a test project that is not in `lunate.slnx`
- **WHEN** `scripts/gate-tests.sh` runs the partition checks
- **THEN** it fails and names the stale row, so the table is refreshed with the project list

#### Scenario: A real run records per-project wall times

- **GIVEN** a gate run that executed test projects
- **WHEN** the run finishes
- **THEN** `artifacts/verify-timing/` holds a per-project record naming each project's wall time beside the shard's own record, and `scripts/shard-weights.sh weights --from` folds it into a weight table

#### Scenario: A shard's two culture passes are both kept

- **GIVEN** a shard that ran its default-culture pass and its de-AT pass
- **WHEN** the run records the per-project wall times
- **THEN** it writes one record for each pass, neither pass replacing the other, and the fold takes the project's slower recorded pass
