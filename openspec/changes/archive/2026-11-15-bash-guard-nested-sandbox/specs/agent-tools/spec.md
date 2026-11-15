## MODIFIED Requirements

### Requirement: Bash guard
A `bash` command SHALL run wrapped in a guard profile, not a jail: writes under the state root (`LUNATE_HOME`, default `~/.lunate`) and under the install prefix SHALL be denied at the OS level, `auth.json` under the state root SHALL be read-denied, and every other access SHALL keep its ordinary behaviour (normal work inside and outside the workspace survives the guard). The mechanism is per platform: `sandbox-exec` on macOS, `bwrap` on Linux with `setpriv`'s Landlock rules as the Linux fallback; Windows has no mechanism. Every resolution SHALL run the mechanism once before use, so "installed" cannot stand in for "working": when the mechanism cannot start — including the AppArmor restriction of unprivileged user namespaces on Ubuntu 23.10+ — the report SHALL carry the probe's output and the restriction's name, and the command SHALL run unconfined behind a visible notice. When the process already runs inside a sandbox, so that applying a nested profile is refused (`sandbox_apply: Operation not permitted`), the report SHALL instead say that the process already runs inside a sandbox, that the guard's own rules (the state root, the install prefix and `auth.json`) are not applied, and that the outer sandbox still applies; it SHALL NOT state that the command runs unconfined. When a mechanism enforces less than the profile, the resolution SHALL report that instead of falling back silently. A command whose redirection would write a denied target SHALL be refused before the spawn with a result naming the guard and its mechanism; a failed command that touched a denied target and ended in a filesystem denial SHALL carry a hint that names the guard, marked as best-effort. The guard SHALL be enabled by default. A `bashGuard` settings value of `false` SHALL disable wrapping and probing, and the tool result SHALL explicitly state that the guard is disabled. On startup, while the guard is enabled, Lunate SHALL warn once if a pre-existing hardlink under the state root has a link outside that root, naming a path under the state root. The resolved mechanism and probe state SHALL be available to `/context` and the welcome session header: the mechanism SHALL be one of `sandbox-exec`, `bwrap`, `landlock` or `none`; the probe state SHALL report success, failure or not run. `/context` SHALL show both values, and the header SHALL show the same fact in short form.

#### Scenario: A state-root write is refused and never lands
- **GIVEN** a guarded shell (the mechanism resolved and probed)
- **WHEN** a command redirects into a file under the state root
- **THEN** the result is an error naming the guard and its mechanism, and the file does not exist

#### Scenario: A normal command is untouched
- **GIVEN** a guarded shell
- **WHEN** a command writes inside the workspace, reads its tools, or starts a child
- **THEN** the command behaves as it would unguarded, apart from the wrapping cost

#### Scenario: Reading auth.json is denied
- **GIVEN** a guarded shell and credentials under the state root
- **WHEN** a command reads `auth.json`
- **THEN** the read fails with a filesystem denial (or sees the guard's substitute), the result carries the guard hint, and the secret's text does not reach the result

#### Scenario: normal access survives outside the denied targets
- **GIVEN** a guarded shell
- **WHEN** a command reads a file or a directory that is not a denied target
- **THEN** the access succeeds under every mechanism, the Landlock fallback included

#### Scenario: A compliant command that fails a filesystem gate names the guard
- **GIVEN** the tool result failed with a filesystem denial and the command touched a denied target
- **WHEN** the result is composed
- **THEN** a best-effort hint naming the guard and mechanism is appended

#### Scenario: An unavailable mechanism is reported, never assumed
- **GIVEN** a Linux host where user namespaces are restricted (`apparmor_restrict_unprivileged_userns=1`)
- **WHEN** the first bash command resolves the guard and the mechanism's probe fails
- **THEN** the result names the restriction and the probe's output, states the command runs unconfined, and the same command still produces its honest result

#### Scenario: A nested sandbox is reported as such, not as unconfined
- **GIVEN** a macOS process that already runs inside a sandbox, so `sandbox-exec` cannot apply a nested profile (`sandbox_apply: Operation not permitted`)
- **WHEN** the guard resolves its mechanism for the session
- **THEN** the report says the process already runs inside a sandbox and carries the probe's output, states that the guard's own state-root, install-prefix and `auth.json` rules are not applied while the outer sandbox still applies, does not state that the command runs unconfined, and is emitted once per session rather than once per command

#### Scenario: A pre-existing hardlink outside the state root is warned once
- **GIVEN** the state root contains a file whose filesystem link count exceeds the number of links found under that root
- **WHEN** Lunate starts with the bash guard enabled
- **THEN** one warning for the process names a path under the state root

#### Scenario: Links wholly inside the state root are not reported as an escape
- **GIVEN** a file has multiple hardlinks, all of which are under the state root
- **WHEN** the startup hardlink scan runs
- **THEN** no external-hardlink warning is emitted

#### Scenario: The guard defaults on and can be disabled visibly
- **GIVEN** settings with no `bashGuard` value, or with `bashGuard` set to `false`
- **WHEN** a bash command runs
- **THEN** the guard is active by default; with `false`, the command runs without wrapping or probing and its result says the guard is disabled

#### Scenario: The resolved mechanism and probe state are shared with context surfaces
- **GIVEN** the guard has resolved and probed its mechanism for an interactive session
- **WHEN** the welcome header is rendered and `/context` is requested
- **THEN** `/context` reports the normalized mechanism and whether its probe succeeded, and the header carries the same fact in short form

#### Scenario: A disabled guard reports that no probe ran
- **GIVEN** `bashGuard` is `false`
- **WHEN** the welcome header is rendered and `/context` is requested
- **THEN** both report mechanism `none` and probe state `not run`, and the header identifies the disabled guard
