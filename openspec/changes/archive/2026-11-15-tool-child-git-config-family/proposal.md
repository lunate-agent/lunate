## Why

The tool child's credential-shape strip withholds any variable whose name has a `KEY` part, and git's
environment configuration spells its third member `GIT_CONFIG_KEY_<n>`. A supervisor that configures
git for a session through `GIT_CONFIG_COUNT` / `GIT_CONFIG_KEY_<n>` / `GIT_CONFIG_VALUE_<n>` — the lane
does, with three entries including an empty `credential.helper` — therefore hands the tool child a
*partial* set: git sees `GIT_CONFIG_COUNT=3` but no `GIT_CONFIG_KEY_0` and fails every command with
`error: missing config key GIT_CONFIG_KEY_0` / `fatal: unable to parse command-line config`. The bash
change scan fails the same way, and the model's only way through is to unset every `GIT_CONFIG_*`,
which also drops the supervisor's git hardening (#871, ADR-0036's named false positive).

## What Changes

- The `Inherited` policy's credential-shape rule gains one named exemption: git's environment
  configuration — `GIT_CONFIG_COUNT`, `GIT_CONFIG_KEY_<n>` and `GIT_CONFIG_VALUE_<n>` for a numeric
  `<n>` — reaches the tool child whole. Git reads the keys and values of the first
  `GIT_CONFIG_COUNT` entries and refuses the run when one is missing, so a partial family is worse
  than none.
- The exemption is a fixed name family in `EnvironmentScrubber`, matched case-insensitively like the
  shape rule, applied to inherited names and to declared values alike. It is not a pattern the user
  can configure and it does not widen the pass-through list: `KEY`, `TOKEN`, `SECRET` and the other
  credential words still withhold `GITHUB_TOKEN`, `NPM_TOKEN`, `AWS_SECRET_ACCESS_KEY` and
  `*_API_KEY`.
- The bash change scan needs no separate fix — it spawns git through the same
  `ChildProcess.BuildEnvironment` — but it gets a test that pins it with the same configuration.
- The boundaries in `docs/dev/guide.md` say the git environment-configuration family is exempt, and
  a new ADR amends ADR-0036's rule.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `child-environment`: the inherited policy's credential-shape rule keeps git's environment
  configuration (`GIT_CONFIG_COUNT`, `GIT_CONFIG_KEY_<n>`, `GIT_CONFIG_VALUE_<n>`) whole, and the
  bash change scan reads the same policy.

## Impact

- `src/Lunate.Extensibility.Abstractions/EnvironmentScrubber.cs`: one predicate beside the word list;
  the shape deny and the declared-value deny both consult it.
- `docs/dev/guide.md`: the tool child's boundary sentence names the exemption.
- `adr/0038-tool-child-git-config-family.md`: the durable decision, amending ADR-0036.
- Tests: `EnvironmentScrubberTests` (the family survives, real credentials still do not),
  `ChildProcessToolEnvironmentTests` (a real bash child runs `git status` and `git commit` and sees an
  empty `credential.helper`), `BashChangeTrackerTests` (the scan reads the same configuration and
  answers from git, not from the stamp backstop).
- No public API change, no new package, no new setting.
