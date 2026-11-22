## ADDED Requirements

### Requirement: Remember project notes

The core tool set SHALL include `remember`, a write-risk tool that appends one durable project note. Its only argument SHALL be `note`, a non-empty single-line string; it SHALL take no path, so its target is never caller-chosen. It SHALL append the note as one Markdown bullet to `LUNATE.md` at the repository root, or at the worktree root when the repository root is not an ancestor of the worktree. A new file SHALL begin with a `# Project notes` heading; an existing file SHALL keep every byte it already has, gaining only a final line ending when it lacks one plus the new bullet. The append SHALL preserve the file's existing line endings and byte-order mark, or follow the project convention for a new file. On success it SHALL emit the normal file-change notification and a result naming the file, whether it was created, and its size against the budget.

The notes file SHALL hold at most 8,000 characters. An append that would exceed the budget SHALL be refused with an instructing error naming the current size, the budget and the way to proceed, and the file SHALL remain byte-identical. An empty or multi-line note, an unknown argument, or a note that alone cannot fit SHALL be refused without a write. The tool SHALL never write a global or user-level notes file.

#### Scenario: The first note creates the project file

- **GIVEN** a workspace with no `LUNATE.md`
- **WHEN** `remember` is called with `builds with .NET 11`
- **THEN** the repository root's `LUNATE.md` contains a `# Project notes` heading and the note as a bullet, and the result says the file was created

#### Scenario: Appending keeps existing notes

- **GIVEN** a `LUNATE.md` whose bytes already end with a note and a newline
- **WHEN** `remember` is called with another note
- **THEN** the previous bytes are unchanged and the new bullet follows them

#### Scenario: A missing final newline is repaired without rewriting a line

- **GIVEN** a `LUNATE.md` that does not end with a line ending
- **WHEN** `remember` is called
- **THEN** one line ending is inserted after the existing text and the new bullet follows it

#### Scenario: The budget refuses an append and leaves the file unchanged

- **GIVEN** a `LUNATE.md` already at the 8,000-character budget
- **WHEN** `remember` is called with a note that would exceed it
- **THEN** the call is an error naming the current size, the budget and how to proceed, and the file's bytes are unchanged

#### Scenario: Invalid notes are refused without a write

- **GIVEN** a workspace with no `LUNATE.md`
- **WHEN** `remember` is called with an empty note, a multi-line note or an unknown argument
- **THEN** the call is an error and no file is created

#### Scenario: The tool has no global target

- **GIVEN** any workspace
- **WHEN** `remember` is called
- **THEN** it writes only the repository or worktree root's `LUNATE.md`, never a user-level file
