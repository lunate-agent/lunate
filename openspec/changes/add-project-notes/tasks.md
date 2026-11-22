# Tasks — add-project-notes

## 1. Notes discovery and prompt

- [x] 1.1 Add red tests in `tests/Lunate.Coding.Tests/SystemPrompt` for a missing file, root discovery from a nested launch, the `AGENTS.md`-then-notes order, the read truncation marker and the untouched no-notes prompt; confirm they fail.
- [x] 1.2 Add `ProjectNotes` with the root selection, the 8,000-character read bound and the prompt block, then append that block to the `{agents}` substitution in `SystemPrompt` and update `system-prompt.md`; rerun the focused tests.

## 2. Remember tool

- [x] 2.1 Add red tests in `tests/Lunate.Coding.Tests/Tools` for creating the file, appending without changing existing bytes, repairing a missing final newline, preserving line endings and BOM, the budget refusal leaving the file unchanged, invalid notes, the root-only target and the file-change notification; confirm they fail.
- [x] 2.2 Implement `RememberTool` with the hand-written schema, shared validator, append-only write and budget check.
- [x] 2.3 Add red tests for core registration and register `remember` in `HarnessFactory.CreateTools`; update the system-prompt parity expectation and rerun the focused tests.

## 3. Documentation and changelog

- [x] 3.1 Update `docs/agents-md.md` with the notes file, its precedence over facts and the budget behaviour; update the core-tool lists in `README.md`, `docs/how-lunate-works.md` and `docs/dev/guide.md`.
- [x] 3.2 Write the changelog fragment at `changes/<pr-number>.md` and commit it before the archive.

## 4. Verification

- [x] 4.1 Run `dotnet csharpier format .`, the touched Release builds and focused test classes, and `openspec validate add-project-notes --type change --strict`; fix failures.
- [x] 4.2 Run `openspec validate --all --strict`, then sync and archive the change as the branch's last commit.
