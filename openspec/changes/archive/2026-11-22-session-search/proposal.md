## Why

A past turn can only be found by recency: `--continue` and the session picker order sessions by
their last write, and nothing looks inside them. "What did we decide about X last week" is a manual
`grep` over the state directory. The session JSONL already holds every turn, so the missing piece is
a search over it and a way to land on the turn a hit names.

## What Changes

- `lunate sessions search <query>` scans the session store linearly and prints every matching turn —
  session id, turn timestamp, the session's `cwd`, the surrounding message text and the session file
  path — or the same rows as JSON with `--json`. A run with no match prints that it found nothing and
  exits 0; a malformed JSONL line is skipped and counted, never fatal.
- The scan states and enforces its bound: at most 200 session files and 8 MiB of message text per
  invocation, at most 20 matches. No index, no second store, no cache: the JSONL files are the
  corpus.
- The session picker (`/resume`, `--resume`) gains a search field: typing filters by turn content,
  one row per matching turn (`timestamp session-id — snippet`), and confirming a row resumes that
  session with the matching entry as its active leaf, so the conversation continues from that turn.
  Clearing the query returns to the ordinary newest-first listing.
- The store gains one read-only query (a linear scan of `*.jsonl` under the sessions root) that both
  surfaces share; searching never creates, appends to or rewrites a session file.
- Docs: `docs/cli.md` and `docs/usage.md` name the command, the bound and the picker's search field;
  `lunate --help` lists the subcommand.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `agent-sessions`: the store serves a bounded, read-only full-text search over past sessions' turns
  and reports its bound; the picker listing it feeds can be searched by turn text.
- `tui`: the session picker gains a search field over turn content, and confirming a hit opens the
  session at that turn.

## Impact

- `src/Lunate.Agent/Sessions/Session.Search.cs` — the scan and its result records (internal; the
  coding host is a friend assembly, so no `PublicAPI` entry changes).
- `src/Lunate.Coding/Commands/SessionsCommand.cs` — the `sessions search` subcommand, wired in
  `Cli.Run` and `CliHelp`.
- `src/Lunate.Coding/Tui/PickerOwner*.cs` and `src/Lunate.Coding/Sessions/InteractiveSession.Selection.cs`
  — the session picker's search field and the pick value that names an entry.
- Tests: a fixture state directory under `tests/fixtures/session-search/`, a store test for match /
  no match / malformed line, the CLI command test, and a picker test that a search hit resumes at the
  matching turn.
- Docs: `docs/cli.md`, `docs/usage.md`. No new NuGet packages, no new public API.
