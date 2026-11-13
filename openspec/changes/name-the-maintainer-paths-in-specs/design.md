# Design: name-the-maintainer-paths-in-specs

Refs #751, #614. S3 of the 0.4.0 documentation architecture (`docs/dev/information-architecture.md`).

## Context

S3 relocates the maintainer pages: `docs/README.md` → `docs/dev/README.md`, `docs/guide.md` → `docs/dev/guide.md`, `docs/architecture.md` → `docs/dev/architecture.md`, `docs/distribution.md` → `docs/dev/distribution.md` and `docs/media/README.md` → `docs/dev/media.md`. The plan makes the two stubs (`docs/README.md`, `docs/guide.md`) the only old paths that remain, for one release, and records all five redirects in `docs/docs.json`.

The specs were written when those pages lived at `docs/`. `openspec/specs/repo-quality/spec.md` names `docs/README.md` (map), `docs/architecture.md` (architecture page) and `docs/guide.md` (ADR numbering rule); `openspec/specs/distribution/spec.md` names `docs/distribution.md` (runbook). After the move those requirements point at stubs or at nothing, so a session that follows the source of truth is sent to the wrong page.

The documentation checker does not read `openspec/specs/`, so no gate would have caught the disagreement; the review of #751 did. This change repairs the requirements through the OpenSpec lifecycle — delta, sync, archive — rather than by hand-editing `openspec/specs/**`, which `scripts/check-spec-archive.py` refuses.

## Goals / Non-Goals

- **Goal:** every spec requirement that names a moved maintainer page names its new home.
- **Goal:** the delta is a path substitution only — no rule text, scenario or behaviour changes; the move-do-not-rewrite rule of the plan applies to specs too.
- **Non-Goal:** the `docs/media.md` move needs no spec change: no requirement names `docs/media/README.md`.
- **Non-Goal:** fixing requirements that were already stale before this change (`openspec/specs/tui/spec.md` still says the key table lives in `docs/guide.md`, though S2 moved it to `docs/keybindings.md`). That is S2's requirement to repair, not a path this slice moved, and fixing it here would change the rule and its scenarios.

## Decisions

1. **Four MODIFIED requirements, no ADDED or REMOVED ones.** The requirements already own these facts; only their paths are wrong. Copying the full requirement block keeps the archive lossless, and keeping the scenario names keeps the delta reviewable as a two-word diff.
2. **`docs/README.md` and `docs/guide.md` stay unnamed in the requirement even though S3 leaves stubs there.** The requirement says where the fact lives, not which redirect exists; the stubs are a one-release courtesy recorded in `docs/docs.json`, and naming them would couple the spec to a redirect that is deleted in the next release.
3. **`docs/dev/media.md` gets no requirement.** Nothing in the specs names the media page or the media assets; adding one would be new behaviour, out of scope for a path repair.
4. **The change is archived in this pull request, last commit**, as `AGENTS.md` and the git-discipline skill require. The proposal cannot precede the implementation commit on this branch without rewriting history the lane will not force-push, so the proposal, delta, design, ADR review and archive land as the final commits of the same pull request that contains the move.

## Risks / Trade-offs

- [The delta copies large requirement blocks] A later edit to the same requirement on `main` conflicts at archive → keep the copied text byte-identical except the two paths, so a merge is a mechanical re-read rather than a rewrite.
- [Specs and stubs drift apart in the next release] The stubs are deleted in the release after 0.4.0 and the requirements already name the new homes → deleting the stubs needs no further spec change.
- [A stale requirement survives this change] `tui` and `ci-pipeline` still name `docs/guide.md` in prose → recorded in the final report as a follow-up gap; `ci-pipeline`'s scenario stays true because the stub exists at that path.

## Migration Plan

Proposal → delta → design → ADR review → tasks on this branch; then re-run `openspec validate --all --strict`, `scripts/docs-check --check`, `scripts/docs-sync --check`, markdownlint and the release-workflow test; the archive commit (`openspec archive name-the-maintainer-paths-in-specs --yes`) is the last commit and syncs `openspec/specs/` in the same merge.

## Open Questions

None.
