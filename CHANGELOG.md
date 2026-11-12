# Changelog

Notable changes to Lunate, one section per release. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project aims to use
[Semantic Versioning](https://semver.org/spec/v2.0.0.html). Versions are tagged on
[GitHub Releases](https://github.com/lunate-agent/lunate/releases); the release process itself is in
[`docs/distribution.md`](docs/distribution.md).

## [Unreleased]

### Added

<!-- changelog-fragments:start -->
- **Per-PR changelog fragments.** Each pull request writes `changes/<number>.md` and never edits
  `CHANGELOG.md`; `scripts/changelog-assemble` folds the fragments into the unreleased section in
  pull-request order, the release pull request runs it, and `ci-ok` refuses a changelog edit from
  any other pull request (#794).
<!-- changelog-fragments:end -->

### Fixed

- **Settings and provider-store writes survive a second process.** Both stores held only an
  in-process lock, so two Lunate processes that each read first could each replace the file and the
  later one silently dropped the earlier change. Each read-modify-write now takes an owner-only
  `settings.lock` / `providers.lock` claim beside the file, waits a bounded time and merges, or
  refuses without writing (#584). The setup wizard's commit takes the same two claims before it
  seeds its staged bodies, so its three-file replace merges with another process's change instead
  of replacing it.

## [0.3.0] - 2026-11-09

The 0.3.0 quality release: a fresh install gets one guided setup path, a configured install gets a re-runnable entry menu, each model can route to its own native API family, and the decision layer's native System One transport lands behind the existing seam. The release also carries every rc.1 review fix, the rc.2 proof pass, the rc.3 proof pass, and the docs/spec agreement work the milestone asked for. The rc sections below record every change; highlights of the line:

### Added

- **The setup wizard.** A fresh interactive install opens one nine-step walk — provider, credential,
  connection test, model, a minimal model check, chat profile and thinking, optional fast and
  decision profiles, the decisions layer, and a summary. Nothing is written before the summary
  confirms, `Esc` writes nothing, an environment key is offered before typing one, and a skipped
  model check never counts as verified. `lunate setup` runs the same walk non-interactively from
  explicit flags and exits 2 naming the answer that only interaction can supply; `/setup` opens the
  full walk on a first run and an entry menu (add a provider, add a profile, change the chat profile,
  edit decisions, walk everything) on a configured install (#524).
- **Provider API families.** A provider entry carries one base URL per family — `openai-chat`,
  `openai-responses`, `anthropic-messages` and `google` — and a model's family comes from the
  profile, discovery or the catalog. Custom providers can carry literal or templated headers
  (`{session.id}`, `{run.id}`, `{lunate.version}`) and choose where the credential goes (`bearer`,
  `x-api-key`, a named header or a query parameter); the value is never part of a header template.
  The OpenAI Responses and Anthropic Messages paths are wired, and an Anthropic-compatible custom
  provider is accepted (#474, #484, #490, #495, #498).
- **Google Gemini directly.** The Google family ships with its native SDK adapter, per-family base
  URL and query-string credential placement, so a Gemini model can run without an OpenAI-compatible
  proxy (#474, #500).
- **OpenCode Go routes each model to its native API.** Discovery reads
  `https://opencode.ai/inference/go/v1/models`, and a request goes to the model's declared API
  family — Chat Completions, Responses, Anthropic Messages or Google — with the session header
  captured on the request (#473).
- **Native System One decisions.** `systemone` is the fifth provider family, served by a native
  decision transport rather than an `IChatClient`. It adds the four question kinds (noul, choice,
  score, box), confidence computed from the distribution, confidence-gated routing, atomic approval
  questions, provider-reported decision cost, exact-version profile pinning, request limits and
  `429`/`529` backoff. Jev-1.14 is preset on OpenRouter, Jev-1.13 on OpenCode, and provider quirks
  (criteria shape, image-only headers) stay preset data (#487).
- **Thinking per profile.** A profile's `reasoningLevel` seeds every session that uses it;
  `/thinking` changes the current session only, while `/settings thinking` and the wizard persist
  the profile. An unsupported model refuses the level by name instead of silently accepting it
  (#476).

### Changed

- **Tool processes no longer inherit provider API keys.** Every preset `KeyVariable` and its alias,
  plus each configured custom provider's `env:` reference, is stripped from a tool child's environment
  on all platforms, including Windows, where the spawn built its own environment; the user's own
  variables are untouched. An explicit pass-through list for named variables is #642 (0.4.0).
- **MCP stdio child environment is allow-listed.** A stdio server no longer inherits the session
  environment minus Lunate's known credential values (the 0.2 behaviour of #449/#455); it receives
  exactly the base set the shared `EnvironmentScrubber` builds — `PATH`, `HOME`/`USERPROFILE`,
  `SystemRoot`, `TEMP`/`TMP`/`TMPDIR`, `LANG` — plus its own declared `env` map and nothing else,
  configured or not (#472). The shared scrubber is annotated with the reason for each base entry,
  and a source-scan test keeps it the only child-environment code path.
- **Question suggestions stay interactive.** The suggestion consumer is wired only where a person
  answers; print mode and ACP have no surface to attach one to (#446).
- **CI: one platform job, and PowerShell only when it can break.** The platform-independent checks
  run once on Linux; the Windows PowerShell leg is path-filtered by a deny-by-default change
  detector and skips when the platform job already proved the same checks (#435).
- **The inert `McpServerOptions.WithheldCredentialValues` option is removed** from
  `Lunate.Protocols`; the allow-list scrub is the only child-environment path (#483).

- **`decisions.approvalsThreshold` is a confidence.** The key now carries what the spec always said it
  did: the confidence a risk signal's chosen answer must reach to escalate. The default `0` is the
  `2t − 1` form of the shipped `0.5` probability gate, so behaviour is unchanged unless the key is
  set; an explicit `0.5` now means a `0.75` probability of the risk (#573).

### Fixed

- **Setup and settings.** The wizard now persists the chosen region, honours `--api-family` for presets, runs the decision-profile model check, refuses a thinking level that conflicts with an existing `reasoningBudget`, and merges a re-run profile instead of replacing it (#568, #569, #570, #579, #580).
- **Runtime and providers.** `--no-session` with a `{session.id}` header resolves an ephemeral id; Anthropic prompt caching and turn merge follow the resolved family; a model/profile switch re-seeds thinking; a discovered endpoint survives profile resolution; the Windows MCP child environment includes `PATHEXT`, `ComSpec`, `APPDATA` and `LOCALAPPDATA` (#565, #571, #598, #599, #600).
- **Decisions.** Failed adapter attempts still record their usage; question-suggestion and extension decisions record usage and cost; the provider cost is persisted and survives `--resume`; typed transport errors reach the operator; approval state carries the untrusted arguments once; `approvalsThreshold` is confidence; a shadow capability failure records the failure and leaves the call unchanged; discovery refuses a scheme or port redirect (#559–#562, #572, #573, #558, #563).
- **Docs and specs.** `systemone` is the fifth API family everywhere; the architecture map and `AGENTS.md` record the 0.3 project graph; the guide no longer documents a `timeout_s` argument, `/shell` command or `shell` setting, and names `output.toolResultLimit` correctly (#574, #575, #577).

### CI

- The macOS `perf` leg runs on release events only; pull-request verify shards keep the startup budget, and gate self-tests retry once with a flake ledger.

## [0.3.0-rc.3] - 2026-11-08

Proof-pass fixes on top of rc.2, found by a real phone session over SSH as well as by CI.

### Fixed

- **Only the credential prompt masks.** The wizard, `/auth` and `/login` were masking every input, so a
  typo in a profile name hid behind asterisks and only showed up in the summary. Provider, endpoint,
  model id, profile name, region and descriptions echo normally; a secret's plaintext still never
  reaches a frame (#617).
- **The model check sends what the session sends.** The check built its client on a path of its own and
  reported `HTTP 400 — Model is unavailable` for a profile whose session then ran fine, because the
  request carried the provider-qualified id in the model field. It now builds through the production
  path (profile → provider → family → client), with a test asserting the check's request and the first
  session request are identical except the prompt (#613).
- **`/quit` during a running turn** cancels the turn and exits, per the in-force tui spec (#635).
- **SIGTERM and SIGHUP restore the terminal** before exit, so a killed session no longer leaves the
  terminal raw (#636).
- **Esc and Ctrl+C cancel a running tool call**, including a long `dotnet test`: a lone Esc byte is
  recognised after a short timeout because mobile terminals send it that way, the process tree is
  terminated, the tool result is recorded as cancelled by the user, and the live area shows
  `esc to interrupt` while a run is in progress (#621).
- **Repeated lines at narrow width.** The live area counted a wrapped line as one row, so every redraw
  left its overflow behind — a wizard walk at 40 columns printed the same notice 10–20 times (#617).
- **Skills notices** are setting-only and go to stderr: an over-limit description warns without
  disabling the skill, and an unknown context window means no threshold and the full index (#631).

### Changed

- **Tool processes no longer inherit provider API keys.** Every preset `KeyVariable` and its alias,
  plus each configured custom provider's `env:` reference, is stripped from a tool child's environment
  on all platforms — including Windows, where the spawn built its own environment. The user's own
  variables are untouched. An explicit pass-through list for named variables is #642 (0.4.0).

### Added

- **OpenCode Go / Jev.** The systemone family joins the opencode-go preset with `jev-1.13` pinned, and
  the wizard prints one line when it offers it: billed to the pay-as-you-go balance, not the Go plan
  (#618).

## [0.2.1] - 2026-11-02

Security: the single-file binaries bundle the .NET runtime, so a runtime advisory reaches users
through the installer, not through their own SDK. 0.2.0 was built with SDK 10.0.402 → runtime
10.0.13; this release builds with SDK 10.0.403 → runtime **10.0.14**, the out-of-band servicing
release for **CVE-2026-111497** (Brotli decoder). No Lunate code changes.

## [0.2.0] - 2026-11-02

The first stable release of the 0.2 line. Its contents are the two candidates' entries below —
[0.2.0-rc.1] and [0.2.0-rc.2] — which record every change, and the fixes that landed after rc.1 was
cut are in the rc.2 section. Highlights of the line:

- **Bash guard**: every command runs inside an OS sandbox (Seatbelt on macOS, bubblewrap/Landlock on
  Linux), with the guard's state in the header and a probe that proves it works.
- **Decision layer**: one optional profile, four consumers (approvals, routing, the decide tool,
  question suggestions), the same gate in the TUI, print mode and ACP.
- **Provider store and profiles**: providers and credentials live in user scope only; a project may
  reference them, never define one.
- **Session work**: fork/clone with images and redaction, an owner-only state directory, and a
  history that stays consistent under concurrent appends.
- **Extensions**: single-file extensions with `#:package`, remote (out-of-process) extensions, a
  self-contained template, and a published contract at `1.15.0`.
- **MCP**: stdio children keep the environment they had, minus the credential values Lunate itself
  knows unless the server's own `env` map names them.
- **Docs**: a configuration reference generated from the settings schema, and a gate that loads every
  tagged config example through the real parser.

## [0.2.0-rc.2] - 2026-11-02

The third candidate. The decision layer ships behind one settings block, and the external review's
security findings are closed: control bytes cannot reach the terminal from a model or a tool, session
state and recordings are owner-only, and a branched session no longer writes back what its source file
had redacted.

### Added

- The decision layer. One `decisions` block in `settings.json` names the profile that decides, then
  opts into any of four consumers: approvals (`off`, `shadow`, `enforce`, with `approvalsThreshold`
  deciding how certain a deny must be), routing (`off`, `shadow`, `enforce` over `routingProfiles`),
  the information-only decide tool, and display-only suggestions beside a question's top option. A
  decision can only tighten an approval, never grant one; without `profile` the layer stays off;
  with no user present an `enforce` deny stands; an escalation may override a remembered
  reject-always. The same gate runs in the TUI, print mode and ACP (#398, #399, #400, #402, #410,
  #444, #458, #443). Settings are JSON only, and `docs/configuration.md` is generated from the
  schema that parses them.

### Fixed

- Text bound for the terminal is scrubbed at every render boundary — live frame, committed markup,
  tool output, diffs, the trust prompt, the select list, the git branch footer and bracketed paste —
  so an OSC 52 clipboard write or an OSC 8 link in a model's or tool's output renders as inert text
  instead of executing in the terminal (#447).
- Session history and recordings are created owner-only (0600 files, 0700 directories), and a file
  left world-readable by an older build is narrowed the next time it is written (#448).
- A forked session file is redacted like its source and carries that session's image blobs, so a
  fork resumes instead of failing on the first request; a fork fails loudly when a blob is missing
  rather than producing a session that can never resume (#438).
- The four session history lists stay consistent when an append throws, so the next turn builds a
  request instead of crashing with an out-of-range index (#441).
- The bash guard denies a target that does not exist yet without creating it first, and reports
  Landlock as `unavailable` rather than claiming a sandbox it does not have (#437).
- A release candidate is created as a prerelease, and the pack dry run refuses `/`, the home
  directory, the repository or its ancestors instead of deleting them (#434).
- Stdio MCP children keep inheriting Lunate's environment so existing setups continue to work, but
  credential values Lunate knows (provider preset and provider-store `env:` values, auth-store keys,
  extension secrets and configured MCP `env` values) are removed unless the server's own `env` map
  declares that variable. A declared `${VAR}` still resolves from Lunate's environment (#449, #455).

## [0.2.0-rc.1] - 2026-11-01

The second release: the extension contract becomes a published surface, the bash guard lands on macOS
and Linux, and the tool gains a pack-only gate that proves every package version before a tag does.

### Added

- The bash guard: a bash command is wrapped in an OS-level deny that refuses writes under the state
  root (`LUNATE_HOME`) and the install prefix and read-denies `auth.json` — `sandbox-exec` on macOS,
  bubblewrap on Linux with `setpriv`'s Landlock rules as the fallback, Windows reported unsupported
  rather than run unconfined silently. The mechanism is probed once per session, so a restricted
  setup (Ubuntu 23.10+'s AppArmor user-namespace restriction) is detected and reported, never
  assumed away (#241).
- The interactive session checks GitHub Releases at most once a day, in the background, and names a
  newer release as one dim linked line in the welcome header (`v0.2.0 available · changelog`). The
  answer is cached under the state root for 24 hours; the request carries no telemetry beyond
  `User-Agent: lunate/<version>`. Opt out with `updateCheck: false` or `LUNATE_UPDATE_CHECK=0`;
  `--offline`, print mode, ACP and CI never check (#238).

### Fixed

- A follow-up queued in the instant a turn ended could be left behind: the turn's drain found the
  queue empty and the queueing continuation read the turn as still running, so neither started the
  entry — it sat in the queue until another turn ended, though the spec says a queued message runs
  when the turn completes. Taking the queue's head and releasing the turn now happen in one step
  under the gate the enqueue path holds too, so the queue and the turn can no longer both leave the
  entry to the other, and a handed-off turn can no longer clear the new turn's run handle.
- The `lunate` tool package shipped its nuget.org page with no licence and no rendered readme:
  `0.1.0` packed with "License missing" and "Readme missing" while the extension package carried
  both. The tool's project now declares the MIT expression, packs the repository README at the
  package root, and names its project and git repository, and
  `scripts/check-package-metadata.sh` — run by the verify gate on both shipped packages and by the
  pack job before the push — opens the packed `.nupkg` and fails when it loses one of the three.
- The slow-hook warning accused an extension of time the machine spent. A dispatch was timed with a
  wall clock around the handler, so a loaded thread pool — where a queued continuation waits for a
  worker — inflated the number: a hook whose own work is nothing reported 648–1392 ms past a 20 ms
  threshold. A crossing is now only called slow when the pool is not the reason it crossed: while
  more work waits for the pool than it has workers, the dispatch is counted but never feeds the
  maximum, the slow count, the warning or the warn-once latch, so a loaded machine cannot name an
  extension for the scheduler's delay and the first crossing on an idle machine still warns.
- A code-mode run that produced nothing at all was reported as "The script timed out", which hid
  the cause. A run that never reached the `hello` handshake now says so, naming the runtime, the
  elapsed time and the missing handshake, and every failure path (timeout, runtime that died, failed
  script) reports the child's exit code and stderr. `StructuredContent` carries `helloSeen` and
  `elapsedMs`, so a loaded runner's slow spawn is visible instead of masquerading as a slow script.
- Three `CodeModeExtension.Tests` cases failed together on a loaded Windows runner: the `node`
  child was started and stayed silent past the 30 s default deadline, so the invitation, the bridged
  ask and the pid never arrived. The tests that are not about the deadline now run with a ceiling
  (120 s) rather than the product default, the kill test yields once so its pid frame is flushed
  before the script blocks the event loop, and an empty pid or ask fails with the run's own report
  instead of a bare assertion.
- The release smoke waits for nuget.org to index the tag's extension package before installing it —
  polling the flatcontainer index, the package content and the registration resource under one
  ten-minute budget — instead of retrying the install five times into the lag. `v0.1.0`'s smoke went
  red on all three operating systems because the package had been pushed but not yet indexed; an
  exhausted budget now fails naming the version and the state of each feed resource, which tells a
  missing package apart from a slow feed.

## [0.1.0] - 2026-10-27

The first stable release. It is `0.1.0-rc.2` plus one fix found on a loaded runner: `ProcessRunner`
abandoned a child's stdout when a write to its stdin hit a closed pipe, so a child that exited while
an answer was in flight looked like one that never answered.

A release candidate is not `latest`, so both installers resolve this build when no version is given.
The `lunate` tool and `Lunate.Extensions.Subagents` publish from the same tag.

## [0.1.0-rc.2] - 2026-10-27

### Added

- The subagents extension ships beside the tool as `Lunate.Extensions.Subagents` — an officially
  supported extension, not a sample. `lunate install Lunate.Extensions.Subagents` installs it from
  NuGet, `lunate packages` lists it with its entry point and API version, `lunate packages --verify`
  recomputes the install record, and `lunate uninstall` removes it. The release smoke proves that
  path from the published feed on all three operating systems.

### Fixed

- A hyphenated tag is published as a prerelease and kept out of `latest`. `v0.1.0-rc.1` published
  as a full release, so `latest/download` — what both installers use when no version is given —
  served a candidate.
- `A_space_after_the_command_closes_the_list` read the input line and the autocomplete list between
  the two writes of one handler, and failed on a loaded runner. The test now waits for the list to
  follow the edit.

## [0.1.0-rc.1] - 2026-10-27

The first release candidate. It exists so the install channels and the recorded media can be
verified against a real, versioned build; the first stable release is `0.1.0`.

### Added

- A native terminal coding agent in one self-contained binary, with an interactive session,
  a live area that keeps the transcript while tools run, prompt editing in `$EDITOR`, and
  session resume and fork.
- C# depth without a language server: `cs_diagnostics`, `cs_find_symbol`, `cs_find_references`,
  `cs_outline` and `cs_rename` from Roslyn, offered only in a C# workspace.
- Extensions in C#: whole packages from NuGet, or a single `.cs` file that declares its tools and
  dependencies, is approved once, compiled and loaded into the running session. Tools, commands,
  hooks, TUI renderers and widgets, MCP servers, model providers and skills all come from the
  same public API, and `Lunate.Roslyn` is the flagship extension built only on it.
- Open protocols: an MCP client, an ACP server for editors, `AGENTS.md` project rules and
  `SKILL.md` skills.
- A trust boundary with a visible approval panel: every write and command asks, edits show their
  diff, a repository asks once, and calls that would extend their own trust (or write the state
  root) always ask. `--yolo` turns the panel off and says so.
- `--print` and `--json` for scripting a run, with a priced footer when the provider reports cost.

### Security

- The approval panel is the boundary: `--yolo` removes it, and a process running with the user's
  shell can write the state root. There is no sandbox in this release; 0.2 adds a bash guard
  profile for macOS and Linux.

[0.1.0]: https://github.com/wdaniel1993/lunate/compare/v0.1.0-rc.2...v0.1.0
[0.1.0-rc.2]: https://github.com/wdaniel1993/lunate/compare/v0.1.0-rc.1...v0.1.0-rc.2
[0.3.0]: https://github.com/wdaniel1993/lunate/compare/v0.2.1...v0.3.0
[0.2.1]: https://github.com/wdaniel1993/lunate/compare/v0.2.0...v0.2.1
[0.2.0]: https://github.com/wdaniel1993/lunate/compare/v0.1.0...v0.2.0
[0.2.0-rc.2]: https://github.com/wdaniel1993/lunate/compare/v0.2.0-rc.1...v0.2.0-rc.2
[0.2.0-rc.1]: https://github.com/wdaniel1993/lunate/compare/v0.1.0...v0.2.0-rc.1
[0.1.0-rc.1]: https://github.com/wdaniel1993/lunate/releases/tag/v0.1.0-rc.1
