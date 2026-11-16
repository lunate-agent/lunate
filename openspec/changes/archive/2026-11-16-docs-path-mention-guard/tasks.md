## 1. The guard recognises a named, unread docs reference

- [x] 1.1 Add `ALLOWED_DOCS_MENTIONS` (file + exact reference) to `scripts/no-docs-references.sh`, print one note per applied allowance, and keep failing for any other docs path in that file and for a `File`/`Directory` call on the allowed line.
- [x] 1.2 Enter `tests/Lunate.Coding.Tests/TargetFrameworkTests.cs` with `Path.Combine("docs", "dev", "spikes")` and update the script header to describe the mention allowance beside the reader and fixture allowances.
- [x] 1.3 Extend `--self-test` with the allowed mention, a second docs path in the same file, and a read on the allowed line; `bash scripts/no-docs-references.sh --self-test` passes and `bash scripts/no-docs-references.sh` is green on the tree.

## 2. The guard runs wherever a docs read can land

- [x] 2.1 Add the guard as a `platform_checks` step in `scripts/verify.sh` (after the docs link/navigation check, before the docs lint) and name it in the comment above the function.
- [x] 2.2 Add the guard as a step of `scripts/tree-checks.sh` and name it in the script header and in `.github/workflows/tree-checks.yml`'s header comment.
- [x] 2.3 Add the check to `CHECKS` in `tests/scripts/test_tree_checks.py` and to its docstring lists; run that test class.
- [x] 2.4 Add `tests/scripts/test_no_docs_references.py`, which pins that the docs profile, the platform checks and the tree checks run the guard and that the `platform` and `verify-docs` jobs call those profiles, and run it from the guard's step in `scripts/verify.sh`.

## 3. Prose and gate

- [x] 3.1 Update `CONTRIBUTING.md` (the gate step lists and the tree-check paragraph) and `docs/dev/README.md` (the docs profile and the tree-check list) to name the guard.
- [x] 3.2 Run `dotnet csharpier check .` (no C# changed, but the gate runs it), `openspec validate --all --strict`, `bash scripts/no-docs-references.sh`, `bash scripts/no-docs-references.sh --self-test`, `python3 tests/scripts/test_tree_checks.py` and `bash scripts/verify.sh --profile docs`.
- [x] 3.3 Write the changelog fragment `changes/<pull-request>.md` and commit it before the archive.
- [x] 3.4 Run `openspec validate docs-path-mention-guard --type change --strict`, sync the delta specs and archive the change as the last commit.
