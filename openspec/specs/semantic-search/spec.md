# semantic-search Specification

## Purpose
The optional `semantic_search` tool proves the `embeddings` capability: it embeds the workspace into
a rebuildable cache and ranks it against a natural-language query, so the agent can find code by
what it does rather than by the words it uses. It is off by default, offered only when an
`embeddings` profile resolves, and it never writes its index into the workspace or a session.

## Requirements

### Requirement: Optional semantic search tool

`semantic_search` SHALL join `grep`, `find` and `ls` as an optional search tool, off by default and
enabled through the same three levers: `tools.extra`, `LUNATE_EXTRA_TOOLS` and `--extra-tools`. It
SHALL be offered to the model only when its name is enabled **and** an `embeddings` usage resolves
to a profile that meets the embeddings requirement. When either is absent the tool SHALL NOT be
registered, so the model is never told about a tool that cannot run; the host SHALL print one
single-line notice naming the settings key that would make it available. It SHALL be read-only, SHALL
require no approval, and SHALL follow the optional tools' name validation (a core name is refused
with the accepted values).

The tool SHALL accept a required non-empty `query` string and an optional `max_results` integer
(default 10, at least 1, at most 50). An unknown argument SHALL be refused with an error naming the
accepted arguments, and an empty query SHALL be refused with an error saying a query is required.

A search SHALL return the best-matching workspace regions in descending similarity order, each with
its workspace-relative path, its 1-based start line, its 1-based end line and a snippet, and the
result SHALL name the query. The result SHALL be bounded by `max_results` and by total UTF-8 output
bytes (8 KiB), with a marker stating the cap when a cap cut the result. A workspace with nothing to
index SHALL return a readable "no files to search" result rather than an error. An embeddings call
that fails SHALL produce an error naming the model, the profile and the provider's reason, with the
next step, never an unhandled exception.

#### Scenario: Disabled by default

- **GIVEN** a settings file without `tools.extra`
- **WHEN** the harness is built
- **THEN** `semantic_search` is not registered and the model is not offered it

#### Scenario: Enabled beside grep

- **GIVEN** `tools.extra` set to `["grep", "semantic_search"]` and a resolvable `usages.embeddings`
- **WHEN** the harness is built
- **THEN** `grep` and `semantic_search` are registered and no other optional tool is

#### Scenario: Enabled without an embeddings profile is not offered

- **GIVEN** `tools.extra` set to `["semantic_search"]` and no `usages.embeddings`
- **WHEN** the harness is built
- **THEN** `semantic_search` is not registered and one notice names `usages.embeddings`

#### Scenario: A search returns ranked path and line hits

- **GIVEN** an index over two files whose third and eleventh lines are the closest matches
- **WHEN** `semantic_search` runs with a query
- **THEN** the result names the query and lists the two regions in descending similarity order with
  their paths, start and end lines and snippets

#### Scenario: The result count is capped

- **GIVEN** an index with more matching regions than requested
- **WHEN** `semantic_search` runs with `max_results` set to the cap
- **THEN** exactly the capped number of regions is rendered, with the truncation marker

#### Scenario: Bad arguments are refused with the accepted set

- **GIVEN** a `semantic_search` call with an unknown argument or an empty query
- **WHEN** the tool validates it
- **THEN** it returns an error naming the accepted arguments or the missing query, and embeds
  nothing

### Requirement: The workspace index is a rebuildable cache

The index SHALL be a cache of chunks and their embedding vectors, stored under the Lunate state
root's `cache` directory, keyed by the workspace root and the resolved embeddings model. It SHALL
NOT be written inside the workspace and SHALL NOT be written into a session file, so deleting it
loses nothing but the time needed to embed the workspace again. A missing, unreadable or malformed
cache SHALL be treated as empty and rebuilt, never as an error the model sees.

The index SHALL cover the workspace files `grep` and `find` may see: the `.gitignore` chain
honoured, `.git` and directory symlinks excluded, binary files skipped, files above 256 KiB skipped.
Files SHALL be split into line chunks of at most 40 lines; a chunk SHALL carry its path, its 1-based
start and end lines, the digest of its text and its vector. A chunk whose text digest and path are
unchanged SHALL be reused instead of being embedded again. The index SHALL be capped at 2,000 chunks
in deterministic path order, with the cap stated in the search result when it was reached.

Building and updating the index SHALL happen on the first search call, never at startup, and SHALL
batch the embedding requests (at most 64 inputs per request). A failed rebuild SHALL leave the
previous cache usable.

#### Scenario: The first search builds the index and the second reuses it

- **GIVEN** a workspace with two changed-eligible files and a missing cache
- **WHEN** `semantic_search` runs twice with no file change between the calls
- **THEN** the first call embeds every chunk, the second call embeds none, and both return the same
  hits

#### Scenario: A changed file is re-embedded, the rest are reused

- **GIVEN** a built index
- **WHEN** one file's text changes and `semantic_search` runs
- **THEN** only that file's chunks are embedded again and the other chunks keep their vectors

#### Scenario: A deleted cache costs time, not state

- **GIVEN** a built index whose cache directory is deleted
- **WHEN** `semantic_search` runs
- **THEN** the tool rebuilds the index and returns hits, and no session file or workspace file is
  changed

#### Scenario: A corrupt cache is rebuilt

- **GIVEN** a cache file containing invalid JSON
- **WHEN** `semantic_search` runs
- **THEN** the tool treats the cache as empty, rebuilds it and returns hits

#### Scenario: Ignored and binary files are not indexed

- **GIVEN** a workspace whose `.gitignore` excludes `bin/`, one binary file and one matching source
  file
- **WHEN** the index builds
- **THEN** no chunk names a `bin/` path or the binary file, and the source file is indexed
