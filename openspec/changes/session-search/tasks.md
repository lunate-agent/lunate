## 1. Store query

- [ ] 1.1 Add `Session.Search.cs` to `Lunate.Agent`: a bounded, read-only linear scan over the
      sessions root returning session id, entry id, timestamp, header `cwd`, path and snippet per
      matching user/assistant turn, with skipped-line and bound counters.

## 2. Fixture and store tests

- [ ] 2.1 Add the fixture state directory `tests/fixtures/session-search/` with a matching session, a
      session that does not match, and one truncated JSONL line.
- [ ] 2.2 Add `SessionSearchTests` covering a match, no match, the malformed line, the bound and that
      searching writes nothing.

## 3. CLI

- [ ] 3.1 Add `SessionsCommand` (`lunate sessions search <query> [--json]`), dispatch it from
      `Cli.Run` and list it in `CliHelp`.
- [ ] 3.2 Add the command test for the printed row, the path, `--json`, no match and usage errors.

## 4. Picker search field

- [ ] 4.1 Let `PickerOwner` filter the session picker through a search delegate, rebuild rows per
      query, and title the filtered picker.
- [ ] 4.2 Wire `InteractiveSession` to search the store on each query and to resume a hit with the
      matching entry as the active leaf; add the picker test.

## 5. Docs and close-out

- [ ] 5.1 Update `docs/cli.md` and `docs/usage.md`.
- [ ] 5.2 Format with CSharpier, run the named tests and a Release build of every touched project.
- [ ] 5.3 Write `changes/<pull-request-number>.md`, sync specs and archive the change as the last
      commit.
