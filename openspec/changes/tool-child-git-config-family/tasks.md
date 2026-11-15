# Tasks — tool-child-git-config-family

## 1. Prove the gap (test first)

- [ ] 1.1 Add a failing `EnvironmentScrubberTests` case: an `Inherited` build over a parent holding
      `GIT_CONFIG_COUNT=3`, `GIT_CONFIG_KEY_0..2` and `GIT_CONFIG_VALUE_0..2` (with
      `GIT_CONFIG_VALUE_0` empty) plus `GITHUB_TOKEN`, `NPM_TOKEN` and `AWS_SECRET_ACCESS_KEY` keeps
      all seven `GIT_CONFIG_*` names and withholds the three credentials. Run it against the current
      tree and confirm it fails on the withheld `GIT_CONFIG_KEY_<n>`.
- [ ] 1.2 Add a failing `EnvironmentScrubberTests` case for a declared `GIT_CONFIG_KEY_0` value: it
      reaches the child, while a declared `GITHUB_TOKEN` still does not.
- [ ] 1.3 Add a failing `ChildProcessToolEnvironmentTests` case: a real POSIX `bash` child under the
      lane's three-entry configuration runs `git status` and `git commit` in a temp repository and
      reads an empty `credential.helper`; a real Windows child pins the same three names arriving.
- [ ] 1.4 Add a failing `BashChangeTrackerTests` case: with the same configuration in the process
      environment, the post-bash scan answers from git (`ScanSource.Git`, a real change published)
      rather than from the content-stamp backstop. It sets process-wide names, so it lives in its own
      `DisableParallelization` collection and restores every name in a `finally`.

## 2. The family in the one builder

- [ ] 2.1 `EnvironmentScrubber`: one predicate beside the word list that recognises
      `GIT_CONFIG_COUNT`, `GIT_CONFIG_KEY_<n>` and `GIT_CONFIG_VALUE_<n>` (numeric `<n>`,
      case-insensitive); both the inherited shape deny and the declared-value deny consult
      `IsCredentialShaped(name) && !IsGitConfigurationName(name)`.
- [ ] 2.2 Update the XML remarks of `EnvironmentScrubber` (the shape rule's and `Build`'s) and of
      `ChildProcess.BuildEnvironment` so the one exemption is stated where the rule is.

## 3. Docs and decision record

- [ ] 3.1 `docs/dev/guide.md`: the tool child's boundary paragraph names the git
      environment-configuration exemption and why a partial family breaks git.
- [ ] 3.2 `adr/0038-tool-child-git-config-family.md` and the change's `adr.md` manifest (drafted with
      the adr artifact; ADR-0036 is amended, never edited).

## 4. Verify and finish

- [ ] 4.1 Focused test classes green: `EnvironmentScrubberTests`,
      `ChildProcessToolEnvironmentTests`, `BashChangeTrackerTests`, `ChildEnvironmentGuardTests`.
- [ ] 4.2 `dotnet csharpier format .` then `check`, Release builds of the touched projects (the test
      projects included), the fast gate's documentation and ADR checks, and
      `openspec validate --all --strict`.
- [ ] 4.3 Write `changes/<pr>.md`, sync the delta spec into `openspec/specs/`, and archive the change
      as the final commit.
