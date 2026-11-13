# Proposal: name-the-maintainer-paths-in-specs

Refs #751, #614. S3 of the 0.4.0 documentation architecture.

## Why

S3 moves the documentation map, the implementation plan, the architecture diagrams and the release runbook under `docs/dev/`, and points `AGENTS.md` at their new homes. The source-of-truth specs still name the old paths, so `openspec/specs/repo-quality/spec.md` requires the map at `docs/README.md`, the architecture page at `docs/architecture.md` and the ADR numbering rule in `docs/guide.md`, and `openspec/specs/distribution/spec.md` names `docs/distribution.md`. A spec that disagrees with the tree is wrong even when the diff is documentation only: it tells the next session to look at a stub.

## What Changes

- The repo-quality requirements name the maintainer paths that now hold the fact:
  - "Documentation entry points and guideline": the map is `docs/dev/README.md`, and `AGENTS.md` points there.
  - "Architecture documentation": the page is `docs/dev/architecture.md`, in the requirement and in the scenario that names it.
  - "ADR number verification": the numbering rule is documented in `docs/dev/guide.md`.
- The distribution "Channel artifacts" requirement names `docs/dev/distribution.md` as the runbook's home.
- Nothing else changes: no rule text, no scenario additions, no behaviour. The old paths stay as the four-line stubs S3 leaves for one release, and `docs/docs.json` records the redirects.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `repo-quality`: three requirements name the moved maintainer pages (`Documentation entry points and guideline`, `Architecture documentation`, `ADR number verification`).
- `distribution`: `Channel artifacts` names the moved release runbook.

## Impact

- `openspec/specs/repo-quality/spec.md` and `openspec/specs/distribution/spec.md` reach the new text through this change's delta and the archive commit; no spec file is hand-edited.
- Documentation: the moved pages themselves landed in the same pull request (`docs/dev/README.md`, `guide.md`, `architecture.md`, `distribution.md`, `media.md`), with the instruction links and the `AGENTS.md` path edit.
- No code, no tests, no packaging and no release workflow change.
