## MODIFIED Requirements

### Requirement: Skill sources from extensions

Extensions SHALL contribute skills through a new `ISkillSource` service registered like other extension services: an id plus an enumeration of skill directories **on disk inside the extension's own folder** (v1 is disk-only — no in-memory skills; the core scans and validates them exactly like any other skill). Contributed skills SHALL appear in the skills index and `/skills` tagged `extension '<id>'`, SHALL inherit the extension's trust (no separate question) **and its disable state** (`disable-model-invocation` honoured), and SHALL count under the contributing extension's context budget (enforced before the global index cap). A malformed descriptor SHALL warn like a core skill and SHALL NOT load. A shadowed extension skill follows the common collision rule: the winner carries the `shadows` marker, the one-time warning names it, and the extension copy is not listed separately. The ADR-0017 catalogue and `docs/dev/spec/extensibility.md` SHALL gain the resource-discovery entry; the record lands as `adr/0017-addendum-skills.md`.

#### Scenario: A contributed skill joins the index tagged by source

- **GIVEN** a trusted extension registering an `ISkillSource` with one skill directory inside its folder
- **WHEN** a session starts
- **THEN** the index and `/skills` list the skill with `extension '<id>'` as its scope, and the read tool can read its files

#### Scenario: Over-budget contributions drop and log

- **GIVEN** an extension whose contributed index lines exceed its context budget
- **WHEN** the session starts
- **THEN** exceeding lines are dropped and an extension log entry names the extension and the budget

#### Scenario: An untrusted extension contributes nothing

- **GIVEN** an untrusted project extension registering a skill source
- **WHEN** the session starts
- **THEN** its skills are absent from the index and `/skills`

### Requirement: The Roslyn built-in preserves the recorded C# tool contract and budgets

Migration SHALL preserve exactly the model-facing tool names `cs_diagnostics`, `cs_find_symbol`,
`cs_find_references`, `cs_outline` and `cs_rename`. For the same committed fixtures and inputs, each
tool's `ToolResult.Output` and `IsError` SHALL match the recorded pre-migration baseline, and every
behavior already specified by the C# tool requirements in `agent-tools` SHALL remain true.
The existing golden-session and end-to-end tests SHALL run unchanged as proof; a golden SHALL NOT be
rewritten to make a diff pass.

The migration SHALL record the baseline in `docs/dev/eval/roslyn-built-in-migration-baseline.md` before
changing composition and compare the migrated build against it using the same fixture, runtime and
measurement method. The evidence SHALL include the startup floor and median, tool output and error
flags for all five tools, idle process-tree memory, and Roslyn process-tree peak memory after loading
the reference solution and after ten edit-to-diagnostics cycles. The startup floor SHALL remain
within the existing platform gate and shall not load Roslyn or MSBuild assemblies before the first
`cs_*` call. A startup test SHALL assert that no `Microsoft.CodeAnalysis.*` or `Microsoft.Build.*`
assembly is loaded before first C# use. Idle memory SHALL remain within the existing ADR-0009 gate, and the measured Roslyn
peak SHALL remain within its existing reference-workspace gate of 750 MB; the built-in workspace
cap SHALL remain two. The evidence record SHALL identify the baseline and migrated commits,
runtime, OS/RID, fixture and measurement commands; deterministic result fixtures SHALL be committed
under `tests/fixtures/`.

#### Scenario: All five tools match the recorded baseline

- **GIVEN** the same fixture workspace and recorded input for each C# tool
- **WHEN** the pre-migration and migrated builds execute those calls
- **THEN** the declarations are exactly the five `cs_*` names and each `Output` and `IsError` value
  matches the committed baseline evidence

#### Scenario: Startup stays lazy and inside its floor gate

- **GIVEN** a published migrated binary that has not called a C# tool
- **WHEN** the existing startup-floor measurement and lazy-loading check run
- **THEN** the floor remains within the platform budget, no `Microsoft.CodeAnalysis.*` or
  `Microsoft.Build.*` assembly is loaded before first C# use, and the measured floor and median are
  recorded beside the pre-migration baseline

#### Scenario: Roslyn memory stays within its recorded gate

- **GIVEN** the reference solution and the existing two-workspace cap
- **WHEN** peak process-tree memory is measured after load and after ten edit-to-diagnostics cycles
- **THEN** the Roslyn peak remains within 750 MB per reference workspace and both measurements are
  recorded against the pre-migration baseline
