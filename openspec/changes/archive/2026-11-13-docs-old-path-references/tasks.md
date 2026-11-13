# Tasks — docs-old-path-references

One group: the two spec paths and the checker's prefix list ship together, because the prefix list is
what let the old paths survive review.

## 1. The last references to the pre-move paths

- [x] 1.1 Self-test first: a page under `docs/spikes/` and a page under `docs/eval/` are reported as
      un-navigated user pages.
- [x] 1.2 `scripts/docs_check/constants.py`: drop `docs/spikes/` and `docs/eval/` from
      `RECORD_PREFIXES` and the dead `spec/` from `MAINTAINER_PREFIXES`; the self-test passes.
- [x] 1.3 The `extensions` spec names `docs/dev/spec/extensibility.md` and
      `docs/dev/eval/roslyn-built-in-migration-baseline.md`; `openspec validate --all --strict` is
      green.
- [x] 1.4 Repository grep: no reference to `docs/spec/`, `docs/spikes/` or `docs/eval/` outside the
      records; the docs profile of `scripts/verify.sh` is green.
- [x] 1.5 Changelog fragment `changes/825.md`, then the archive as the final commit.
