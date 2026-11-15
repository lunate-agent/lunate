# Tasks — configurable-step-limit

## 1. Settings

- [x] 1.1 Add a failing test in `tests/Lunate.Coding.Tests/SettingsStoreTests.cs`: `maxSteps` resolves
      from the settings file, is 50 when missing, and a non-integer, zero or negative value refuses
      the file naming `maxSteps`. Extend the defaults test.
- [x] 1.2 Add `AgentSettings.MaxSteps` (default 50), the `maxSteps` case and `ReadMaxSteps` in
      `src/Lunate.Coding/SettingsStore.cs`, and merge `launch.MaxSteps ?? settings.MaxSteps` in
      `ApplyLaunch`.
- [x] 1.3 Add the `maxSteps` entry to `src/Lunate.Coding/settings-schema.json` and its probe in
      `tests/Lunate.Coding.Tests/SettingsSchemaTests.cs`; assert the resolved value in the
      every-key test.

## 2. Launch flag

- [x] 2.1 Add failing launch-parser tests in `tests/Lunate.Coding.Tests/CliTests.cs` for
      `--max-steps 55` (space and inline) and the usage errors for a missing value, `0`, a negative
      number and a non-integer.
- [x] 2.2 Add `LaunchOptions.MaxSteps`, the `--max-steps` case and `RunLimits.TryParseSteps`
      (invariant culture, `>= 1`), and the `--max-steps <n>` line in `src/Lunate.Coding/CliHelp.cs`.

## 3. Harness wiring and the run test

- [x] 3.1 Add a failing print-mode test in `tests/Lunate.Coding.Tests/PrintModeTests.cs`: with
      `--max-steps 55` and `LoopingChatClient`, the run makes 55 tool calls, emits
      `step_limit_reached` with `maxSteps: 55` before `run_finished` with `stopReason: step_limit`,
      and exits 2. Add a settings-file variant if the first does not cover the merge.
- [x] 3.2 Add a failing interactive test in
      `tests/Lunate.Coding.Tests/InteractiveSessionStreamingTests.cs`: a settings file with
      `"maxSteps": 1` and a looping tool call show the `step limit reached (1 steps)` notice.
- [x] 3.3 Pass `MaxSteps = settings.MaxSteps` into the `AgentHarnessOptions` of `PrintMode`,
      `InteractiveSession` and `AcpMode`.

## 4. Documentation

- [x] 4.1 Update `docs/usage.md` (Run limits), `docs/cli.md` (session options) and `docs/print.md`
      (cap the run) to name the 50 default, `--max-steps <n>` and `maxSteps`.
- [x] 4.2 Run `scripts/docs-sync` and commit the regenerated `docs/settings.md` row.

## 5. Gate and archive

- [x] 5.1 Run `dotnet csharpier format .`, `dotnet csharpier check .`, build `Lunate.Coding` and its
      test project in Release, and run the named test classes (`CliTests`, `SettingsStoreTests`,
      `SettingsSchemaTests`, `PrintModeTests`, `InteractiveSessionStreamingTests`).
- [x] 5.2 Write the changelog fragment `changes/<pull-request-number>.md` under `### Added` and
      commit it.
- [x] 5.3 Run `openspec validate --all --strict`, sync the delta specs and archive the change with
      `openspec archive configurable-step-limit --yes` as the last commit.
