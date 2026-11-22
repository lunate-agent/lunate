## Why

Nothing carries a fact from one session to the next except `AGENTS.md`, which is the repository's rules file and is not a place the agent should append to. A durable place for project facts — "builds with X", "the client prefers Y" — lets the agent stop re-learning the same thing every session ([#987](https://github.com/lunate-agent/lunate/issues/987)). A readable notes file comes first; embeddings over sessions wait until there is a memory feature to embed ([#981](https://github.com/lunate-agent/lunate/issues/981)).

## What Changes

- Add a project notes file, `LUNATE.md`, at the repository root (the worktree root when the repository root is not the run's own tree). It is discovered automatically and appended to the system prompt after the `AGENTS.md` chain.
- State the precedence: `AGENTS.md` rules come first and win when a note disagrees; notes are facts, not rules.
- Add a core `remember` tool that appends one note to the project's own `LUNATE.md`; it takes no path, so it can never write global or user state. It adds one Markdown bullet and preserves every existing byte except a missing final newline. A new file gets a `# Project notes` heading.
- Enforce one explicit budget: `LUNATE.md` holds at most 8,000 characters. The prompt shows the first 8,000 with a marker naming the omitted count; an append that would exceed the budget is refused, leaves the file unchanged and tells the model how to proceed.
- Show the write in the transcript through the normal tool call/result and file-change event; the session format does not change.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `agent-loop`: the system prompt also carries the project notes file after `AGENTS.md`, with the documented precedence and the read-side budget behaviour.
- `agent-tools`: add the core `remember` tool, its append-only contract, its root-only target and its budget refusal.

## Impact

- `src/Lunate.Coding`: a `ProjectNotes` reader/composer, the `RememberTool`, core registration in `HarnessFactory`, and the system-prompt template/harness.
- Tests: `tests/Lunate.Coding.Tests/SystemPrompt` for discovery/precedence/budget and `tests/Lunate.Coding.Tests/Tools` for append and refusal.
- Docs: the `AGENTS.md` page and the pages that enumerate the core tools.
- No new NuGet package, no settings key, no session-schema change and no golden-fixture change.
