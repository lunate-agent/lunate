## ADDED Requirements

### Requirement: Session search

The store SHALL answer one read-only question over the whole sessions root: which past turns contain
a phrase. `lunate sessions search <query>` SHALL scan every `.jsonl` under the sessions root linearly,
in the store's own recursion (hidden and system files included, symlinked directories not followed),
and return one row per matching turn carrying the session id, the turn's timestamp, the header's
`cwd`, the surrounding message text and the session file path. Matching SHALL be a case-insensitive
ordinary substring test over the text of `user` and `assistant` message entries — never over tool
results, compaction summaries, extension entries or image blobs — and a query SHALL be used verbatim
(no regex, no token splitting). Rows SHALL be ordered newest session first by the listing's
comparison (last-modified, then created, then id, all descending) and in file order within a session.

The scan SHALL state and enforce its bound: at most 200 session files and 8 MiB of message text per
invocation, and at most 20 matches. It SHALL NOT build or keep an index, a cache or a second store.
When a bound stops the scan the result SHALL say so; the bound itself SHALL print with every search.

A malformed JSONL line SHALL be skipped and counted, never fatal: the file's other lines, and every
other file, SHALL still be searched. A file whose header line cannot be read SHALL be skipped like a
listing skips it. Searching SHALL NOT create, append to or rewrite any file; the image sweep and the
writer claim SHALL NOT run for a search.

`lunate sessions search` SHALL print each row's session id, timestamp, `cwd`, snippet and session
path to stdout, and with `--json` the same rows as a JSON array; it SHALL exit 0 with no match,
printing that nothing matched plus the bound, and exit 2 on a missing query or an unknown argument.
A timestamp SHALL be formatted culture-invariantly. The snippet SHALL be a single line: the message
text around the first match, whitespace collapsed, bounded with `…` when text was cut.

#### Scenario: A query returns the matching turn with its session facts

- **GIVEN** a session file whose user turn says "let us settle the retry budget"
- **WHEN** `lunate sessions search retry budget` runs over that sessions root
- **THEN** one row prints the session id, the turn's timestamp, the header's `cwd`, a snippet around
  "retry budget" and the session file path

#### Scenario: No match is an empty result

- **GIVEN** sessions whose turns do not contain the phrase
- **WHEN** the phrase is searched
- **THEN** no row prints, the output says nothing matched, the bound prints, and the exit code is 0

#### Scenario: A malformed line does not abort the search

- **GIVEN** a session file with a matching turn and one line that is not valid JSON
- **WHEN** the phrase is searched
- **THEN** the matching turn still returns and the result reports the skipped line

#### Scenario: The bound is stated and enforced

- **GIVEN** a sessions root with more than 200 session files
- **WHEN** a search runs
- **THEN** at most 200 files and 20 matches are scanned and the output names the bound and that it
  stopped

#### Scenario: Only conversation turns match

- **GIVEN** a session whose tool result and compaction summary contain the phrase but whose user and
  assistant turns do not
- **WHEN** the phrase is searched
- **THEN** no row returns

#### Scenario: Searching writes nothing

- **GIVEN** a sessions root whose files have known bytes and modification times
- **WHEN** a search runs
- **THEN** every file keeps its bytes and modification time and no new file appears
