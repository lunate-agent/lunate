## Context

The interactive session owns a turn's start and end, the session title and the terminal. The agent
loop emits the terminal `RunFinished`/`RunError` events but must not know about desktop UI. The TUI
already decodes raw VT input in `Lunate.Tui` and owns the `IConsoleIO` seam, but `IConsoleIO` is a
public contract other hosts implement; adding focus members to it would be a public API change the
card does not ask for.

The current terminal paths have no focus signal. The standard xterm mechanism is opt-in: the
application writes `CSI ? 1004 h` to request focus reports, receives `CSI I` on focus in and `CSI O`
on focus out, and writes `CSI ? 1004 l` to stop. Terminals that do not implement it simply ignore
the request.

```text
User terminal
    |
    | focus reporting (CSI I / CSI O)
    v
Lunate.Tui  SystemConsoleIO -- KeyReader -- focus state
    ^                                   |
    | keys, raw mode                    | IConsoleIO (unchanged public seam)
    |                                   v
Lunate.Coding  InteractiveSession -- TurnNotifier -- DesktopNotification
    |                  |                                  |
    |                  +-- session title, outcome, clock  +-- osascript / notify-send / PowerShell
    v
Agent event stream (RunFinished / RunError)
```

## Goals / Non-Goals

**Goals:**

- One opt-in notification per root turn, only for a genuinely long turn and only when the terminal
  says it is unfocused.
- Fail open like the turn itself: notification delivery is invisible to the turn's result, output
  and exit code.
- One deterministic seam for tests: the turn gate and the sender are injectable, and the focus state
  is part of the scripted console.

**Non-Goals:**

- No OS notification when focus is unknown, no polling of window managers and no Accessibility
  automation to discover the frontmost application.
- No notification from print mode, ACP mode, child runs or background extension work.
- No notification queue, history, retry or preference UI; the next eligible turn is the retry.
- No new package, public API change or session-format change.

## Decisions

- **The trigger lives in `InteractiveSession`, after the root event stream settles.** The session is
  the only layer that knows the turn's start time, the session title, the terminal focus state and
  whether the run was a child. The agent loop stays UI-free. Alternative: a harness hook, rejected
  because the loop has no terminal and a hook would expose notifications to non-interactive hosts.
- **`notify` and `notifyMinSeconds` are two top-level user-scope settings.** Scalar keys match the
  existing settings style (`updateCheck`, `startupHeader`, `maxTime`), and `0` as the explicit
  no-threshold value avoids a second boolean. Alternative: a `notifications` object, rejected
  because a two-key section buys no grouping the settings reference cannot already show.
- **The TUI exposes focus through an internal optional interface, not `IConsoleIO`.** The public
  console contract stays byte-for-byte compatible; `SystemConsoleIO` implements the internal seam,
  and tests implement it on their scripted console. The session treats a console without the seam,
  and a seam whose value is null, as unknown focus.
- **Focus reporting is enabled only around raw mode.** `SystemConsoleIO` writes `DECSET 1004` after
  entering raw mode, disables it before a child process owns the terminal and on raw-mode exit, and
  resets the state to unknown across that handover. This prevents focus bytes from becoming an
  editor's input and prevents a stale state after the child returns.
- **`KeyReader` consumes focus reports before `VtInputDecoder`.** Focus sequences are input-protocol
  events, not keybindings. Consuming them at the reader keeps the decoder's public `KeyEvent`
  vocabulary unchanged.
- **A detached process starter is the production sender.** macOS gets one `osascript` invocation
  whose title and body travel in environment variables; Linux gets `notify-send` with the title and
  body as separate arguments after `--`; Windows gets a PowerShell toast script with the text in
  environment variables. All output is redirected and drained, and the process object is disposed
  when it exits. Title and body are never interpolated into a shell command. Alternative: shelling
  out with a command string, rejected for quoting and injection risk.
- **The turn gate owns all suppression and failure isolation.** It checks enabled, known-unfocused
  focus and elapsed duration, then calls the sender inside one catch-all. A sender failure is
  silent; the turn has already settled. Alternative: let the sender decide, rejected because then
  every platform path would need to re-implement the same no-op guarantees.
- **The duration uses the session scheduler's clock.** Production reads the monotonic wall clock;
  tests advance virtual time. The notification is skipped for cancelled-before-start runs only in
  the same way any other turn end is skipped: every root run that settles gets the same gate.

## Risks / Trade-offs

- [Many terminals do not report focus] -> the state stays unknown and no notification is sent; the
  usage page states this limit, and the setting remains safe to leave on.
- [The focus request adds escape bytes to the terminal] -> it is written once on raw-mode entry and
  once around a child handover, and a terminal that ignores it is unaffected.
- [Windows toast needs an application identity] -> the PowerShell toast is best effort; a refused
  toast is silent, matching the card's best-effort rule.
- [A notification process outlives a fast session exit] -> the process is detached and its output
  is drained; the operating system reaps it, and a failed start is swallowed.
- [A short default threshold may surprise a user who expects immediate feedback] -> the threshold is
  documented and configurable, and `0` is the explicit always-notify value.

## Migration Plan

No migration: both keys default to today's behaviour (off, 10-second threshold), the settings schema
version does not change, and an existing file without the keys keeps loading. Rolling back is
deleting the change; no persisted state was added.

## Open Questions

None. The focus-reporting limit is documented rather than worked around, and no in-force ADR needs
supersession: ADR-0004 (own VT decoder) and ADR-0007 (live-area concurrency) stay the governing
decisions for the TUI seam this change extends.
