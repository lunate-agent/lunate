# Tasks — print-mode-end-of-options

## 1. Test first

- [ ] 1.1 Add failing tests in `tests/Lunate.Coding.Tests/CliTests.cs`: `LaunchOptions.TryParse`
      with `-p -- --not-a-flag` yields the prompt `--not-a-flag` (with and without `--json`),
      `-p -- --` yields the prompt `--`, `-p a -- b` is the two-prompt usage error, and
      `Cli.Run(["-p", "--"])` exits 2 with the missing-prompt usage text.
- [ ] 1.2 Add a failing print-mode test in `tests/Lunate.Coding.Tests/CliTests.cs`: a scripted run
      with `["-p", "--", "--acp"]` runs print mode and sends `--acp` as the prompt instead of
      starting ACP mode.

## 2. Implementation

- [ ] 2.1 Add the `--` case to `LaunchOptions.TryParse` before the inline `--flag=value` split: the
      first `--` ends option parsing and every later token is positional; only a leading-dash token
      before the marker is an unknown option.
- [ ] 2.2 Make the `--acp` and `--no-extensions` pre-scans in `src/Lunate.Coding/Cli.cs` look only
      at tokens before the first `--`, so a prompt cannot select a mode.

## 3. Documentation

- [ ] 3.1 Name `--` in `src/Lunate.Coding/CliHelp.cs`, `docs/cli.md` and `docs/print.md`.
- [ ] 3.2 Write `changes/<pr>.md` under `### Added`/`### Fixed` and run the docs check.

## 4. Verify and archive

- [ ] 4.1 Run `dotnet csharpier check .`, `dotnet build` for the touched project and its test
      project, and the named test classes; then `scripts/verify.sh`.
- [ ] 4.2 `openspec validate print-mode-end-of-options --strict`, sync the delta into
      `openspec/specs/print-mode/spec.md`, and archive as the last commit.
