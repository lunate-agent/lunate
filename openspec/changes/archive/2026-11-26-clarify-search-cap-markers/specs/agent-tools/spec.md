# Agent Tools Specification Delta

## MODIFIED Requirements

### Requirement: Optional search tools

When configured, the optional read-only search tools SHALL be registered beside the core set: `grep` (regex search with `path`, `glob`, `case_sensitive` and `max_results` of 100 default / 500 cap, using the ripgrep binary when present and an internal walker otherwise, both honouring `.gitignore` and skipping binary files), `find` (glob matching with `path`, `type` of `file` or `dir`, 100/500 bounds, case-insensitive on Windows and macOS, case-sensitive on Linux), `ls` (one directory listing with `path` and `depth` of 1 default / 3 max, directories first then files, `.git` always skipped) and `semantic_search` (the embeddings-backed repo search the `semantic-search` capability specifies; it is registered only when an embeddings profile also resolves). `grep`, `find` and `ls` SHALL refuse a path outside the workspace exactly as `read` does; all four SHALL require no approval, SHALL follow no directory symlink and SHALL route their output through the existing truncation path, whose marker states the cap when a total-byte cap cut the result. Their results SHALL name what was searched. When a result-count cap cut the result, `grep`, `find` and `ls` SHALL end with `[N more matches]`, `[N more files]` and `[N more entries]` respectively (singular for one), and `semantic_search` SHALL end with `[N shown; more matching regions exist, raise max_results for more]`; `semantic_search`'s own 8 KiB total-byte cap SHALL use the shared search-output truncation marker, `[search output capped at 8192 bytes]`. When the configured list is empty — the default — none SHALL be registered, and the tool set SHALL be exactly the core set.

#### Scenario: Nothing extra by default

- **GIVEN** a settings file without `tools.extra`
- **WHEN** the harness is built
- **THEN** only the core tools are registered and no `grep`, `find`, `ls` or `semantic_search` exists

#### Scenario: A configured list registers exactly its tools

- **GIVEN** `tools.extra` set to `["grep", "ls"]`
- **WHEN** the harness is built
- **THEN** `grep` and `ls` are registered, `find` is not, and a `bash` name in the list is rejected with the accepted values

#### Scenario: semantic_search needs both the name and a profile

- **GIVEN** `tools.extra` set to `["semantic_search"]` and no resolvable `usages.embeddings`
- **WHEN** the harness is built
- **THEN** `semantic_search` is not registered and the model is offered no tool that cannot run

#### Scenario: grep honours gitignore and the cap

- **GIVEN** a workspace whose `.gitignore` excludes `bin/` and one file with 600 matching lines
- **WHEN** `grep` runs with a 100 default
- **THEN** nothing from `bin/` appears, exactly 100 matches are returned and the result ends with `[500 more matches]`

#### Scenario: semantic_search states the count cap in its own words

- **GIVEN** an index with three matching regions
- **WHEN** `semantic_search` runs with `max_results: 2`
- **THEN** exactly two regions are rendered and the result ends with `[2 shown; more matching regions exist, raise max_results for more]`

#### Scenario: semantic_search states its total-byte cap

- **GIVEN** a rendered semantic search result that exceeds the 8 KiB output bound
- **WHEN** `semantic_search` bounds the result
- **THEN** the output stays within 8 KiB and carries `[search output capped at 8192 bytes]`

#### Scenario: A path outside the workspace is refused

- **GIVEN** a `grep` call with `path: "../.."`
- **WHEN** the workspace checks it
- **THEN** the call is refused naming the path, the resolved target and the allowed roots, and no file is read

#### Scenario: ls skips .git at every depth

- **GIVEN** a repository root and `depth: 3`
- **WHEN** `ls` renders the tree
- **THEN** no `.git` entry appears and directories precede files at each level
