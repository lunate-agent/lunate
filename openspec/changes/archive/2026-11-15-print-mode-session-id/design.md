## Context

`LaunchOptions.TryParse` (`src/Lunate.Coding/LaunchOptions.cs`) is the one launch parser for the
interactive session, print mode and ACP. Session selection is shared: `SessionSelection`
(`src/Lunate.Coding/SessionSelection.cs`) resolves `--continue` and `--session` for both frontends,
`PrintMode.ResolveSession` (`src/Lunate.Coding/PrintMode.cs`) and
`InteractiveSession.StartInitialSession` (`src/Lunate.Coding/InteractiveSession.Selection.cs`) consume
the result, and `HarnessFactory.CreateSession` writes a fresh file through `SessionProvisioning`,
which names the file after the session's own id. The generated id is
`s_<UTC yyyyMMdd-HHmmss>-<8 lowercase hex>`. `SessionPaths.SessionFileName` is the store's path-safety
authority: it refuses `/`, `\` and `..`.

A supervisor needs to name a run before it starts, find the process by `--session-id <id>` on its
command line, and continue the same name later (lunate#870). Nothing selects a session by a
caller-supplied id, and nothing creates a session under a chosen name.

```text
+---------------------------- lunate process ----------------------------+
|  argv: -p --session-id <id> "prompt"                                   |
|      |                                                                 |
|      v                                                                 |
|  LaunchOptions.TryParse ---- SessionSelection.ValidateId ---- grammar  |
|      |                                                                 |
|      v                                                                 |
|  PrintMode / InteractiveSession -- SessionSelection.IdPath --> <id>.jsonl
|      |                                    |                            |
|      | file exists: Session.Load          | missing: HarnessFactory    |
|      v                                    v  .CreateSession(id)        |
|  AgentHarness --------- RunStarted/RunFinished(sessionId)              |
+------------------------------------------------------------------------+
                                    |
                                    v
              ~/.lunate/sessions/<repo-hash>/<worktree-hash>/<id>.jsonl
```

## Goals / Non-Goals

**Goals:**

- `lunate -p --session-id <id> "…"` (and with `--json`) creates `<id>.jsonl` when absent and
  continues it when present, in the working directory's session directory.
- The id appears as `sessionId` on the run's events and on the process command line.
- An id outside the grammar, and a combination with another selection flag, is a usage error (exit 2).
- The flag behaves the same in the interactive session, so the shared parser never accepts a
  selection one frontend silently ignores.

**Non-Goals:**

- No change to `--session`, `--continue`, `--resume`, `--no-session` or their store-wide lookup.
- No new settings key, no public API, no session-format or golden-file change.
- No concurrency protocol for two processes choosing the same id at the same instant; the existing
  one-writer claim still refuses a second appender.

## Decisions

### 1. A grammar with one authority, validated in the parser

The id becomes a file name, so the grammar must be path-safe and must not read as a flag or a hidden
file. It is 1–128 characters from `[A-Za-z0-9._-]`, starting with a letter or digit, without `..`
(which mirrors `SessionPaths.SessionFileName`). `SessionSelection.ValidateId` is the one validator;
`LaunchOptions.TryParse` calls it while reading the value, so an invalid id exits 2 before any
composition, exactly like `--env` and `--thinking`. Print and interactive resolution call the same
path builder, so a direct `PrintMode.RunAsync` call in a test cannot smuggle an invalid id past the
parser.

Alternative rejected: accept any path-safe value. That would accept `..`-neighbours, leading dots,
spaces and non-ASCII, and would give the supervisor a usage error only after composition instead of
at the command line.

Alternative rejected: require the exact generated shape (`s_<stamp>-<hex>`). A supervisor names a
run for its own reasons (`lane-issue-870`, a UUID, a workflow id); the generated shape is a default,
not a contract.

### 2. The lookup is the working directory's session directory, not the store

`<sessionDirectory>/<id>.jsonl` with `sessionDirectory = SessionDirectory ?? DefaultSessionDirectory(workspace)`
is deterministic: present means continue, absent means create. `--session <id>` searches the whole
store and can be ambiguous; that error would make a supervisor's chosen name unusable, and a
store-wide match could continue another worktree's conversation. A same-named file elsewhere is
ignored and untouched, which is the same boundary `--continue` keeps for working directories.

Alternative rejected: store-wide `FindById` then create. It reintroduces the ambiguity error and the
cross-worktree read for no benefit.

### 3. Creation reuses `SessionProvisioning`, with an explicit id

`HarnessFactory.CreateSession` gains an optional session id and passes it to a new internal
`Session.Create(path, cwd, repo, worktree, sessionId, timeProvider)` overload, so the header keeps
the repository identity and worktree fields a fresh run records. Provisioning still writes a
provisional file and renames it to `<id>.jsonl`, so a failed creation leaves nothing behind. If the
destination exists by the time of the rename, the create fails with the existing `IOException` and
the frontend reports it naming the id; it never overwrites the other file.

Alternative rejected: a public `Session.Create` overload for the id. The repository's rule keeps the
session surface internal for the coding assembly, and this is only a test-and-host seam.

### 4. The flag is parsed for both frontends and honoured by both

The parser is shared, so accepting `--session-id` in one mode and ignoring it in the other would be
a silent no-op for a session identity. Interactive resolution mirrors print mode: load `<id>.jsonl`
when present (a load failure keeps the existing "notice then fresh session" behaviour, and the fresh
session gets a generated id rather than appending to a corrupt file), and create with the id when
absent. ACP refuses it through `HasSelection`, like every other selection flag.

Alternative rejected: print-mode-only with an interactive usage error. There is no technical reason
the interactive session cannot start under a chosen name, and the flags table documents one shared
surface.

### 5. No new event or session-format field

`AgentHarness` already stamps the session's id on `RunStarted`/`RunFinished` through
`AgentEventChannel(_options.Session?.SessionId)`, and the header already carries the id. Creating the
session with the chosen id is therefore the whole identity change; `--json` emits it unchanged and
the golden session files are untouched.

## Risks / Trade-offs

- [Two supervisors choose the same id concurrently] -> the loser's rename fails and exits 2 naming
  the id; the winner keeps the file. No silent overwrite, no duplicate writer (the claim refuses the
  second appender).
- [A corrupt `<id>.jsonl` blocks the name] -> print mode exits 2 naming the id; the interactive
  session reports it and starts a fresh generated id, never appends to the corrupt file. The user
  can repair or remove the file and rerun with the same name.
- [The grammar rejects a name a supervisor already uses] -> the usage error names the rule and the
  value; the rule admits letters, digits, `.`, `_` and `-`, which covers UUIDs, issue numbers and
  lane names.
- [Interactive and print drift] -> both call `SessionSelection.IdPath` and the same create overload;
  tests cover create and continue in both where the shared resolution changes.

## Migration Plan

Additive. No existing invocation changes meaning: `--session-id` was an unknown option (exit 2), and
no stored session id is affected. Rollback reverts the parser case and the two resolution branches.

## Open Questions

None. The card's acceptance and the shared-parser shape decide the surface; the in-force ADRs
(0002 layering, 0003 loop, 0015 session format) are respected — no new dependency, no public API, no
format change.
