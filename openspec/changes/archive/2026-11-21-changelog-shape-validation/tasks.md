## 1. Tests first

- [x] 1.1 `tests/scripts/test_changelog_assemble.py`: a `### Added` heading repeated in one version
      section fails `--check` and `--write` by name, and `--write` leaves the file unchanged.
- [x] 1.2 A bullet above the first `###` heading in the unreleased section fails `--check` and
      `--write` by name, and a released section's own prose still passes.
- [x] 1.3 A second `## [Unreleased]` fails by name, and `--release` refuses the same file, writing
      nothing.
- [x] 1.4 A clean changelog passes, and the repository's own `CHANGELOG.md` passes.
- [x] 1.5 The entry-count guard: the kept-entry count and the refusal name both counts, equal counts
      are accepted, and the guard is loaded from the script to be exercised directly.

## 2. The changelog shape check

- [x] 2.1 `scripts/changelog-assemble`: one section walk records level-2 and level-3 headings, opens a
      version section on `## `, and reports a repeated heading at its later line naming the earlier.
- [x] 2.2 `## [Unreleased]` twice gets its own diagnostic; any other repeated level-2 heading the
      generic one.
- [x] 2.3 In the unreleased section a non-blank line before the first `###` heading is reported by
      line and text; released sections keep their prose.
- [x] 2.4 The check runs in `assemble` before anything is rebuilt, so `--check`, `--write`,
      `--check --tag` and `--release` all refuse the file and write nothing.

## 3. The entry-count guard

- [x] 3.1 The kept entries — the unreleased section's top-level bullets outside the generated marker
      region — are counted on the input and on the rebuilt text.
- [x] 3.2 A difference refuses the write, naming the count read and the count that would be written.

## 4. The spec, the fragment and the checks

- [x] 4.1 `openspec/specs/ci-pipeline` delta: the shape check, its named diagnostics and the
      entry-count refusal in the *Changelog assembly* requirement.
- [x] 4.2 `changes/<pull-request>.md`: this change's own fragment under `### Fixed`.
- [x] 4.3 `python3 tests/scripts/test_changelog_assemble.py`, `python3 tests/scripts/test_changelog_guard.py`,
      `dotnet csharpier check .`, `openspec validate changelog-shape-validation --type change --strict`.
      (A plain `--check` run on this branch's tree is expected to fail: the open fragments are
      assembled by the release pull request, not by an ordinary branch.)
- [x] 4.4 `openspec sync-specs` then `openspec archive changelog-shape-validation --yes` as the final
      commit.
