# ADR Review Manifest

- Status: completed
- Review date: 2026-11-14

## Review Summary

No major durable architectural decision is introduced. This change restates a process decision the lane
switchover already made — one implementation pull request per card, archive last and before review — in
`AGENTS.md`, in the `openspec-git-discipline` skill and in `openspec/specs/repo-quality/spec.md`. It adds no
product boundary, data format, dependency or public contract, and no repository-level ADR file is created:
the decision it records is the process one, and its home is `AGENTS.md` and the spec, where the worker and
the reviewer read it.

## In-Force Context Reviewed

- `adr/0011-repo-quality-tooling.md` — the documentation map, the markdownlint scope and the review-map
  flow. `AGENTS.md` and the skill stay inside that scope; this change adds no page and no new home.
- `openspec/specs/repo-quality/spec.md` — owns the two gate stages and the archiving scenario this change
  modifies; the gate's contents are unchanged.
- `scripts/archive-only-check.sh` and `scripts/check-spec-archive.py` — the guards already encode "the
  archive is the final commit of the change's own pull request"; this change makes the documents say the
  same thing rather than changing a guard.

## New Durable ADRs Created

- None - no major durable architectural decisions were introduced. The lane's shape (`lunate-lane#21`,
  `docs/dev/distribution.md`) is where the release and merge execution is tracked, and it is unchanged.
