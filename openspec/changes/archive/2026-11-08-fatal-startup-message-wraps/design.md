## Context

The interactive frontend paints one live area through `LiveArea`/`FrameWriter` and commits
finished output to scrollback through the same writer (ADR-0007). A fatal error raised during
session composition is caught in `InteractiveSession.RunAsync` and written straight to stderr
(`Console.Error`) while the live area is already started. The live area's teardown then clears
the rows it believes it owns; the error's wrapped continuation rows are inside that region, so
they disappear. At 40 columns the user sees the first row only.

The pre-session renderer is the plain path used before the live area starts: the raw-mode failure
inside `InteractiveSession.RunAsync` and the terminal-support/setup refusals in `Cli.RunInteractive`
(written to stderr). It relies on the terminal's own wrapping, which works in cooked mode but is a
second, implicit wrapping rule — the one the card says must agree with the live area.

The workspace-trust guard is `LunateHome.StoreOutsideWorkspace`. It checks containment only when
`LUNATE_HOME` is set; an unset variable resolves to `<profile>/.lunate` and is never refused. The
reported default-home failure does not reproduce; the design keeps that behavior and pins it.

Lightweight component view of the change (existing code, ASCII):

```
+--------------------- Lunate.Coding ---------------------+
| Cli.RunInteractive -- pre-session fatal ---------+      |
| InteractiveSession.RunAsync                      |      |
|   catch(composition failure) -- fatal ---------+ |      |
+-----------------------------------------------|-|------+
                                                | |
                       TerminalMessage.Wrap/Lines (Lunate.Tui)
                                                | |
                 +------------------------------+ |
                 |                                |
        stderr (cooked)                  LiveArea.WriteScrollback
        Console.Error                     clears frame, then writes
                                          text at terminal width
```

## Goals / Non-Goals

**Goals:**

- Every fatal startup reason is readable in full at any width, including 40 columns.
- The pre-session path and the live-area path use one width rule, so they cannot diverge.
- A fatal message survives the live area's teardown in the terminal's visible output.
- The default `~/.lunate` still starts a session from the user's home directory.

**Non-Goals:**

- No change to the live-area notice slot's four-line cap for ordinary notices.
- No change to the failure message itself, its trigger, or the guard's containment rule.
- No public API, package, protocol, or session-format change.

## Decisions

**One wrapping helper in `Lunate.Tui`.** Add an internal `TerminalMessage` with `Lines(text, columns)`
(returns sanitized physical lines, wrapping with `CellText.Wrap`, `int.MaxValue`/non-positive columns
means no wrap) and `Write(TextWriter, text, columns)` for the pre-session renderer. `CellText` already
owns the cell-width and control-byte rules, so the wrap agrees with `FrameWriter`'s physical-row count.
Alternative considered: let the terminal wrap the pre-session text. Rejected because the live area
clips instead of wrapping, and the card asks both paths to share one width rule.

**Fatal live-area messages go to scrollback, not the notice slot.** The notice slot caps at four lines
and is part of the frame the teardown clears. `LiveArea.WriteScrollback` already clears the live frame
first, then writes through the scrollback console at the terminal width; the message is above the live
area and the final clear leaves it in place. The session escapes markup and writes each wrapped line.
Alternative considered: show the fatal text as a notice and delay disposal. Rejected because teardown
still clears it and the four-line cap would truncate a long reason.

**The catch keeps its exit-code mapping and stderr fallback.** The interactive catch writes to the live
area when it exists (production and the scripted host); if scrollback is unavailable the existing
stderr write remains. Nothing else on the composition failure path changes.

**The default-home confirmation is a regression pin, not a guard change.** `StoreOutsideWorkspace`
already skips containment when `LUNATE_HOME` is unset. The change adds the explicit spec sentence and
a child-process session test in the user's home; no production branch in the guard changes.

## Risks / Trade-offs

- [A fatal message written through the live writer could race a paint tick] -> `WriteScrollback` runs
  under the live area's paint gate and clears the frame first; the session stops its lifetime before
  the write, and the final dispose clears only the frame's tracked rows.
- [Wrapping the pre-session message ourselves can double-wrap when the terminal also wraps] -> every
  emitted line is at most the terminal width; the terminal only wraps if a line exceeds it, which the
  helper prevents. Redirected output uses `int.MaxValue`, so pipes keep one line per sentence.
- [A platform reports a zero column count] -> non-positive width means no wrap; the message is still
  complete, and no line is clipped.

## Migration Plan

No migration. The change ships with the 0.3.0 fix; no configuration or stored data is read differently.

## Open Questions

None. ADR-0007 (live-area concurrency) and ADR-0016 (workspace boundary) stay in force; this change
uses their existing seams and does not revisit them.
