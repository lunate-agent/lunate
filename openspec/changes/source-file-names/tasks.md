# Tasks — source-file-names

One slice: the renames and the test land together, because the test is what makes the renames stay
true. Refs #583.

## 1. Reproduce and pin

- [x] 1.1 Reproduce the finding at `origin/main` (`2649040c`): scan `src/` for files whose leading
      base-name segment is a type declared in a different file. Four hits, matching the review.
- [ ] 1.2 Add `tests/Lunate.Agent.Tests/SourceFileNameTests.cs`: for every `.cs` file under `src/`,
      the text before the first `.` of the base name must be a top-level type the file declares, or
      must not be a type name anywhere under `src/`; the failure message names the file and the file
      that declares the type.
- [ ] 1.3 Run it red against the four files and record the output on the PR.

## 2. Rename the four misnamed files

- [ ] 2.1 `src/Lunate.Agent/IFileChangeSink.cs` → `src/Lunate.Agent/RunFileChangeSink.cs`
      (`IFileChangeSink` stays in `Lunate.Contracts`).
- [ ] 2.2 `src/Lunate.Extensibility.Abstractions/Commands.cs` →
      `src/Lunate.Extensibility.Abstractions/CommandDescriptor.cs`.
- [ ] 2.3 `src/Lunate.Extensibility.Remote/RemoteExtensionStatus.cs` →
      `src/Lunate.Extensibility.Remote/RemoteExtensionException.cs`.
- [ ] 2.4 `src/Lunate.Coding/ChildProcess.Unix.cs` → `UnixChildProcess.cs` and
      `src/Lunate.Coding/ChildProcess.Windows.cs` → `WindowsChildProcess.cs`.
- [ ] 2.5 No type, namespace or public API changes; `git diff` shows renames only.

## 3. Verify

- [ ] 3.1 Run it green.
- [ ] 3.2 `dotnet csharpier format .` then `dotnet csharpier check .`.
- [ ] 3.3 Build `Lunate.Agent`, `Lunate.Coding`, `Lunate.Extensibility.Abstractions`,
      `Lunate.Extensibility.Remote` and `Lunate.Agent.Tests` in Release.
- [ ] 3.4 `openspec validate source-file-names --strict`.
- [ ] 3.5 Record the classes run and the acceptance answers on the PR.

## 4. Record

- [ ] 4.1 State on #583 that the two remaining same-named files (`DecisionRequestValidator.cs`,
      `InputTrace.cs`) are deliberate parallel implementations, not misnamed files, so the finding's
      second clause is answered for names, not for types.
- [ ] 4.2 Archive this change as the final commit.
