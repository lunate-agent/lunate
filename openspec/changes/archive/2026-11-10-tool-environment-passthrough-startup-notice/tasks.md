# Tasks — tool-environment-passthrough-startup-notice

## 1. Red: the resolved-settings path says nothing today

- [x] 1.1 In `ToolEnvironmentPrintModeTests`, change the user-owned case to assert the resolved
      settings' startup output names `MY_PROJECT_TOKEN` and says it is admitted into tool processes
      (today it asserts silence). Run it: it must fail on the missing line.
- [x] 1.2 Add a focused `ToolEnvironmentPassthroughTests` case for the admission notice text (names
      in order, empty list empty). Run it: it must fail to compile before the helper exists.

## 2. Green: one line at startup in all three frontends

- [x] 2.1 Add `AdmissionNotice(names)` to `ToolEnvironmentPassthrough`: the exact admitted names in
      configured order, empty for an empty list, no values.
- [x] 2.2 `InteractiveSession.ApplyToolEnvironment`, `PrintMode.ApplyToolEnvironment` and
      `AcpMode`'s resolve branch emit the notice at `NoticeLevel.Info` when the list is non-empty,
      keeping the existing collision warning unchanged.
- [x] 2.3 Keep the collision case: a listed credential name yields both the admission line and the
      one warning. Update `ToolEnvironmentPrintModeTests`' collision case to expect both lines.
- [x] 2.4 Run the focused classes: `ToolEnvironmentPrintModeTests`, `ToolEnvironmentPassthroughTests`,
      `ChildEnvironmentGuardTests`, plus the interactive/ACP classes that reference the changed
      call sites. All green.

## 3. Docs state the consequence

- [x] 3.1 `settings-schema.json`'s `toolEnvironment.passthrough` description gains the consequence
      sentence (the model can read an admitted variable from a command it runs); regenerate
      `docs/configuration.md` with `scripts/docs-sync`.
- [x] 3.2 `docs/guide.md`'s pass-through bullet gains the same consequence sentence, keeping the
      user-scope, exact-name and collision-warning sentences.
- [x] 3.3 `python3 scripts/docs-sync --check` is clean.

## 4. Finish

- [x] 4.1 `dotnet csharpier format .`, then `dotnet csharpier check .`; Release build; focused tests.
- [x] 4.2 `openspec validate tool-environment-passthrough-startup-notice --strict` and
      `bash scripts/check-openspec-skills.sh`.
- [x] 4.3 Sync the delta spec into `openspec/specs/` and archive the change as the final commit.
