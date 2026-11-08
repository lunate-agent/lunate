## 1. Prove the symptom (test-first)

- [x] 1.1 Add an overlapping-spawn reproduction to `UnixChildProcessTests`: two (or more) spawns start on a barrier in the same instant, one is a short-lived victim whose captured stdout merges to EOF, the other a long-lived bystander; assert the victim's reader reaches EOF with no wait-budget involvement while the bystander is alive. Run it against the current spawn and record the red.
- [x] 1.2 Add the same reproduction for the victim's stderr.
- [x] 1.3 Add a descriptor-list test: a child spawned while a bystander is alive writes its own open descriptors to a file; assert it holds no pipe end it was not given. Run it against the current spawn and record the red.

## 2. Close the descriptor leak (Lunate.Coding)

- [x] 2.1 Create the three captured pipes close-on-exec (`HandleInheritability.None`) and keep handing the child its ends through `posix_spawn_file_actions_adddup2`/`addopen`; the child still gets 0/1/2.
- [x] 2.2 On Darwin add `POSIX_SPAWN_CLOEXEC_DEFAULT` (0x4000) to the spawn flags beside `POSIX_SPAWN_SETSID`; Linux keeps the close-on-exec handles.
- [x] 2.3 State the invariant beside the pipe creation: no descriptor another spawn created may be visible to this child.
- [x] 2.4 Do not add a spawn lock, widen a timeout, or reorder `ProcessRunner`'s reader waits.

## 3. Verify

- [x] 3.1 `UnixChildProcessTests` and `ProcessRunnerTests` green in Release by filter; every test class that references `ChildProcess`/`ProcessRunner` also run.
- [x] 3.2 `dotnet build Lunate.slnx -c Release`, `dotnet csharpier check .`, `openspec validate spawn-descriptor-isolation --strict`, `bash scripts/check-openspec-skills.sh`.

## 4. Docs and archive

- [x] 4.1 Check whether `docs/` names the POSIX spawn descriptor contract; update the guide only if it would otherwise be wrong.
- [x] 4.2 Sync the `process-isolation` delta into the main spec.
- [ ] 4.3 Archive the change as the final commit on the branch.
