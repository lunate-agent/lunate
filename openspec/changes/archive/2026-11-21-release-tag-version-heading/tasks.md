## 1. Tests first

- [x] 1.1 `tests/scripts/test_changelog_assemble.py`: a temporary released tree helper and the tag
      cases — a correct tag, a prerelease tag, no heading below `## [Unreleased]`, another version's
      heading, a wrong date spelling, a `Directory.Build.props` mismatch, both failures in one run, a
      tag without `v`, and `--tag` refused with `--write` and `--release`.
- [x] 1.2 `tests/scripts/test_release_workflow.py`: the tag-guarded changelog step passes
      `--tag "${GITHUB_REF_NAME}"`, and the gate job still precedes every publish job.

## 2. The tag check

- [x] 2.1 `scripts/changelog-assemble`: `--tag <tag>` accepted only with `--check`, the tag's leading
      `v` stripped, a tag without it refused by name.
- [x] 2.2 The first level-2 heading below `## [Unreleased]` must be `## [<version>] - <YYYY-MM-DD>`;
      the date is validated with the shared exactly-dashed-and-calendar-valid helper `--release`
      also uses.
- [x] 2.3 `<Version>` in `Directory.Build.props` must equal the tag's version; every failure prints
      what it expected and what it found, and one run reports all of them.

## 3. The workflow and the docs

- [x] 3.1 `.github/workflows/release.yml`: the tag-guarded changelog step runs
      `--check --tag "${GITHUB_REF_NAME}"`; the standalone "Check tag against version" step is
      removed because the new check subsumes it.
- [x] 3.2 `docs/dev/distribution.md` names the tag-time check and what it proves, in step 1 and
      step 2.

## 4. Bookkeeping and verification

- [x] 4.1 `changes/<pull-request>.md`: this change's own fragment under `### Fixed`.
- [x] 4.2 `python3 tests/scripts/test_changelog_assemble.py`,
      `python3 tests/scripts/test_release_workflow.py`,
      `python3 tests/scripts/test_changelog_guard.py`,
      `python3 tests/scripts/test_release_contract.py`, `dotnet csharpier check .`, markdownlint for
      the touched Markdown, `python3 scripts/docs-check --check`, `openspec validate --all --strict`.
      (A plain `--check` run on this branch's tree is expected to fail: the open fragments are
      assembled by the release pull request, not by an ordinary branch.)
- [x] 4.3 `openspec sync-specs` then `openspec archive release-tag-version-heading --yes` as the final
      commit.
