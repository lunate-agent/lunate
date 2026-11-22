## Context

`AGENTS.md` is the repository's rules file, read by `AgentsInstructions` from the repository root down to the working directory and appended to the system prompt (`src/Lunate.Coding/SystemPrompt/AgentsInstructions.cs`, `SystemPrompt.cs`). It is not a place the agent may append to: a rule change is a human edit, and rewriting it would overwrite intent.

The core tools are registered in `HarnessFactory.CreateTools` and described by the `agent-tools` spec; the loop runs them itself (ADR-0003), and every write emits a `FileChangeEvent` whose sink is stamped per run. The system prompt and its project-instruction block belong to the `agent-loop` spec. The workspace boundary is ADR-0016: `Workspace.RepoRoot` may be the main checkout while `Workspace.WorktreeRoot` is a linked worktree, so `AgentsInstructions` falls back to the worktree when the root is not an ancestor.

This change adds a readable, project-local memory file next to those mechanisms. It does not add embeddings, a database, a settings key or a session-format change.

## Goals / Non-Goals

**Goals:**

- Discover one notes file from the repository root and put it in the system prompt after the `AGENTS.md` chain.
- Make the precedence unambiguous: `AGENTS.md` rules first, notes after; a note never overrides a rule.
- Let the model append one note without choosing or rewriting the file, so unrelated content survives byte-for-byte.
- Give the notes an explicit size budget with a stated read behaviour and a stated append refusal.
- Show the append in the transcript through the ordinary tool call/result and file-change event.

**Non-Goals:**

- Global or user-level memory; a second notes file in a subdirectory; per-note settings.
- Semantic search, embeddings or session recall ([#981](https://github.com/lunate-agent/lunate/issues/981) leaves that for later).
- Editing or pruning notes with `remember`; the model may still use `edit` when it is allowed to write the file.
- A session schema or golden-fixture change: the notes are composed at session build time like `AGENTS.md`.

## Decisions

### The file is `LUNATE.md` at the repository root

The name is product-specific, readable and does not collide with `AGENTS.md`. The root is `workspace.RepoRoot` when it is an ancestor of `workspace.WorktreeRoot`, otherwise the worktree root. That mirrors `AgentsInstructions` fallback: a launch from a subdirectory of the main checkout reaches the repository root, while a linked worktree writes its own notes instead of the main checkout's.

Only the root file is discovered. A chain would need a second precedence rule (which nested note wins, and which file `remember` appends to) without serving the card. `remember` takes no path, so there is exactly one target and no way to select global state.

Alternative considered: reuse `AGENTS.md` itself. Rejected: it is the human-owned rules file and the card asks for a place the agent writes.

### The prompt carries notes after the `AGENTS.md` chain

`ProjectNotes` reads `LUNATE.md` and returns one block headed `### Project notes (LUNATE.md)` with a sentence that facts here are subordinate to the `AGENTS.md` rules. `SystemPrompt` appends that block to the `AGENTS.md` blocks and substitutes the combined value through the existing `{agents}` token, so neither user text is rescanned for tokens; the template prose introduces the notes when they exist, and an empty result leaves the prompt bytes unchanged apart from the trim `SystemPrompt` already performs.

The block is present only when the file exists, so a session without notes composes exactly as today. The notes are user content like `AGENTS.md`: they are excluded from the under-1,000-token core-prompt assertion, and a resumed session gets the current file because the prompt is composed when the harness is built.

### The budget is 8,000 characters, counted once

The file holds at most 8,000 characters (UTF-16 code units, the unit the rest of the prompt composer counts). On the read side, the prompt carries the first 8,000 characters and a marker naming the omitted count and the file for the rest; the cut never splits a surrogate pair. On the write side, an append whose resulting file would exceed 8,000 is refused with an error naming the current size, the budget and the remedy (shorten the note or edit `LUNATE.md` to remove stale notes), and the file is not written.

The budget is fixed rather than a settings key: the card asks for an explicit budget, and a settings key would need schema, validation, documentation and a generated-reference change that the feature does not need yet. 8,000 characters is roughly 2,000 estimated tokens, enough for dozens of one-line facts without competing with the session history.

Alternative considered: drop the oldest notes when full. Rejected: that rewrites content the agent did not author, and it makes an append silently destructive. Refusal is visible and leaves the decision to the model or the person.

### `remember` appends one bullet and never rewrites a line

`RememberTool` implements `ITool` with one required `note` string. It trims surrounding whitespace and refuses an empty or multi-line note, an unknown argument and a note that cannot fit. It appends `- <note>` to the root `LUNATE.md` using the file mutation queue and the same raw text/file-access path as `WriteTool`: an existing file keeps its bytes, ending style and BOM, gaining only a missing final line ending before the bullet; a new file starts with `# Project notes` and follows the project's line-ending and BOM policy.

The tool resolves its target through `ProjectNotes` and canonicalizes it; a symlink whose real target leaves the notes root is refused, so the fixed target is still a boundary. On success it emits a `FileChangeEvent` (`Created` or `Modified`) and returns a result naming the file, the created/replaced state and the character count against the budget. No new event kind or session entry is needed; the existing tool call and result are the transcript record.

`ToolRisk.Write` matches `write`/`edit`, so the normal approval policy applies and the append is visible like any other file change.

### Component and flow view

```text
 Model
   │ remember(note)
   ▼
 Lunate.Coding.RememberTool ──append only──> <repo root>/LUNATE.md
   │                                             │
   │ ToolResult + FileChangeEvent                │ read at harness build
   ▼                                             ▼
 session transcript (existing tool entries)   SystemPrompt ── {notes} after {agents}
```

The boundary is deliberate: the tool owns the write, `ProjectNotes` owns discovery and the read budget, and the prompt composer owns precedence. No frontend parses the notes file.

## Risks / Trade-offs

- [Notes grow until appends are refused] -> The refusal names the budget and the remedy; the prompt read marker still exposes the first 8,000 characters.
- [A subdirectory launch writes above the workspace root] -> The target is fixed at the repository root and only this one file, not a caller-chosen path; a symlink that escapes the root is refused.
- [A note duplicates an existing fact] -> Appending is deliberately simple; deduplication would need a semantic rule and is out of scope.
- [Notes can contain stale or hostile text] -> They are user/repository content like `AGENTS.md`; they are explicitly subordinate to rules, and the model can read the whole file on demand.
- [The tool list changes for every frontend] -> Core registration feeds the existing parity test and the `--tools`/prompt lists; no frontend-specific path is added.

## Migration Plan

Additive. Existing sessions and fixtures have no notes file and no `remember` call, so their prompts and files are unchanged. Rollback is removing the tool registration and the `{notes}` substitution; an existing `LUNATE.md` remains a harmless Markdown file.

## Open Questions

None. The card fixes the intent; the file name, root selection, precedence, 8,000-character budget and bullet format are the judgement calls recorded above. No in-force ADR is revisited: ADR-0003 (the loop runs tools), ADR-0012 (tool contract), ADR-0016 (workspace boundary) and ADR-0015 (session format) are followed, not changed.
