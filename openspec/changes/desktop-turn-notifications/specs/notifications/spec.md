## ADDED Requirements

### Requirement: Opt-in desktop notification for a long turn

The interactive session SHALL send at most one desktop notification per root turn, and only when all
of these hold: notifications are enabled, the terminal reports that its window is not focused, and
the turn's elapsed wall-clock duration is at least the configured threshold. Notifications SHALL be
off by default and SHALL be enabled by the `notify` setting or the `--notify` launch flag; the flag
enables them for that invocation without rewriting the settings file.

The duration threshold SHALL be the `notifyMinSeconds` setting, a non-negative integer of seconds
with a documented default of 10. A value of `0` SHALL remove the threshold, so every completed turn
that passes the focus gate is notified. A turn shorter than a positive threshold SHALL send nothing.

The notification SHALL carry the session title as its title and the turn's outcome as its body. The
session title SHALL be the stored `/name` title, falling back to the session id when no title is
stored and to the working directory's name when the session has no file. The outcome SHALL name a
normal finish, a cancellation, a run failure, or the reached limit (`length`, `step_limit`,
`token_limit`, `cost_limit`, `time_limit`) in words, and SHALL include the elapsed duration. A run
failure's notification SHALL carry the failure message, bounded so a long provider error cannot
become the whole notification.

The session SHALL NOT notify for a child (subagent) run, for print mode, or for ACP mode. A second
turn that ends while the terminal is unfocused SHALL notify again; a focus change after the turn
ended SHALL NOT cause a second notification for that turn.

#### Scenario: A long unfocused turn notifies once with its title and outcome

- **GIVEN** an interactive session with `notify` enabled, a stored session title `release prep`, and
  a threshold of 10 seconds
- **WHEN** a turn runs for 42 seconds while the terminal reports that its window is unfocused and
  then finishes normally
- **THEN** exactly one notification is sent, titled `release prep`, whose body names a normal finish
  and the elapsed time

#### Scenario: The flag enables notifications for one run

- **GIVEN** no `notify` setting and an otherwise eligible long unfocused turn
- **WHEN** the session starts with `--notify`
- **THEN** the turn's notification is sent, and the settings file is unchanged

#### Scenario: The default is off

- **GIVEN** no `notify` setting and no `--notify`
- **WHEN** a long turn ends while the terminal is unfocused
- **THEN** no notification is sent

#### Scenario: A focused terminal sends nothing

- **GIVEN** notifications enabled and a turn at least as long as the threshold
- **WHEN** the terminal reports that its window is focused at the end of the turn
- **THEN** no notification is sent

#### Scenario: A short turn sends nothing

- **GIVEN** notifications enabled, an unfocused terminal and a threshold of 10 seconds
- **WHEN** a turn ends after 9 seconds
- **THEN** no notification is sent

#### Scenario: A zero threshold notifies every eligible turn

- **GIVEN** notifications enabled, an unfocused terminal and `notifyMinSeconds` set to `0`
- **WHEN** a turn ends immediately
- **THEN** one notification is sent

#### Scenario: A child run never notifies

- **GIVEN** notifications enabled and an unfocused terminal
- **WHEN** a subagent child run finishes during a root turn
- **THEN** the child's finish sends nothing; only the root turn's own end can notify

#### Scenario: An unknown focus state sends nothing

- **GIVEN** notifications enabled and a turn longer than the threshold
- **WHEN** the terminal never reported a focus state
- **THEN** no notification is sent

### Requirement: Focus reporting is best effort

The terminal seam SHALL request focus reporting with the standard xterm control sequence
`DECSET 1004` when it enters raw mode, and SHALL turn it off before the terminal is handed to a child
process and when raw mode is left. The key reader SHALL consume `CSI I` (focus in) and `CSI O`
(focus out) as focus changes rather than key events.

The session's focus state SHALL start unknown and SHALL become known only after the terminal sends
one of those reports. A terminal without focus reporting, a remote terminal that strips the
sequence, or a terminal that ignores the request leaves the state unknown; Lunate SHALL NOT infer
focus from keystrokes, process state, timing or a platform-specific window query.

#### Scenario: Focus sequences change the state without becoming keys

- **GIVEN** a session whose terminal reports focus
- **WHEN** the input contains `CSI I` followed by a typed character and then `CSI O`
- **THEN** the focus state changes in and out, and only the typed character reaches the input line

#### Scenario: A terminal without focus reporting is silenced

- **GIVEN** a terminal that never sends a focus report
- **WHEN** a long notification-enabled turn finishes
- **THEN** the focus state stays unknown and no notification is sent

### Requirement: Platform transports are best effort

The interactive session SHALL deliver a notification through the host platform's own command:
`osascript` with a notification script on macOS, `notify-send` on Linux, and a PowerShell toast
through the Windows Runtime on Windows. The notification text SHALL be passed without shell string
interpolation, so a title or outcome containing quotes, newlines or backslashes cannot break the
command. The transport SHALL start the command without waiting for it and SHALL discard its output.

An unsupported platform, a missing command, a failed process start, a non-zero exit or a platform
permission that refuses notifications SHALL be silent: it SHALL NOT throw into the turn, SHALL NOT
change the turn's outcome or exit code, SHALL NOT post a notice, and SHALL NOT write to stdout. The
next eligible turn SHALL try again.

#### Scenario: Linux uses notify-send

- **GIVEN** a Linux session with `notify-send` available
- **WHEN** an eligible turn ends
- **THEN** one notification command is started with the session title and the outcome as arguments

#### Scenario: macOS uses osascript

- **GIVEN** a macOS session
- **WHEN** an eligible turn ends
- **THEN** one `osascript` command is started, and the title and body reach it as data rather than as
  unescaped source text

#### Scenario: Windows uses a PowerShell toast

- **GIVEN** a Windows session
- **WHEN** an eligible turn ends
- **THEN** one PowerShell toast command is started with the title and body supplied out of band

#### Scenario: A failing notification never fails the turn

- **GIVEN** notifications enabled, an unfocused terminal and a sender that throws
- **WHEN** the eligible turn ends
- **THEN** the turn still finishes normally, no notice or stdout output reports the failure, and the
  session remains usable

#### Scenario: A missing platform tool is a silent no-op

- **GIVEN** a Linux session without `notify-send`
- **WHEN** an eligible turn ends
- **THEN** the failed start is swallowed, the turn's outcome is unchanged, and the session continues

#### Scenario: The disabled path never starts a process

- **GIVEN** notifications disabled
- **WHEN** a long turn ends
- **THEN** no notification sender is invoked at all
