## Why

The archived `semantic-search` change left the `agent-tools` requirement claiming that all four
optional search tools end with a `[N more …]` line when a cap cut the result. `semantic_search`
does not: its count cap says `[N shown; more matching regions exist, raise max_results for more]`
and its total-byte cap uses the shared search-output truncation marker. The synced spec therefore
describes behaviour the tool does not have, and a reviewer cannot tell which contract is intended.

## What Changes

- Correct the `agent-tools` "Optional search tools" requirement so it states each tool's marker:
  `grep`, `find` and `ls` end their count-capped results with `[N more matches]`, `[N more files]`
  and `[N more entries]`; `semantic_search` states its `max_results` cap with
  `[N shown; more matching regions exist, raise max_results for more]` and its 8 KiB byte cap with
  the shared search-output truncation marker.
- Add a scenario that pins `semantic_search`'s count-cap marker.
- No code or behaviour changes. The tool already does what the corrected requirement says, and the
  existing tests already assert both markers.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `agent-tools`: the optional search tools' cap-marker contract now matches the shipped behaviour
  of all four tools instead of asserting one marker for every cap on every tool.

## Impact

- `openspec/specs/agent-tools/spec.md` only, through this change's delta and the archive.
- No source, test, settings, schema, session-format or changelog change: the shipped markers and
  their tests are unchanged.
