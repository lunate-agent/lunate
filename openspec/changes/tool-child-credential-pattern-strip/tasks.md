# Tasks — tool-child-credential-pattern-strip

## 1. Prove the gap (test first)

- [x] 1.1 Add failing `EnvironmentScrubberTests` cases for the shape deny under `Inherited`: an
      unresolved `GITHUB_TOKEN` / `NPM_TOKEN` / `AWS_SECRET_ACCESS_KEY` is withheld; `MONKEY`,
      `KEYBOARD_LAYOUT`, `HOCKEY_POOL`, `GITLAB_TOKENIZE_TIMEOUT` and `OPENAI_BASE_URL` survive; a
      case-folded `github_token` is withheld; a listed shaped name is admitted; a declared value
      cannot put a withheld name back; `Scrubbed` is unaffected. Run them against the current tree.
- [x] 1.2 Extend `ChildProcessToolEnvironmentTests` so the real POSIX and Windows child does not read
      a withheld `GITHUB_TOKEN` and does read a listed `MY_PROJECT_TOKEN`; replace the old
      `OPENAI_API_KEY_BACKUP` survivor with a name the rule does not match.

## 2. The rule in the one builder

- [x] 2.1 Implement the credential-shape rule in `EnvironmentScrubber` (`Inherited` only): split the
      name on `_` and `-`, compare each part case-insensitively against the fixed word list, withhold
      an inherited variable and a declared value that matches unless the pass-through names it.
- [x] 2.2 Update the XML remarks of `EnvironmentScrubber.Build`, `ChildProcess.BuildEnvironment` and
      `CredentialVariables` so the resolved-name strip and the shape deny are not confused.
- [x] 2.3 Update the tests that pinned the old "only resolved names are withheld" rule.

## 3. Docs and reference

- [x] 3.1 `docs/guide.md`: the tool child's boundary sentence gains the shape rule, its word list and
      the pass-through escape hatch.
- [x] 3.2 `src/Lunate.Coding/settings-schema.json`: the `toolEnvironment.passthrough` description
      says a shaped name is admitted the same way; regenerate `docs/settings.md` with
      `scripts/docs-sync`.
- [x] 3.3 `adr/0036-tool-child-credential-shape-strip.md`: the durable decision (drafted with the
      adr artifact).

## 4. Verify and finish

- [x] 4.1 Focused test classes green: `EnvironmentScrubberTests`, `ChildProcessToolEnvironmentTests`,
      `CredentialVariablesTests`, `ToolEnvironmentPassthroughTests`,
      `ChildEnvironmentGuardTests`, `SettingsSchemaTests`.
- [x] 4.2 `dotnet csharpier format .` then `check`, a Release build of the touched projects,
      `openspec validate --all --strict`, `scripts/check-adr-numbers.py` and
      `scripts/check-adr-status.py`.
- [x] 4.3 Write `changes/<pr>.md`, sync the delta spec into `openspec/specs/`, and archive the
      change as the final commit.
