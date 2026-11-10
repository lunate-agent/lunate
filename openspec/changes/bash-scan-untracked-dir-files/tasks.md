## 1. Regression coverage

- [ ] 1.1 Red: add `BashChangeTrackerTests.A_new_file_inside_an_already_untracked_directory_is_published`, baselining a repo with `.s7-evidence/old.txt` untracked and then creating `.s7-evidence/container-bash.txt` plus `s7-scan-probe.txt`; it fails because only the root probe is published.
- [ ] 1.2 Update `BashChangeTrackerTests.New_files_inside_untracked_directories_are_published` (formerly `Untracked_directories_are_skipped_untracked_files_are_published`) to expect both the root file and the file inside the new untracked directory.

## 2. Scan fix

- [ ] 2.1 Change `BashChangeTracker.ScanAsync`'s `git status` arguments from `-unormal` to `-uall`, and update the scan/parser comments that describe untracked directories.

## 3. Gate

- [ ] 3.1 Run the touched test classes by filter (`BashChangeTrackerTests`, `BashChangeStampBackstopTests`, `RoslynFileBusSyncTests`) and `dotnet csharpier check .`.
- [ ] 3.2 Run `openspec validate bash-scan-untracked-dir-files --strict` and `openspec validate --all --strict`.
