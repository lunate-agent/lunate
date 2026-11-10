# Tasks — tool-child-name-segment-strip

## 1. Red: the segment rule does not exist yet

- [ ] 1.1 Add `ToolChildDefaultStripTests` in `tests/Lunate.Coding.Tests`, each case going through
      `ChildProcess.BuildEnvironment` with a parent set: `GITHUB_TOKEN` stripped,
      `AWS_SECRET_ACCESS_KEY` stripped, `MONKEY` kept, `GITLAB_TOKENIZE_TIMEOUT` kept, a known
      provider key still stripped, and a `toolEnvironment.passthrough` name kept when its segment
      matches. Run it: the strip cases must fail and the keep cases must already pass, which proves
      the leak the issue reports.
- [ ] 1.2 Add the predicate parameter's own cases to
      `tests/Lunate.Extensibility.Tests/EnvironmentScrubberTests.cs`: a matching name is removed, a
      passed-through match is re-admitted, and a `Build` call without the predicate keeps today's
      behaviour. Run it: it must fail to compile before the overload exists.

## 2. Green: the segment rule in the strip, the opt-in after it

- [ ] 2.1 `CredentialVariables`: the four segments, the case-insensitive whole-segment predicate,
      and remarks that describe the new rule and keep the reason the old strip list existed.
- [ ] 2.2 `EnvironmentScrubber`: the optional strip predicate as a new overload; the existing
      six-parameter signature delegates with none; the pass-through re-admission wins over the
      predicate exactly as it wins over the strip list, and a declared value still cannot put a
      stripped name back. Add the new signature to `PublicAPI.Unshipped.txt`.
- [ ] 2.3 `ChildProcess.BuildEnvironment` passes the segment rule.
- [ ] 2.4 Run the focused classes: `ToolChildDefaultStripTests`, `CredentialVariablesTests`,
      `EnvironmentScrubberTests`, `ChildProcessToolEnvironmentTests`,
      `ToolEnvironmentPassthroughTests`, `ToolEnvironmentScopeTests`,
      `InteractiveSessionToolEnvironmentTests`, `ToolEnvironmentPrintModeTests`,
      `ToolEnvironmentAcpTests`, `SettingsStoreToolEnvironmentTests`, plus
      `ChildEnvironmentGuardTests`. All green.

## 3. The stated boundary

- [ ] 3.1 `docs/guide.md` and `src/Lunate.Coding/settings-schema.json` (then
      `scripts/docs-sync`) state the segment rule, the surviving look-alikes and the opt-in
      consequence.
- [ ] 3.2 Restate the known-name-only wording where it became false; leave the pass-through
      matching, scope rule and startup notice text alone.

## 4. Finish

- [ ] 4.1 `dotnet csharpier format .`, then `dotnet csharpier check .`; Release build; focused
      tests.
- [ ] 4.2 `openspec validate tool-child-name-segment-strip --strict`,
      `openspec validate --all --strict`, `bash scripts/check-openspec-skills.sh`.
- [ ] 4.3 Sync the delta spec into `openspec/specs/` and archive the change as the final commit.
