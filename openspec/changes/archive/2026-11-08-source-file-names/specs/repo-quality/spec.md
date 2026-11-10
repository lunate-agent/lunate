## ADDED Requirements

### Requirement: Source file names name a type the file declares

Every C# source file under `src/` SHALL be named after a type it declares, or after no type at all.
The leading segment of a file's base name — the text before the first `.`, so a partial-class file
such as `AgentHarness.Retries.cs` is judged by `AgentHarness` — SHALL NOT be the name of a type that
the file does not declare, so a file name never resolves to a type another file owns. A file whose
leading segment is not a type name anywhere under `src/` is an aggregate file and is allowed. The
repository SHALL enforce the rule with a test that scans `src/` on every platform, reads the
top-level type declarations of each file, and names every offending path together with the file that
declares the type.

#### Scenario: A file named after another file's type fails

- **GIVEN** a file under `src/` whose leading base-name segment is a type declared in a different file
- **WHEN** the source file name test runs
- **THEN** it fails and names both the offending file and the file that declares that type

#### Scenario: A partial-class file passes

- **GIVEN** `src/Lunate.Agent/AgentHarness.Retries.cs` declaring `partial class AgentHarness`
- **WHEN** the source file name test runs
- **THEN** it passes, because the file declares the type its name leads with

#### Scenario: An aggregate file passes

- **GIVEN** a file whose leading base-name segment is not a type name anywhere under `src/`, such as
  `src/Lunate.Extensibility.Abstractions/Hooks/HookPayloads.cs`
- **WHEN** the source file name test runs
- **THEN** it passes, because the name claims no type

#### Scenario: The renamed files pass on every platform

- **GIVEN** `RunFileChangeSink.cs`, `CommandDescriptor.cs`, `RemoteExtensionException.cs`,
  `UnixChildProcess.cs` and `WindowsChildProcess.cs`
- **WHEN** the source file name test runs on POSIX or Windows
- **THEN** it passes, and no file under `src/` is named after a type it does not declare
