## ADDED Requirements

### Requirement: Folders group files by feature and never change a namespace

A folder under `src/` SHALL group files by the feature they serve. A file's namespace SHALL be the
namespace its assembly established for it and SHALL NOT be derived from, or changed by, the folder it
sits in: moving a file between folders SHALL NOT change its declared namespace, its type names, its
members or its assembly. A folder name SHALL NOT appear as a namespace segment relative to its source
project, and no folder SHALL introduce a new namespace for the files it holds. The rule SHALL be
enforced by a source-scanning test that reads every file under `src/` and fails with the file, the
folder and the offending namespace.

#### Scenario: A moved file keeps its namespace
- **GIVEN** a file that declares `namespace Lunate.Tools;` under `src/Lunate.Coding/Tools/`
- **WHEN** the layout test walks `src/`
- **THEN** the file passes, because the folder is part of the path and not part of the namespace

#### Scenario: A folder that becomes a namespace fails the gate
- **GIVEN** a file under `src/Lunate.Protocols/Acp/` that declares `namespace Lunate.Protocols.Acp;`
- **WHEN** the layout test walks `src/`
- **THEN** the test fails, naming the file, the `Acp` folder and the namespace, until the file declares
  the assembly's namespace

#### Scenario: The namespace survives a move across assemblies
- **GIVEN** a type that moved from the `Lunate.Agent` assembly into `Lunate.Contracts`
- **WHEN** the layout test reads the file in `src/Lunate.Contracts/Events/`
- **THEN** the test accepts its historical `Lunate.Agent` namespace, because a namespace records
  compatibility and not location

### Requirement: Test projects mirror the folders of the projects they test

A test project under `tests/` SHALL mirror the feature folders of the project it tests: a test file
that exercises a type or a flow living in `src/<Project>/<Folder>/` SHALL sit in
`tests/<Project>.Tests/<Folder>/`. Test support files that belong to no single feature SHALL stay at
the test project root, and committed fixtures SHALL keep their existing `fixtures/` location. A new
feature folder in `src/` SHALL NOT need a new project or a new namespace for its tests.

#### Scenario: A feature folder has its tests inside it
- **GIVEN** the session store under `src/Lunate.Agent/Sessions/` and its tests
- **WHEN** a reader opens `tests/Lunate.Agent.Tests/`
- **THEN** the session tests sit in `tests/Lunate.Agent.Tests/Sessions/`, the mirrored path

#### Scenario: A cross-feature test names its home
- **GIVEN** a test that drives the session through tools and settings
- **WHEN** the test is placed
- **THEN** it sits in the folder of its subject — the thing whose behaviour the test names — and the
  other feature folders stay free of it
