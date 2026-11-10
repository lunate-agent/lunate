## Context

`BashChangeTracker.ScanAsync` asks `git status --porcelain -z --ignore-submodules=dirty -unormal -- .`. With `-unormal`, git collapses an untracked directory into one `?? dir/` entry. `ParsePorcelain` skips entries whose path ends in `/`, so the snapshot never contains the files inside it. A file created in that directory later leaves the status output byte-for-byte unchanged, so `DiffAsync` sees nothing to publish.

The tracker already has the right baseline semantics: the first scan of a run records what is there, and later scans publish only paths whose status entry or content hash changed. The fix is to make the status output carry the files, not to add another diff layer.

## Goals / Non-Goals

**Goals:**

- Publish `Created` for a file a bash call creates inside a directory that was untracked before the call.
- Keep a file that was already inside that directory at baseline quiet.
- Keep the existing real-change rules: already-dirty files are not re-flagged, reverts publish, commits do not.

**Non-Goals:**

- No new file-watching service, no public API change, no change to the stamp-walk backstop.
- No attempt to publish directory entries themselves; there is no directory change kind.

## Decisions

- **Use `-uall` for the status scan.** Git then lists every untracked file individually, including those under an untracked directory, and existing content-hash diffing applies unchanged. It also keeps `.gitignore` filtering in git, which a hand-rolled directory walk would have to reproduce.
  - Alternative: expand `?? dir/` entries with a filesystem walk. It avoids git listing a large untracked tree but must re-implement ignore rules and the stamp walk's `bin`/`obj`/`node_modules` exclusions, and would disagree with git about what is untracked.
  - Alternative: use `-uall` only when a directory entry is present. That is two scan shapes for one question and adds a branch the tests would have to pin twice.
- **Leave `ParsePorcelain`'s trailing-slash skip in place** as a defensive parser rule for callers that still receive `-unormal`-shaped output, and keep its unit test. The scan itself now normally receives file paths.
- **Update the test that pinned the old skip.** It asserted that a file inside a newly created untracked directory was not published. The issue's expected behaviour explicitly covers "whether or not `dir` was already untracked", so that file is now published too; the new regression test covers the already-untracked baseline from the reproduction.

## Risks / Trade-offs

- **A very large untracked tree now produces a long status output.** -> The scan already has a 2-second window and falls back to the content-stamp walk on timeout; the existing per-file hash pass has the same shape of cost for the paths it sees. `bin`, `obj` and `node_modules` are normally ignored in this repository and in the Lua/test fixtures; if they are not ignored, git reports them as untracked and the spec already says untracked files are included.
- **A pre-existing untracked file inside an untracked directory becomes part of the baseline.** -> That is the intended snapshot behaviour: it is not published on the next scan unless its content actually changes, which the regression test asserts.

## Migration Plan

None. The change is internal to the scan; existing runs baseline on their next scan and no stored session or snapshot is persisted across processes.

## Open Questions

None. No in-force ADR is affected; this stays inside the existing file-change-bus and bash-change-scan contracts.
