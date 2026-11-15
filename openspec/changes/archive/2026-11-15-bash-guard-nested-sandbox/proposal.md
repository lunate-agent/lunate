## Why

Inside an existing macOS sandbox — a worker harness that runs the whole session under
`sandbox-exec` with its own profile — Lunate's guard probe fails, because macOS does not let a
process apply a nested sandbox profile. The guard then reports that the command "runs unconfined",
which is false (the outer profile still applies) and hides the one thing the user needs to know: the
guard's own state-root, install-prefix and `auth.json` rules were not added.

## What Changes

- The macOS guard resolution recognises the probe failure that means "this process already runs
  inside a sandbox" (`sandbox_apply: Operation not permitted`) and reports that instead of the
  generic unavailable message.
- The nested-sandbox notice says the process already runs under a sandbox, that the guard's own
  rules are not applied, and that the outer sandbox still applies; it never claims the command runs
  unconfined, and it carries the probe's output.
- The notice is emitted at most once per session, through the existing per-guard notice path, not
  on every command.
- `docs/security.md` names the nested-sandbox case in the bash-guard section.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `agent-tools`: the bash guard's unavailable report gains the nested-sandbox case, which names the
  outer sandbox and the guard's absent rules instead of stating that the command runs unconfined.

## Impact

- `src/Lunate.Coding/BashGuard.Platforms.cs` (macOS resolution and the notice text).
- `tests/Lunate.Coding.Tests/BashGuardTests.cs` (probe failure path and message) and the tool-level
  once-per-session expectation in `tests/Lunate.Coding.Tests/BashGuardToolTests.cs`.
- `docs/security.md` (bash-guard paragraph).
- No new dependency, no public API change, no change to the probe seam or the `bash` tool's output
  bytes. Linux's unavailable path is unchanged: the card is the macOS nested case.
