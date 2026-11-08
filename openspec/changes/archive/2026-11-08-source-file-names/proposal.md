# Source file names that match the type they hold

## Why

The 0.3.0-rc.1 review (finding N-3, `acfb9da`) found source files whose name names a type the file
does not declare: `src/Lunate.Agent/IFileChangeSink.cs` holds `RunFileChangeSink`, while
`IFileChangeSink` lives in `Lunate.Contracts`. A reader who jumps to that file by name lands on the
wrong type, and a `Go to File` search for the contract's name offers two hits in two projects, only
one of which is the contract.

## What Changes

- Rename the four files under `src/` that are named after a type they do not declare, so each file
  is named after a type it holds:
  - `src/Lunate.Agent/IFileChangeSink.cs` → `RunFileChangeSink.cs` (`IFileChangeSink` stays in
    `Lunate.Contracts`).
  - `src/Lunate.Extensibility.Abstractions/Commands.cs` → `CommandDescriptor.cs` (the file holds the
    command contract family; the `Commands` type is `Lunate.Coding`'s).
  - `src/Lunate.Extensibility.Remote/RemoteExtensionStatus.cs` → `RemoteExtensionException.cs`
    (`RemoteExtensionStatus` stays in `Lunate.Extensibility.Abstractions`).
  - `src/Lunate.Coding/ChildProcess.Unix.cs` / `ChildProcess.Windows.cs` →
    `UnixChildProcess.cs` / `WindowsChildProcess.cs` (the files hold those two types, not
    `ChildProcess`).
- No type is renamed, moved or changed; no public API changes; no behaviour changes.
- A source-scanning test pins the rule so the next misnamed file fails a focused test instead of the
  next review.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `repo-quality`: one requirement added — a file under `src/` may only be named after a type it
  declares, and a test enforces it.

## Impact

- `src/Lunate.Agent`, `src/Lunate.Extensibility.Abstractions`, `src/Lunate.Extensibility.Remote`,
  `src/Lunate.Coding`: file renames only (4 files, 2 of them a platform pair).
- `tests/Lunate.Agent.Tests`: one new test class that walks `src/`.
- Nothing outside the repository: no package, no contract, no settings key, no documentation fact
  changes.
