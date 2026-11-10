## Why

A bash call that creates a file inside a directory that was already untracked publishes nothing to the file-change bus, so extensions watching changes never see the new file. The scan asks git status with `-unormal`, which collapses an untracked directory into one entry; the tracker skips those directory entries, so a file appearing inside one is invisible.

## What Changes

- Ask git status for every untracked file (`-uall`) instead of collapsing untracked directories (`-unormal`), so a new file inside an already-untracked directory gets its own status entry and is diffed and published like any other new file.
- Keep the snapshot rules intact: files present when the run baselined are not republished, already-dirty files stay quiet without a real change, reverts publish and commits do not.
- Pin the issue's reproduction as a regression test and update the test that pinned untracked-directory files being skipped.

## Capabilities

### New Capabilities

### Modified Capabilities

- `extensions`: the file change bus's bash scan publishes new files inside untracked directories; only directory entries themselves stay unpublishable.

## Impact

- `src/Lunate.Coding/BashChangeTracker.cs` — the status scan's untracked mode.
- `tests/Lunate.Coding.Tests/BashChangeTrackerTests.cs` — regression coverage.
- No public API, package, session-format or golden-file change.
