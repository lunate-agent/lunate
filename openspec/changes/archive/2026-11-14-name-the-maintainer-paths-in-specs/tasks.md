# Tasks: name-the-maintainer-paths-in-specs

## 1. Proposal and delta

- [x] 1.1 Proposal names the four affected requirements and the two capabilities (`repo-quality`, `distribution`), with the move-only rule stated.
- [x] 1.2 Delta specs copy each MODIFIED requirement in full from `openspec/specs/` and change only the maintainer paths: `docs/dev/README.md`, `docs/dev/architecture.md`, `docs/dev/guide.md`, `docs/dev/distribution.md`; no scenario, rule or new requirement is added.
- [x] 1.3 Design records why `docs/media.md` needs no requirement, why the stubs stay unnamed in the requirement, and why the archive is the last commit.

## 2. Verification

- [x] 2.1 `openspec validate name-the-maintainer-paths-in-specs --strict` passes.
- [x] 2.2 `openspec validate --all --strict` passes before the archive.
- [x] 2.3 `./scripts/docs-check --check` and `./scripts/docs-sync --check` pass on the moved tree.
- [x] 2.4 `npx --yes markdownlint-cli2@0.23.3` reports no issue over the documentation paths.
- [x] 2.5 The release-workflow test that reads the runbook path (`tests/scripts/test_release_workflow.py`) passes.

## 3. Archive

- [x] 3.1 `scripts/check-spec-archive.py` sees `openspec/specs/**` and the archive entry in the same diff.
- [x] 3.2 `openspec archive name-the-maintainer-paths-in-specs --yes` is the final commit of the pull request; `changes/820.md` is committed before it.
