## 1. Tests first

- [x] 1.1 `tests/scripts/test_changelog_assemble.py`: a fragment linking `../adr/x.md` assembles into
      `adr/x.md`, and `../../x.md` fails `--check` naming the fragment, the link and the resolved path.
- [x] 1.2 A link inside `changes/` (`README.md`) assembles into `changes/README.md`; an anchor, a URL
      and an inline code span are carried as written.
- [x] 1.3 `--write` writes nothing when one fragment leaves the repository, and the repository's own
      fragments assemble into entries whose every link resolves inside the tree.
- [x] 1.4 `--self-test` passes, and a case that fails exits non-zero naming the case.

## 2. The link rewrite

- [x] 2.1 `scripts/changelog-assemble`: one function resolves a destination against the fragment's
      directory and emits it relative to the root, keeping the anchor tail and `<…>` bracketing.
- [x] 2.2 Anchors, root paths, URLs with a scheme and inline code spans stay as written; inline links,
      images and reference-style definitions are rewritten.
- [x] 2.3 A destination that resolves outside the root is a per-fragment error naming the fragment, the
      link and the path relative to the root; `--check`, `--write`, `--check --tag` and `--release`
      all refuse the tree and write nothing.
- [x] 2.4 The rewrite runs in the fragment parse, so an assembled entry never carries a link written
      from `changes/`.

## 3. The self-test and the gate

- [x] 3.1 `--self-test` is a mode beside `--check`/`--write`/`--release`, reads no tree, and proves the
      rewrite, the carried forms and the escaping-link refusal.
- [x] 3.2 `scripts/verify.sh` runs `scripts/changelog-assemble --self-test` in the changelog step.
- [x] 3.3 `changes/README.md` states the rewrite and the refusal.

## 4. The change's own records

- [x] 4.1 `changes/<pull-request>.md`: this change's own fragment under `### Fixed`.
- [x] 4.2 `python3 tests/scripts/test_changelog_assemble.py`, `python3 scripts/changelog-assemble
      --self-test`, `python3 scripts/docs-check --check`, `dotnet csharpier check .`,
      `openspec validate changelog-fragment-links --type change --strict`. (A plain `--check` run on
      this branch's tree is expected to fail: the open fragments are assembled by the release pull
      request, not by an ordinary branch.)
- [x] 4.3 `openspec sync-specs` then `openspec archive changelog-fragment-links --yes` as the final
      commit.
