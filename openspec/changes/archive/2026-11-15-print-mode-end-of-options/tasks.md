# Tasks — print-mode-end-of-options

## 1. Test first

- [x] 1.1 Add failing tests in `tests/Lunate.Coding.Tests/CliTests.cs`: `LaunchOptions.TryParse`
      with `-p -- --not-a-flag` yields the prompt `--not-a-flag` (with and without `--json`),
      `-p -- --` yields the prompt `--`, `-p a -- b` is the two-prompt usage error, and
      `Cli.Run(["-p", "--"])` exits 2 with the missing-prompt usage text.
- [x] 1.2 Add a failing print-mode test in `tests/Lunate.Coding.Tests/CliTests.cs`: scripted runs
      with `["-p", "--", "--no-extensions"]` and `["-p", "--", "--acp"]` run print mode and send the
      token as the prompt instead of starting another mode.

## 2. Implementation

- [x] 2.1 Add the `--` case to `LaunchOptions.TryParse` before the inline `--flag=value` split: the
      first `--` ends option parsing and every later token is positional; only a leading-dash token
      before the marker is an unknown option.
- [x] 2.2 Make the `--acp` and `--no-extensions` pre-scans in `src/Lunate.Coding/Cli.cs` look only
      at tokens before the first `--`, so a prompt cannot select a mode.

## 3. Documentation

- [x] 3.1 Name `--` in `src/Lunate.Coding/CliHelp.cs`, `docs/cli.md` and `docs/print.md`.
- [x] 3.2 Write `changes/885.md` under `### Fixed` and run `scripts/docs-check --check`.

## 4. Verify and archive

- [x] 4.1 Run `dotnet csharpier check .`, a Release build of `Lunate.Coding` and its test project,
      and the `CliTests`, `NoExtensionsTests` and `AcpStdoutPurityTests` classes (CI owns the full
      suite and the publish/perf legs); `scripts/verify.sh --profile docs` passes up to the
      environment-blocked `openspec update` check.
- [x] 4.2 `openspec validate print-mode-end-of-options --strict`, sync the delta into
      `openspec/specs/print-mode/spec.md`, and archive as the last commit.
