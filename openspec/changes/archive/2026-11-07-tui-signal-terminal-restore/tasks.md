---
## 1. Restore instance safety

- [x] 1.1 Test-first: a `Lunate.Tui.Tests` unit test (fake platform delegates, no libc) proves the raw-mode restore instance is safe to dispose while another thread suspends/resumes it — after dispose, `Suspend`/`Resume` become no-ops and the restore runs exactly once
- [x] 1.2 Serialise `UnixRawMode`'s instance through `SerializedTerminalMode` (`Suspend`/`Resume`/`Dispose` behind one lock; dispose makes later suspend/resume no-ops) and mirror the change in `WindowsConsoleMode`'s restore for the sibling paths

## 2. Signal handlers

- [x] 2.1 Test-first: `TerminalSignalRestore.Install` returns no-op registrations for the no-op instance (a run that never entered raw mode) and for Windows, and real handlers otherwise; the restore they trigger is driven end to end by the pty tests in `Lunate.Coding.Tests`
- [x] 2.2 Implement the registration in `InteractiveSession.RunAsync` (non-Windows, after `EnterRawMode` succeeds), disposing the live `ITerminalMode` on signal and letting the platform's default disposition end the process

## 3. End-to-end signal test

- [x] 3.1 Build the pty fixture: allocate a pseudo-terminal (libc `posix_openpt`/`grantpt`/`unlockpt`/`ptsname`), capture `stty -a` of the pty, launch the built `lunate` binary against it with a scratch `HOME`/workspace, wait for the input box
- [x] 3.2 Drive `SIGTERM` mid-session: the test sends the signal to the lunate pid, waits for exit, and asserts the pty's `stty -a` attributes equal the captured baseline (echo, canonical mode, not re-asserted from memory)
- [x] 3.3 Drive `SIGHUP` the same way and assert the same baseline equality
- [x] 3.4 Drive both signals mid-turn: submit `!sleep 30` through the pty, approve it, and signal while the command is still running; `stty -a` equals the baseline each time

## 4. Gate

- [x] 4.1 `dotnet csharpier format .` after the last edit; `dotnet csharpier check .` green
- [x] 4.2 Release build and the touched test projects green (`Lunate.Tui.Tests`, `Lunate.Coding.Tests`)
- [x] 4.3 `openspec validate tui-signal-terminal-restore --type change --strict` green, then `openspec validate --all --strict` and `bash scripts/check-openspec-skills.sh` before the archive as the final commit
