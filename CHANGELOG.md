# Changelog

Notable changes to Lunate, one section per release. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project aims to use
[Semantic Versioning](https://semver.org/spec/v2.0.0.html). Versions are tagged on
[GitHub Releases](https://github.com/lunate-agent/lunate/releases); the release process itself is in
[`docs/dev/distribution.md`](docs/dev/distribution.md).

## [Unreleased]

## [0.6.0] - 2026-11-22

### Added

- The `todo` tool keeps one short, replaceable plan per session: each item is `pending`, `in_progress` or `done`, the live area shows it in at most five lines, and `--json` emits one `todo_updated` event with the full list on every update. The list replays from the session's existing tool calls, so the session format is unchanged. The event is a new case in the contract's event unions, so the contract moves to `1.17.0`; the extension-author note is under that version in `docs/extensions/README.md` (#992).

- **`web_search` and `web_fetch` are core tools.** A plain install can now look up a library's
  current API, a changelog or an error message without an MCP server: `web_search` returns ranked
  title, URL and snippet results from DuckDuckGo's HTML endpoint with no API key, and `web_fetch`
  returns a page's readable text with its title and links as markdown. Both are `Execute` risk, so
  the interactive panel asks once per call, ACP asks its editor, and an unattended run declines them
  unless `--yolo` is set. Search stops at 5 results by default (max 10) and 8 KiB, fetch at 20 KiB,
  and a non-2xx response, a redirect loop or a timeout names the URL and the reason. No new NuGet
  package (#998).

- **A desktop notification when a long turn ends while the terminal is unfocused.** The `notify`
  setting or `--notify` flag enables it, `notifyMinSeconds` sets the duration threshold (10 seconds
  by default), and the notification carries the session title and the outcome. Delivery uses
  `osascript`, `notify-send` or a PowerShell notification through the Windows shell's notification
  area, which needs no registered application identity, and is best effort; a failing notification
  never changes the turn. Focus comes from the terminal's own xterm focus reports, so a terminal
  without them stays silent rather than guessing (#1008).

- **The bash family can run a command in the background.** `bash` with `background: true` returns a job id immediately, `bash_output` returns only the output written since the last read plus the job's state or exit code, and `bash_kill` stops the job and its whole process tree with the foreground path's children-first kill. Unread output is bounded with a truncation marker, the existing bash timeout still applies, and ending the session kills the jobs it still owns (#1010).

- **A budget-tier write-up says where Lunate's 50 steps go.** The analysis replays the five budget
  arm trajectories of `rerun-624-bosgame-2` and finds the ceiling is not tool errors: three of the
  four failures never edit source at all, spending 26–32 steps downloading and comparing upstream
  releases, the fourth fixes the bug at step 12 and spends the remaining 38 debugging a regression
  test it wrote itself, and the one success spends 26 of its 32 steps reading. It then assesses every
  mini-swe-agent design choice the card names and gives each step-cutting one its own proposal — a
  task-shaped benchmark prompt, a `submit` tool, a bash-only arm, a read-once ledger and offline runs
  — with its expected saving and risk; each is a follow-up card, so this pull request changes no
  behaviour ([docs/dev/eval/swebench-budget-tier-steps.md](docs/dev/eval/swebench-budget-tier-steps.md))
  (#995).

- **`lunate doctor` prints the facts a bug report needs.** One offline command reports the version, OS and architecture, install channel (`dotnet tool`, `self-contained`, `Homebrew` or `Scoop`), config and state paths with whether each exists, the resolved provider and model, credential presence — never a value — container-engine availability, terminal capabilities and the last update-check result. `--json` prints one pasteable object; a missing required path or an unreadable settings file exits 1, and the command never makes a network request (#997).

### Changed

- **The red `verify-powershell (windows)` on `main` was a card about a run that predates its fix, and
  this pull request records the triage instead of a second change.** Run `45016780346` ran commit
  `e28e95fd`, whose `scripts/verify.ps1` still packed the four SDK packages from before
  `Lunate.Decisions.Model` became one, so the contract roundtrip's template restore failed with
  `NU1101` on the package the feed the step had just packed did not hold — the failure `#1000` named.
  `#1001` packs and metadata-checks the fifth package, and its own run `45019942790` is green in that
  step, which is the state `main` carries, so nothing in the tree needs to change (#1005).

- **The decision model is its own package.** `DecisionRequest`, `DecisionQuestion` and its kinds, `DecisionAnswer`, `DecisionBox`, `DecisionResult`, `DecisionUsage` and the two seams `INativeDecisionClient` and `IAgentDecisionService` move to `Lunate.Decisions.Model`, with `PublicAPI` files and the contract pair's version. The engine — the chat adapter, builder, OpenTelemetry wrapper, parser and validator — stays in the unpacked `Lunate.Ai.Decisions`, which only `Lunate.Agent` ships; `Lunate.Extensibility.Abstractions` depends on the model package instead of packing the engine, so a contract consumer no longer sees the engine twice. The types keep the `Lunate.Ai.Decisions` namespace ([ADR-0045](adr/0045-decision-model-package.md), amending [ADR-0044](adr/0044-decision-model-ownership.md)) (#973).

- **An extension carries no copy of the decision model.** Extensions and the authoring template
  reference `Lunate.Decisions.Model` compile-only beside the contract pair, so an extension's output
  — and an installed extension package — no longer carries an assembly the host ignores, and a load
  no longer logs the ignored-copy warning for it (#973).

- Release artifacts: what the single-file publish's size and startup pay for is now measured per
  component and recorded in the distribution runbook — ReadyToRun is 2.7× the download and, on
  `osx-arm64`, about 100 ms of startup, while single-file compression buys 3.5% of the archive for
  about 720 ms on every launch, so `EnableCompressionInSingleFile` stays off with the numbers beside
  the decision ([ADR-0008](adr/0008-release-publish-flags.md)) (#993).

### Fixed

- **A red run no longer files a second card for the `ci-ok` aggregate.** The red-run reporter now skips
  the two jobs whose result is derived from the others' — the `ci-ok` aggregate and the reporter itself —
  when the run also holds a red job that ran, files no card for them, and names the skipped job with the
  run link in the run's own log; a run whose only red is the aggregate still files, because then nothing
  else speaks for the gate. Run 45013417456 filed "CI failed: ci-ok" beside the card for
  `verify-powershell (windows)`, which is all its failing step restated, and only on the runs where the
  aggregate's record happened to conclude before the reporter read the jobs; the card set no longer depends
  on that race (#1002).

- **The setup wizard survives a model listing that offers several APIs.** A listing entry that
  declares more than one API family is now read through its `recommended_api` when that names a
  supported family, and skipped otherwise, instead of failing the whole listing; the OpenCode Go
  Gemini entry no longer breaks every other model. A listing that cannot be parsed is reported as an
  undecipherable response rather than a network failure, and the connection-test notice includes the
  underlying message with the credential redacted (#990).

- **The auth store merges concurrent writes instead of dropping a key.** `auth.json` now takes the
  owner-only `auth.lock` claim for its whole read-modify-write and re-reads the document under it, so
  an `auth login`, the TUI's auth picker and the setup wizard's commit in another process no longer
  replace a key another process saved from an earlier read. The wizard's commit takes the auth claim
  beside the settings and provider-store claims before it seeds, a save that changed no key writes
  nothing, and a save that cannot take the claim refuses naming the file and the lock (#991).

- **The Windows PowerShell gate packs the decision model its template binds.** `scripts/verify.ps1`'s
  contract roundtrip still packed the four SDK packages from before `Lunate.Decisions.Model` became
  one, so the template's restore asked the feed the step had just packed — the only source a
  `Lunate.*` package may come from — for a package it did not hold, and `verify-powershell (windows)`
  failed with `NU1101` on every run since while the bash gate stayed green. The twin now packs and
  metadata-checks the same five packages as `scripts/verify.sh`, and the roundtrip's wiring test pins
  the two lists against each other and against the packages the template binds (#1000).

## [0.5.0] - 2026-11-21

### Added

- **Themes.** Every colour is now a semantic token — `accent`, `muted`, `error`, `success`,
  `warning`, `code`, `diff.add`, `diff.remove`, `prompt`, `notice` — behind three built-in themes
  (`default`, `ansi16` and `mono`) and a `theme.json` in the Lunate home that maps a token to an ANSI
  name, a 256-colour index or a hex value. `--theme <name|path>` selects the built-in or reads a
  token file for the run, `tui.theme` remains the persisted choice, the renderer downgrades a hex
  value by capability, and `NO_COLOR` still wins over every theme (#850).

- **`bash` can run in a Docker or Podman container.** `--env docker:<image>` or
  `--env podman:<image>`, or the user-scope `environment` setting, selects one long-lived container
  per session for every process-backed tool command; the workspace is bind-mounted at its host
  absolute path so the direct file tools, verified edits and the post-call change scan keep working
  untouched, while the state root, install prefix, `auth.json` and the engine socket are never
  mounted. The container is started with a keep-alive command so it outlives the gap between calls,
  cancel and timeout end the command's process group inside the container, the session
  marker is forwarded with `-e`, the header and `/context` name the environment and its readiness,
  and a Linux-only end-to-end test proves the path against a real engine (#627).

- **A container selection is refused where tool commands cannot run in it.** `lunate --acp` keeps
  tool commands on the host, so a user-scope `environment` value refuses the ACP start with exit
  code 2 and a message naming the setting and the modes that can run it, rather than serving an
  editor session whose commands would run host-side against the selection (#627).

- **Full screen for the interactive session.** `--fullscreen`, or `tui.fullscreen: true` in the
  settings file, moves the session onto the terminal's alternate screen after the first frame and
  restores the scrollback on every exit path — a normal quit, `Ctrl+C` and an unhandled exception.
  Inline rendering stays the default, and a terminal that cannot switch buffers keeps the session
  usable inline (#878).

- **Mermaid diagrams in answers.** A fenced code block labelled `mermaid` is drawn as a box diagram
  when it is a `flowchart`/`graph` (TD, TB or LR) or a `sequenceDiagram`; an unsupported diagram
  type, a parse failure and a drawing wider than the terminal each show the source with one line
  saying why, and a terminal without Unicode gets the `+ - |` and `< > ^ v` stand-ins with the same
  layout. A new Mermaid diagrams page documents the grammar in scope and the three fallbacks (#879).

- **The step limit is configurable, so print mode can run past 50 model calls.** `--max-steps <n>`
  sets a run's model-call ceiling for that invocation, and `maxSteps` in `settings.json` sets it for
  every run; both work interactively and in print mode, and a value that is not a positive integer is
  a usage error (exit 2). The default stays 50, and a run that reaches its configured ceiling still
  ends with `step_limit_reached` and `step_limit` (#875, #883).

- **The Mermaid renderer is its own Lunate-free library.** `Mermaid.Terminal` packages a C# port of
  mermaid-ascii's flowchart and sequence layout and drawing — Unicode and ASCII glyph sets, the
  frame/label runs a host paints separately, and the source-plus-reason fallback for unsupported
  input — with its own README, MIT licence credit and release package. `Lunate.Tui` consumes it as a
  project reference and keeps only the fence adapter, so diagrams are laid out by the port: box
  borders line up, arrows attach to their edges and the TUI and Provider APIs boxes no longer
  overlap. The Unicode drawing uses mermaid-ascii's `┌ ┐ └ ┘ ─ │` frame, `┄`/`┆` dotted strokes and
  `► ▼ ◄ ▲` heads, while ASCII keeps `+ - |` and `< > ^ v`; the documented parse, unsupported-type
  and width fallbacks and the 40-column floor are unchanged (#884).

- **Print mode accepts `--session-id <id>`.** A run can be named before it starts: the flag creates
  `<id>.jsonl` in the working directory's session directory when it does not exist and continues it
  when it does, so a supervisor can find the process by `--session-id <id>` on its command line and
  resume the same name later. The chosen id is the run's `sessionId` on `run_started` and
  `run_finished`; an id outside the session id grammar (1–128 characters from `A-Z`, `a-z`, `0-9`,
  `.`, `_` and `-`, starting with a letter or digit and without `..`) and a combination with
  `--session`, `--continue`, `--resume` or `--no-session` are usage errors (exit 2) (#870, #908).

- **A design proposal and an ADR draft for hook-based extensibility on observables.** The design
  records what Lunate already has — the typed intercepting hook catalogue and the closed
  `AgentEvent` stream that `--json` and ACP render — and compares it with Pi's `pi.on()` events and
  Claude Code's hooks on declaration, ordering, failure, interception and out-of-process reach. It
  decides one hot, session-scoped BCL `IObservable<AgentEvent>` per extension, delivered off the
  run's hot path through a bounded per-subscriber queue, error-isolated per subscription and unable
  to veto or modify a run; System.Reactive stays out of the contract (ADR-0007 confines it to the
  TUI live area); out-of-process parity keeps the hook catalogue it already has and gains the event
  stream as a declared projection on a later protocol version; `--json` and ACP remain projections
  of the same records. `adr/0040-hook-based-extensibility-on-observables.md` is proposed and awaits
  maintainer sign-off; the implementation lands in a follow-up card, so this pull request changes no
  behaviour (#897, #912).

- **Light and dark built-in themes.** `--theme light` paints dark ANSI inks for a light-background
  terminal, where the shipped pastel palette washes out, and `--theme dark` names the shipped
  palette itself; both join `tui.theme`'s accepted values in the same `--theme` > setting > default
  order, a `theme.json` still overlays whichever is selected, and neither changes the colour depth
  `NO_COLOR` and the detection rules decide (#918).

- **The release pull request's changelog edit is one command.** `scripts/changelog-assemble --release <version> [--date YYYY-MM-DD]` assembles the fragments, promotes `## [Unreleased]` to `## [<version>] - <date>` without the generated markers, leaves a fresh empty unreleased section, deletes the consumed fragments and bumps `<Version>`; it refuses, writing nothing, a version that is not newer, an existing release heading, an invalid fragment or a `--date` not spelled `YYYY-MM-DD` (#958).

- **The post-tag channel manifests are one command.** `scripts/fill-release-hashes v<version> --sums <SHA256SUMS>` rewrites the Homebrew URLs and `sha256` values, the Scoop version, URL and hash, and the winget `PackageVersion`, `InstallerUrl` and uppercase `InstallerSha256` from the release's checksum file; it refuses, writing nothing and naming the missing archive or the manifest whose shape moved, and a second run changes nothing (#963).

### Changed

- **The Mermaid diagrams page shows a worked `sequenceDiagram` example**, with the participant boxes,
  lifelines, message labels and note the terminal draws, and says how a label wider than the room
  between two lifelines is truncated (#881).

- **`Mermaid.Terminal` cuts an over-long sequence message label with an ellipsis** instead of
  widening the drawing, so a long message still fits the terminal it was drawn for (#881).

- **The keymap and full-screen pages name their example files under `docs/examples/`.** The keymap
  page now documents `keymap.json`'s shape, path precedence, overlay-by-id, disable and failure
  rules instead of only the `/settings tui.keys` remap; the full-screen page points at the same
  settings block as a file (#881).

- **The ported Mermaid flowchart renderer is pinned to mermaid-ascii's own golden output.** The TD
  and LR layout and drawing now carry upstream source-and-output pairs for a TD open-link chain, an
  LR back-link and an LR dotted link, compared character by character in both Unicode and ASCII,
  and glyph-rule tests assert that every Unicode frame glyph is one column wide, that the ASCII frame
  is ASCII only and that the two glyph sets draw the same geometry (#886, #893).

- **The C# 15 / .NET 11 feature inventory and target-framework recommendation are documented.** The
  four wanted features are union types, closed hierarchies, collection-expression arguments and
  labeled `break`/`continue`; union and closed types need `net11.0`, while the other two need only the
  SDK 11 compiler. The note also records the CSharpier/analyzer requirements and recommends moving
  the self-contained app to `net11.0` while multi-targeting the extension-facing packages
  `net10.0;net11.0` (#899).

- **The Mermaid examples are captured from the real renderer.** Four VHS tapes — a flowchart TD, a
  flowchart LR, a sequence diagram and an unsupported-type fallback — drive `Mermaid.Terminal`
  through a small example renderer, and a Linux workflow uploads the PNGs as the
  `mermaid-examples` artifact linked from the pull request as evidence, without joining `ci-ok`.
  `docs/mermaid.md` and the `Mermaid.Terminal` README embed the Unicode stills; the ASCII glyph
  drawing stays a text block (#887, #900).

- **CSharpier is pinned at 1.4.0 and the tree is formatted with it.** The new pin parses and
  formats the C# 15 constructs the next refactor slices need (union types, closed hierarchies,
  labeled `break`/`continue`, collection-expression arguments), where 1.3.0 reported a syntax
  error; `scripts/gate-tests.sh` formats one sample of each and fails by exit code when the
  pinned formatter cannot (#901).

- **Lunate runs on .NET 11.** The tool, the app, the self-contained release binaries, the TUI,
  `Mermaid.Terminal`, every internal and test project, the extension template and every published
  package — `Lunate.Contracts`, `Lunate.Extensibility.Abstractions`, `Lunate.Agent`, the harness's
  `Lunate.Ai` assemblies and `Lunate.Extensibility.Testing` — target `net11.0` with C# 15, and nothing
  multi-targets (#192, #902).

- **BREAKING** for `dotnet tool` users: the framework-dependent `lunate` package now carries a
  `tools/net11.0` asset and requires the .NET 11 runtime. The self-contained installers are
  unchanged and still need no runtime. **BREAKING** for an extension built elsewhere: there is no
  `net10.0` contract asset to bind to any more, so an extension for this host is `net11.0`
  (#192, #902).

- The extension template's extension project moves to `net11.0` with its test project, and the
  fixture solutions the gate restores move with the repository so they resolve against the pinned
  SDK's own targeting packs. The fixture that models a multi-target project writes that shape into
  its temporary copy at run time, so no project file in the repository declares two frameworks and
  the target-framework test reads the fixtures like any other project (#902).

- **The theme token file's `NO_COLOR` precedence and every colour form's colourless plan are
  pinned.** A running session whose `theme.json` colours all ten tokens under `NO_COLOR` emits no
  colour escape, and the named and 256-index colour forms resolve to no colour at a colourless
  depth beside the hex form already covered (#424).

- **The archived `tui-customization` change records the proof beside every task.** Each tick now
  names the pull request and the test class that carry it — the keymap (#671), themes (#856, #911),
  full screen (#878), Mermaid (#879, #881, #884, #900) and the docs and archive tail (#881) — so the
  change's record can be checked against the code without re-deriving it. The documentation pages,
  their example files and the archive itself landed with #881; this pull request adds only the
  provenance (#913).

- **The tool-output excerpt builds its line lists with collection-expression arguments.** The whole,
  tail and elided lists are one expression each — `[with(capacity: …), …]` — instead of a
  `new List<OutputExcerptLine>(capacity)` plus an `Add` loop, which deletes the three loops and keeps
  the capacity hint. The TUI project opts in to C# 15 for the construct on the pinned SDK 11, ahead
  of the `net11.0` target move (#905, #914).

- **A red push run on `main` files its own bug card, the way the nightly does.** The reporter job
  (`red-main-issue`, formerly `nightly-issue`) now runs on a push as well as on the nightly, reads the
  run's jobs and files one `kind/bug` card per failing job naming the leg and the run, or comments on
  the open card for that job — so a failure like #888's macOS theme-file timeout on a push is a card,
  not a failure someone has to notice. The card takes the lowest open version milestone (`0.5.0`
  rather than `backlog`, read from the GitHub API, so a release that closes its milestone retargets the
  next card), and an `area/tui` or `area/core` label only when the failing test's project decides it:
  the theme-file and repaint tests inside `Lunate.Coding.Tests` name `Lunate.Tui` and are `area/tui`, and
  a red job without a failing test in those projects carries no area label (#889).

- **The edit matcher and the skill scanner name the loop they leave.** A nested mismatch in
  `EditMatcher.FindMatches` is a labeled `continue` on the window loop instead of a `matched` flag
  and an inner `break`, and `SkillScanner.ScanState.Roots` leaves the root and directory walk with
  a labeled `break` instead of repeating `if (truncated) break;`; the cap still warns once and the
  set of matched windows, skills and warnings is unchanged. Labeled `break`/`continue` is C# 15
  syntax, so `Lunate.Coding` opts in to language version 15 on the pinned SDK 11, ahead of the
  `net11.0` target move (#906, #916).

- **The live area's input is a C# 15 union.** `LiveArea.LiveAreaInput` is a `private union` over
  the same input records instead of a private abstract record with a hand-written sealed subclass
  per case, and the reducer is one exhaustive match, so a new case without a rule fails the build
  rather than falling through to an unchanged state. The painted frame, the public contract and the
  tests are unchanged (#903, #920).

- **The auth verification fixtures wait for the task they later await.** The session posts the
  result notice from inside `VerifyAsync` and assigns `VerifyTask` when that call returns, so a loaded
  runner could paint the notice in that window and the fixture then read a null task. The three
  fixtures now wait for the field instead of only for the frame (#903, #920).

- **The contract's closed sets are C# 15 unions.** `AgentEvent` and `ExtensionEvent`, and the seven
  hook-result families (`ProjectTrustResult`, `InputReceivedResult`, `RunStartingResult`,
  `MessageCompletedResult`, `ToolCallingResult`, `TurnEndedResult`, `CompactingResult`), are `union`
  types over their case records, so the set of event kinds and hook outcomes is fixed at compile time
  and a consumer's `switch` is checked for exhaustiveness ([ADR-0043](adr/0043-contract-unions.md),
  superseding [ADR-0042](adr/0042-contract-closed-sets.md)). **Version note for extension
  authors:** every type name, case name, constructor and the `IHookResult`/`IHookPayload` markers are
  unchanged, so `new ToolCallingResult.Block("held")` and `is ToolCallingResult.Block` compile as
  before, and the hook wire and the `--json` event stream are byte-identical. What changes is the
  shape: an event is a struct that carries its case in `Value` (compare by value, read `Kind` where
  you read `GetType()`, and `events.OfType<T>()` keeps working through the contract's overload), and a
  type outside the contract can no longer add a kind or an outcome — adding one is a breaking contract
  change that moves the API version and gets its own note in
  [`docs/extensions/README.md`](docs/extensions/README.md) (#919, #921).

- **The transcript entries are a closed hierarchy.** `TranscriptEntry` is `closed` (C# 15), so its
  four entries are the whole set and the frame renderer's switch and the picker's filter no longer
  need a fallback arm; the compiler now rejects a new entry until both switches handle it. The
  rendering, the pick filter and the public API are unchanged (#904, #926).

- **The solution is laid out in feature folders.** Every project under `src/` groups its files by the
  feature they serve — `Lunate.Agent` into `Loop/`, `Sessions/`, `Decisions/` and `Tools/`;
  `Lunate.Coding` into 24 feature folders; `Lunate.Protocols` into `Acp/`, `Mcp/Client/` and
  `Mcp/Server/`; the AI, Contract, Extensibility, Tui, Roslyn and Mermaid projects the same way — and
  the test projects mirror those folders. The rule is one sentence: a folder groups files by feature
  and never changes a file's namespace, because a namespace records compatibility, not location
  (`src/Lunate.Contracts` still declares `Lunate.Agent` and `Lunate.Ai` types for its type
  forwarders). `tests/Lunate.Coding.Tests/LayoutRuleTests.cs` enforces it in both directions — every
  `src/` feature folder has a mirrored test folder and no test folder exists without one — and
  `CONTRIBUTING.md` and `docs/dev/architecture.md` state it (#695, #927).

- The two folders that had become namespaces fold back into their assembly's namespace:
  `Lunate.Protocols.Acp` → `Lunate.Protocols` (its types are unshipped) and `Lunate.Tui.Platform` →
  `Lunate.Tui` (internal types). No shipped public member changes (#695, #927).

- The decision layer loses a duplicated request validator: `Lunate.Agent` already validates every
  request before it sends one, so `Lunate.Ai.Decisions` no longer re-checks the same object, and the
  question kind is named `YesNo` (#695, #927).

- **The decision model has one home.** The second request/result set in
  `Lunate.Contracts/Decisions/` is gone; `Lunate.Ai.Decisions` owns the one model — request, question,
  kinds, answer, box and result — and the engine, the chat adapter, the System One transport, the
  `decide` tool, the ask-user suggestions, the wizard check and the extension-facing
  `IDecisionService` all use it, with no mapping between two shapes. The contract keeps only the
  `DecisionProbability` record its `DecisionEvaluated` event publishes, and the edge runs
  `Lunate.Ai.Decisions` → `Lunate.Contracts` (ADR-0044). The decision types were never shipped, so no
  published consumer changes (#695, #927).

- **A red verify leg names the step it failed in, where a reader of the run can see it.** A red `ci.yml`
  leg on `main` is triaged from the check run — the job page of a public run answers "Sign in to view
  logs" — and the gate printed its steps but never named the failing one, so the card for
  `verify (windows-latest, 1/4)` of run 44051817008 could carry nothing finer than "Process completed
  with exit code 1". The gate's exit trap now reports the step it stopped in, with its exit status and
  duration, as an `::error::` annotation and in the job summary; the annotation is the one piece of a
  red leg the annotations API returns without an account. The report is pinned through bash for the
  four cases it has — a failed step, a green exit, a failure before the first step and the summary —
  and the platform profile runs that test (#942).

- **The red-main triage starts at the head of `main`, because a card can arrive stale.** Run
  `44052312654` reddened `verify-powershell (windows)`, `verify (windows-latest, 2/4)` and
  `verify (macos-latest, 2/2)` — the three legs whose extension-template roundtrip restores the feed
  the step has just packed — and the reporter filed a card for each. `#939` fixed all three while that
  run was still finishing, so the next push run, on `b062f74d`, was green in every leg and none of the
  three cards had a tree left to change. The contributor guide now says so: a card whose leg is green
  on the most recent push run of the same job, with a sibling card from the same run closed by the
  merge that fixed it, is a duplicate to close rather than a change to make (#944).

- **`AGENTS.md` names the contract's two ADRs.** The architecture rules now point at
  [ADR-0043](adr/0043-contract-unions.md) for the C# 15 unions behind `AgentEvent`,
  `ExtensionEvent` and the hook-result families, and at the
  [ADR-0042](adr/0042-contract-closed-sets.md) it supersedes, so the decision is one link away
  from the rules a contributor reads (#967).

### Fixed

- **The bash guard names a nested macOS sandbox instead of claiming the command runs unconfined.**
  Inside an existing `sandbox-exec` session macOS refuses a second profile, so the guard cannot add
  its own rules; it now says so once, names that its state-root, install-prefix and `auth.json`
  rules are not applied, and keeps the outer sandbox as the effective boundary (#874).

- **`usage_updated` carries `costUsd` for a model whose provider echoes a bare wire id.** The price
  lookup now resolves the record's model in the scope of the provider the run selected, so
  `opencode-go/deepseek-v4.1-flash` reports its cost instead of staying unpriced because another
  preset serves the same wire id. The cost limit prices through the same lookup, so a `--max-cost`
  that the launch accepts is counted against the spend the stream reports (#876).

- **The tool child keeps git's environment configuration whole.** `GIT_CONFIG_COUNT`,
  `GIT_CONFIG_KEY_<n>` and `GIT_CONFIG_VALUE_<n>` now reach a tool child together, so a session whose
  git is configured through the environment — the lane's `credential.helper` empty, `gc.auto=0` and
  `maintenance.auto=false` among it — runs `git status`, `git commit` and the post-bash change scan
  instead of failing with `error: missing config key GIT_CONFIG_KEY_0`. `GIT_CONFIG_KEY_<n>` was the
  one member the credential-shape rule withheld; `*_API_KEY`, `*_TOKEN` and the other credential
  names are still withheld (#871, #877).

- **Print mode accepts `--` as the end of options.** `lunate -p -- "<prompt>"` — with or without
  `--json` — now passes everything after the marker as the prompt, so a prompt that begins with `-`
  is never read as a flag, and a `--acp` or `--no-extensions` inside the prompt no longer changes
  the mode. `--` with no prompt is still the missing-prompt usage error (exit 2) (#869, #885).

- **A theme file with an unknown token is reported token first.** The warning read
  `theme: <path> names the unknown token '…'`, and the live area caps a notice at four lines: with
  macOS's long `/var/folders/…/T` temp root the path spent them at 80 columns, the token never
  reached the terminal, and `InteractiveSessionThemeFileTests.An_unknown_token_warns_and_the_built_in_theme_stands`
  timed out on the macOS push run. It now reads `theme: unknown token '…'; the valid tokens are …;
  the file is <path>`, so a long path can only lose its own tail, and the test reads the theme file
  from a short and from a deliberately long directory (#888, #890).

- **The gate's offline fixture restores find the SDK's targeting packs again, so the .NET SDK 11 pin
  holds.** An SDK bundles targeting packs only for its own framework, so under SDK 11 a `net10.0`
  fixture asks for `Microsoft.NETCore.App.Ref` 10.0.15 — plus the ASP.NET Core ref and apphost packs —
  and the restore's cleared source list had nowhere to fetch them from, which failed 21 tests on macOS
  and Windows (#892). The fixtures' `NuGet.config` now names the machine's NuGet global packages folder
  — where the repository's own build restores exactly those packs — as a read-only fallback folder: no
  feed is named, the per-fixture package folder and isolation stay, and a failure names the fallback
  folder it used (#895).

- **The first-run Escape test runs on macOS again.**
  `InteractiveSessionFirstRunTests.Escape_dismisses_the_first_run_offer_without_a_write` was skipped
  at runtime on macOS while the flake in #817 was open; the fix — waiting for the dismissed offer
  before typing — is in, the quarantine has nothing left to hold off, and the skip is gone
  (#837, #909).

- **A CI leg GitHub never gave a runner is retried instead of filed as a code failure.** The push run
  `43904858046` went red on `main` with nothing failing: `verify (macos-latest, 1/2)` and
  `perf (macos-latest)` were cancelled before their first step — GitHub's own annotation was "The job
  was not acquired by Runner of type hosted even after multiple attempts", beside its macOS arm64
  capacity notice — while every job that did get a runner was green. A job that never started is not a
  red job: it now files no card and gets no comment, and the run's own summary names it — also when
  another job that ran is filed as its own card, so no run loses sight of a leg it did not file. Such a
  run is a
  runner-capacity red, and `.github/workflows/ci-capacity-retry.yml` reruns that run's failed jobs once
  when it completes; the attempt the retry budget does not cover says in the summary that the rerun is
  the remedy. The derived `ci-ok` aggregate and the reporter are never named as abandoned legs, and a job
  that ran is never retried this way, so no executed failure is retried away (#923).

- **A CI job that never got a runner no longer files a bug card.** GitHub concludes a job it never
  acquired a hosted runner for as `cancelled` with `runner_id: 0` and an empty step list — "not acquired
  by Runner of type hosted even after multiple attempts" — and the red-run reporter filed a `kind/bug`
  card for such a leg that no change in this repository could close: one macOS capacity shortage filed
  two cards for a single push run, one of them for the perf leg that a push does not gate on. The
  reporter now files only for a job that ran and did not pass; a `cancelled` job that reached a step, a
  job that held a runner, and a job whose step list or runner id is missing still file, so the rule fails
  closed. The run's own log names each unstarted job together with the run link and says that a rerun is
  the remedy, including on a run where another red job still files a card (#922).

- **A docs path a test names without reading it no longer reddens the docs profile.**
  `scripts/no-docs-references.sh` accepts the docs reference a test only excludes from its scan when
  it is registered per file and spelled exactly, and prints the allowance on every run, so
  `TargetFrameworkTests`' exclusion of the spike records under `docs/dev/spikes` is no longer reported
  as a repository read; any other docs path in that file, and a read call on the allowed line, still
  fail. The guard is also a step of the platform checks and of the shared tree checks now, so a test
  that does read `docs/` is red on the pull request that adds it and on the push to `main` instead of
  only on a prose pull request (#929).

- **The extension template's tool-calling switch keeps the fall-through arm its own README asks for.**
  `Describe` matched `ToolCallingResult.Proceed` and `ToolCallingResult.Block` with no `_` arm: that
  is exhaustive only against the union the host compiles today, while a copy of the template pins the
  published contract, whose family is not a closed set, so the same source does not compile there. The
  macOS `verify` leg built the template against the published `1.16.0` package instead of the freshly
  packed one and reported `CS8509` at `TemplateToolCallingHandler.cs:31`, an error under
  `TreatWarningsAsErrors`. The switch now names the cases it knows and logs `unrecognised` for any
  other, so the template builds against both contract shapes (#933).

- **The verify gate and the copy end-to-end test prove this checkout's contract, not a published
  one.** The contract roundtrip and `ExtensionTemplateCopyEndToEndTests` pack the four SDK packages at
  the version the release carries and build the packaged-extension template against that feed, but a
  machine's global packages folder already holds a copy of the same id and version from the release,
  and NuGet binds an installed copy before it asks any source. So `verify (macos-latest, 1/2)` built
  the template against the published 1.16.0 contract and failed on the switch the hook-result unions
  close (CS8509), while `verify (macos-latest, 2/2)` ran the copy test, where that same contract
  compiled with only a warning outside `Directory.Build.props` and failed at load with a
  `TypeLoadException` on `HandleAsync`. Both restores now use a packages folder of their own, name the
  feed they just packed as a source, and build from the assets file that restore wrote — in
  `scripts/verify.sh`, its `verify.ps1` twin and the copy test; tests pin the wiring and the restore
  behaviour the isolation exists for (#932).

- **The verify gate's PowerShell twin and the copy end-to-end test prove the contract from the feed
  they just packed.** `verify-powershell (windows)` of run 44033633019 went red with
  `verify (windows-latest, 1/4)`/`(2/4)`: a release also serves `Lunate.Contracts` on nuget.org under
  the same id and version, so which source answers is a source order, an HTTP cache and a machine's NuGet
  configuration rather than the gate's intent. #937 gave `scripts/verify.sh` a `nuget.config` of its own
  with a `packageSourceMapping` that lets a `Lunate.*` package come from the packed feed and from
  nowhere else; `scripts/verify.ps1` and `ExtensionTemplateCopyEndToEndTests` — the two remaining places
  that restore the template against the freshly packed feed — now carry the same configuration, restore
  through `--configfile`, and no longer name a source at all (#936).

- **The configurations the gate writes escape the path they name.** The feed path is written into an
  XML attribute, and a temp directory may hold `&` (Windows allows it in a profile path) or `<` (valid
  on Unix): written raw, `nuget.config` is malformed XML and the restore fails before it can prove
  anything, which would redden the same Windows legs for a reason that has nothing to do with the
  contract. `scripts/verify.sh` (`xml_attribute`), its `verify.ps1` twin (`ConvertTo-XmlAttribute`)
  and `ExtensionTemplateCopyEndToEndTests` (`XmlAttribute`) now encode the five characters an
  attribute cannot carry raw, for the contract roundtrip's configuration and for the extension
  package roundtrip's; a test runs the bash helper and parses the result back as XML, and pins that no
  configuration site interpolates a raw path (#936).

- **The extension-template copy end-to-end test gives every child `dotnet` folders of its own.**
  `verify (windows-latest, 2/4)` is the shard that runs the copy test (#938). Its four `dotnet pack`
  children restore and build this repository, so without a packages folder of their own they fill
  the runner's machine-wide one and read whatever an earlier job left there; without an
  `--artifacts-path` of their own they also write `obj/project.assets.json` beside every packed
  project, naming a temp packages folder the test deletes, and the worktree's next build fails on
  it. Every child the test starts now pins `DOTNET_CLI_HOME`, `NUGET_PACKAGES`,
  `NUGET_HTTP_CACHE_PATH` and `TMPDIR`/`TMP`/`TEMP` to the test's own folders, disables the MSBuild
  server and node reuse, uses one node, and the packs build under an `--artifacts-path` inside the
  test's temp directory. The gate's `FixtureRestore` already pins the same set for its own child
  restores; a wiring test pins the copy test's set and its artifacts path (#938).

- **The verify gate's contract roundtrip binds the feed it just packed, not a released copy.**
  `verify (windows-latest, 1/4)` went red on the push of `49e4b54e` together with the other two legs
  whose gate restores the template against the packages the step had just packed, while every other
  leg passed: with the packed feed merely listed first among two sources, which source provides
  `Lunate.Contracts` 1.16.0 — the folder feed or nuget.org, which serves the same id and version from
  the release — is left to source order, the HTTP cache and the machine's NuGet configuration. The
  roundtrip now writes a `nuget.config` of its own with `packageSourceMapping`, so a `Lunate.*`
  package can only come from the packed feed and nuget.org only serves the test SDK; `--configfile`
  keeps the runner's own configuration out of the run. The wiring test pins the mapping and a
  stand-in package pair on two local folder feeds pins the restore behaviour it exists for (#937).

- **A red run's card names the step that failed and the tests the log names.** The card filed for
  `verify (macos-latest, 2/2)` of run 44052312654 said only which leg went red, so the session that took
  it had to read the run's jobs through the API before it could start — and the leg's shard runs nothing
  that commit `7e9d0088` had changed, so the answer was in the run, not in the diff. Both facts are in
  documents the reporter already holds: `scripts/ci-issue-triage` now reads the job's first step that did
  not succeed from the run's jobs document (`--failed-step-json --job-id`) and the failing test names from
  the log the reporter downloads for the area label (`--failed-tests-file`), and the card body carries
  them — the first five names, with the count when the log names more. A job whose document holds no
  failed step and whose log names no failing test (a build, format or perf leg) writes neither line: the
  card states what was read, never a guess (#945).

- **A red test step names the tests it failed on, in the run's check run.** `verify (macos-latest, 2/2)` of
  run 44189133429 failed its test step twice, and the annotation #942 added put the step in the check
  run — `verify: the test step failed (exit 1, 875s)` — but not the test: the failing line existed only in
  the job's log, which answers "Must have admin rights" to anyone without a session, so the card named no
  test and the red could be located but not attributed. The gate already reads those lines for the flake
  ledger, so the same pass output now names up to five of them as `::error::` annotations and in the job
  summary, from the three line shapes `record_flake` and `verify-test-runner.ps1` read. A pass that
  passed on its retry, an output with no failing-test line and a missing output report nothing (#948).

- **A red startup budget names the measurement it failed on, in the run's check run.** `perf (macos-latest)`
  of run 44466042157 failed its `Startup budget (dedicated measurement)` step with the annotation "Process
  completed with exit code 1" and nothing else: `scripts/perf.sh` printed the floor, median and budget into
  the job's log, which answers "Must have admin rights" to anyone without a session, so the card could not
  tell the shared macOS runner's known 300-330 ms noise from a doubled startup. The gate now prints the
  sentence it fails on as an `::error::` annotation and into the job summary — the floor, the median, the
  budget and, when the noise policy retried, the first attempt's floor — and names an unreadable hyperfine
  JSON the same way. A leg that measures without enforcing (`STARTUP_BUDGET_ENFORCE=0`) annotates nothing
  on any failure — a breach and an unreadable measurement alike — because it is green and the annotation
  would accuse it (#950).

- **A red test pass names what it failed on, from both attempts.** `verify (macos-latest, 2/2)` of run
  44466042157 failed its de-AT pass twice and the check run carried only `the test (de-AT culture) step
  failed (exit 1, 782s)`: the report read the final attempt's output alone, so a first attempt that failed
  on a test and a retry whose session aborted left nothing to name — and a pass that prints no
  failing-test line at all, which is what an aborted session, a crashed host or a module that never
  started looks like, had nothing else to offer either. The gate already holds both outputs, so the first
  attempt's failing tests are named with the retry's — each test once, five at most — and a pass without
  any test name reports up to three of its failed modules, with the passed, failed and aborted counters
  the module line carries, and the run's own verdict line without the exit-code link it ends with, in the
  same `::error::` annotations and job summary (#951).

- **A red leg names the test it failed on even when the runner colored its output.** `verify
  (macos-latest, 2/2)` of run 44722124911 failed its de-AT pass twice with the runner's verdict "at least
  one test failed", and the check run carried only that verdict: GitHub Actions sets `GITHUB_ACTIONS=true`,
  which makes Microsoft.Testing.Platform color its output even into the redirected file the gate reads, so
  every failing line arrived as `\x1b[31mfailed\x1b[m <test>` and `... \x1b[31mfailed with 1 error(s)\x1b[m`.
  The gate's readers now strip the escape sequences before matching, so the check run's annotations, the
  flake ledger and the card the nightly files name the same test from a colored log as from a plain one —
  and the card carries the area label the colored failing test decides (#954).

- **A first run's `/setup` no longer races the first-run `/auth` offer.** A first run offers the `/auth`
  picker once, after composition, and that offer is titled "Select provider" like the setup walk's first
  step. `SetupWizardEntryMenuTests.A_first_run_keeps_the_full_walk` typed `/setup` as soon as composition
  finished, so the offer and the command raced: a key read while the offer is up belongs to the picker,
  `/setup` is inert in it, and its Enter confirms the first preset and starts a credential prompt — the
  walk never opened and the test's "Select provider" wait expired after 60 seconds. That is the failure
  `verify (macos-latest, 2/2)`'s de-AT pass named in run 44739221460, the same leg that was red three
  times before with no failing test named; under load it reproduced here in both cultures (4 of 20 de-AT
  and 3 of 14 en-US runs) with the session diagnosis showing the credential prompt
  `API key for openai:` (trailing space included) as its last write.
  The test now dismisses the offer and waits for its frame to leave before typing `/setup`, so its wait
  can only be satisfied by the walk's own picker: 0 of 22 de-AT and 0 of 16 en-US runs after the change
  (#956, #957).

- **The release gate proves the tag's own changelog heading.** `scripts/changelog-assemble --check --tag <tag>` fails, naming what it expected and what it found, unless the first heading below `## [Unreleased]` in `CHANGELOG.md` is `## [<version>] - <YYYY-MM-DD>` and `Directory.Build.props` carries the tag's `<Version>`, so a release cannot publish notes under another version's heading (#959).

- **The changelog's own shape is checked before it is assembled.** `scripts/changelog-assemble --check`
  and `--write` now refuse a `CHANGELOG.md` whose unreleased section or version sections the assembler
  cannot read reliably, with one diagnostic per finding naming the file, the line and what was found
  there: a `##` or `###` heading repeated within one version section, a non-blank line outside any
  `###` heading in the unreleased section, and a second `## [Unreleased]`. Before, a second
  `### Added` silently dropped the earlier block's hand-written bullets while reporting the tree as up
  to date, a stray bullet was carried outside every heading into the next release, and everything
  under a second unreleased heading was never assembled or released. The write also counts the entries
  the generated marker region does not own before and after, and refuses, writing nothing, when the
  counts differ, so a write can never carry fewer entries than it read (#960).

- **A changelog fragment's links are rewritten for the repository root.** A fragment is written from
  `changes/`, so its link to an ADR reads `../adr/0043-contract-unions.md`; assembled into
  `CHANGELOG.md` at the root it pointed one level above the repository and failed the documentation
  link check — three 0.5.0 jobs did, and the release was edited by hand to get through. The assembler
  now resolves every relative link destination against the fragment's own directory and emits it
  relative to the root (`adr/0043-contract-unions.md`, `changes/README.md`), leaving an anchor, a root
  path, a URL and a link inside inline code as written, and it refuses a fragment whose link leaves
  the repository from `changes/`, naming the fragment, the link and the path it resolves to, so the
  author fixes it in their own pull request instead of during a release. `scripts/changelog-assemble
  --self-test` proves the rewrite and the refusal, and `changes/README.md` states the rule (#968).

## [0.4.0] - 2026-11-14

### Added

- **The setup wizard walks back, forward and in modes.** Every step keeps its answers, `←`/`→` move
  between answered steps, a returning entry menu edits several settings in one pass, a multi-select
  binds several routing profiles, and a Quick walk asks the minimum while naming every default it
  skipped and the command that changes it (#681, #682).

- **One wizard run configures several providers.** A second provider is collected in the same walk,
  a duplicate name asks for a rename, a credential already stored is reused by reference, and every
  provider lands in one commit and one review (#683).

- **Fast and decision usages can come from any provider.** Each usage step offers the providers
  configured in this run, so a provider set up inline reaches the same review and the same commit,
  and `usages.chat` keeps its own profile (#684).

- **The wizard shows a file manifest and a write receipt.** Before the summary confirms, every
  staged value is grouped by the file it will reach, with the routing profiles the multi-select
  confirmed and never a typed key; after the commit, a receipt names what was written and what was
  left alone (#685).

- **A normal writer no longer loses the wizard's commit.** The commit takes the settings and
  provider-store claims before it seeds its bodies, and an unclaimable commit refuses without
  writing (#668).

- **A documentation checker and navigation manifest.** `scripts/docs-check` keeps every relative
  link, `#anchor`, redirect stub and `docs/docs.json` navigation entry honest, and its self-test plus
  Python suite pin the failure paths; it runs in the docs and platform verify profiles and the
  always-on tree checks (#749).

- **Per-PR changelog fragments.** Each pull request writes `changes/<number>.md` and never edits
  `CHANGELOG.md`; `scripts/changelog-assemble` folds the fragments into the unreleased section in
  pull-request order, the release pull request runs it, and `ci-ok` refuses a changelog edit from
  any other pull request (#794).

- **A fragment bullet may cite several pull requests.** The line ends with one or more references —
  `(#681, #682).`, the changelog's own existing shape — so a change that landed under several pull
  requests keeps its wording, and the assembler's `--check` is proven on a copy of this repository's
  own fragments (#794).

- **A release tag must come from a merged release pull request.** The Release workflow refuses a
  tag whose commit is not on `main` or not associated with a merged release pull request, and
  `scripts/release-contract` reads the published release back after the assets are attached; the
  lane's own release and merge execution stays tracked in lunate-lane#21 (#794).

- **The Automate and Reference documentation groups are complete.** `print.md`, `json-events.md`,
  `acp.md` and `ci.md` document one-shot, machine-readable, editor-driven and unattended use, and
  `cli.md`, `providers.md` and `environment-variables.md` carry the look-up tables; the pages they
  moved out of link them, and a `scripts/docs-check --run` pass compares every option in `cli.md`
  with `lunate --help` in both directions (#827).

- **The first published SWE-bench Verified run is pinned, reproducible and committed.** The run
  definition names five Verified instances (every 100th id in lexicographic order), one budget
  OpenCode Go model and one frontier OpenRouter model, a single US$5.00 cost cap and a 50-step
  parity limit; `scripts/eval/swebench_run.py` runs the Lunate and mini-swe-agent arms on that
  same definition, prices every row, and takes every resolved count from the official harness.
  The published run `rerun-624-bosgame-2` resolved 2/5 and 4/5 for Lunate against 5/5 and 5/5 for
  the mini-swe-agent baseline, spent US$1.6157 of the cap, and records every failure; the README
  benchmark sentence now cites that measured pilot, including that the baseline was ahead, instead
  of the saturated C# suite (#624, #839).

- **A run's evidence is committed where its summary links it.** `rerun-624-bosgame-2` ships each
  instance's submitted `predictions.jsonl` and the official `report.json` for every instance that
  produced a patch under `eval/swebench/runs/`, every instance row of the committed summary and of
  the write-up points at it instead of a machine-local path, and the orchestrator's default work
  tree is the tracked `eval/swebench/runs`, so a clean checkout can follow a published resolved
  count to its official verdict (#624, #839).

### Changed

- **A code-owner review gates the files that can change what runs.** `.github/CODEOWNERS` names
  `@wdaniel1993` as the owner of `adr/`, `.github/`, `AGENTS.md` and `.agents/`, so the
  `protect-default` ruleset can require review from a code owner on the decisions, the workflows
  and the machinery that decides what CI enforces, and the instructions and skills every agent in
  this repository follows; every other path stays on the model review (#804).

- **The documentation check is written down as a contract, not just wired into CI.** `repo-quality`
  now states that every relative link and `#anchor` on the user and maintainer pages resolves, that
  every redirect is the four-line stub, that `docs/docs.json` lists each user-path page exactly once
  and that every page carries a runnable marker or a `proseOnly` reason; `ci-pipeline` names
  `scripts/docs-check --check` in the OS-independent set, in the docs profile and in the
  documentation-only push, so deleting the step is a spec change instead of a quiet regression
  (#818).

- **The maintainer documents live under `docs/dev/`.** The documentation map, the implementation
  plan, the architecture diagrams, the release runbook and the media set move there, their relative
  links follow and `AGENTS.md` names the new paths; `docs/README.md` and `docs/guide.md` keep a
  moved-page stub for one release so older links still land, and the repository-quality and
  distribution specs name the moved pages so the source of truth matches the tree (#751).

- **A misdeclared reasoning style cannot change the wire parameter.** A harness test sends a model
  declared `budget` on an OpenAI-compatible family and one declared `effort` on the Anthropic
  Messages family, so a regression back to the declared style fails the suite instead of shipping
  (#601, #828).

- **Settings and key bindings have a reference page of their own.** `settings.md` and
  `keybindings.md` carry the generated tables, and the guide, the TUI comments and the remaining
  cross-links point there instead of at the pages those tables left (#776, #829).

- **The tool child withholds credential-shaped variables by default.** A `bash` child no longer
  inherits a variable whose name has a `KEY`, `TOKEN`, `SECRET`, `PASSWORD` or `CREDENTIAL` part
  (split on `_` and `-`, case-insensitive), so `GITHUB_TOKEN` and `AWS_SECRET_ACCESS_KEY` stay out of
  it while `MONKEY` and `OPENAI_BASE_URL` arrive; one exact name in `toolEnvironment.passthrough`
  admits a withheld variable back (#741).

- **The picker lifecycle has one owner.** Open, move, filter, confirm, navigate and dismiss are
  messages applied in order by one consumer, `PickerOwner`, which is the only writer of the picker
  state and the only poster of `SetPicker(...)`. A dismissal can no longer clear or replace a picker
  another thread is installing, the wizard's pick is completed by the same owner, and the two
  regression seams that paused a transition mid-flight are gone (#735).

- **The interactive session has a page per task.** `quickstart.md`, `models-and-providers.md`,
  `decisions.md`, `export-copy-debug.md` and `terminal-setup.md` each own one job the first-run and
  session pages used to carry together — the wizard walk, the provider, credential, profile and
  catalog choices, the optional decision layer, `/copy`, `/export` and `/debug`, and the terminal
  matrix with the shell the `bash` tool resolves. `usage.md` keeps only the session, opens with what
  it answers and links the neighbours from Related instead of repeating them, and `first-run.md`
  keeps a four-line moved stub for one release (#753, #842).

- **The CI gate is sized by the event.** A pull request builds and tests on `ubuntu-latest` only, the
  merge queue runs the Ubuntu test gate without repeating the pull request's changes decision,
  changelog guard, platform/docs checks or PowerShell twin, and the full Ubuntu/Windows/macOS matrix
  with the PowerShell twin and the perf measurement runs on a push to `main` and the nightly. CodeQL
  leaves pull requests for the push to `main` and its weekly schedule (#831).

- **A red nightly files its own bug card.** One `kind/bug` card per failing job in milestone `0.4.0`,
  naming the job and the run link, commented on while an open card for that job exists instead of
  filed again (#831).

- **The documentation has an entry point.** `docs/index.md` opens the user path with the governing
  thought — the docs are organised around what you want to do — and lists every task group with the
  page that owns each one; `scripts/docs-check` keeps that list equal to `docs/docs.json`.
  `docs/how-lunate-works.md` gives the mental model the guide held alone: the turn loop, the tools
  and approvals, the context and compaction, sessions and the three modes, each linking the page that
  owns the detail. `configuration.md` moves from Use it to Customize, the README's Get started and
  capability links lead into the index, and the moved-page stubs stay for one more release (#757, #844).

- **Every user-path page closes with Related, and its example is checked.** The extension authoring
  and reference pages now end with a **Related** section like the rest of the user path, and the
  pointer at the end of `docs/dev/README.md`'s writing guideline records the pyramid shape the
  0.4.0 information architecture asks for. Seven pages that quote a real config document or command
  gained the marker `scripts/docs-check` reads — the Roslyn MCP host config and the remote manifest
  are config examples, the package, trust, extension-test and troubleshooting commands are
  `run: no` with their reason — and the remaining `proseOnly` entries state why the page has
  nothing the checker can run instead of announcing a slice that already landed (#614, #845).

- **`AGENTS.md` and the `openspec-git-discipline` skill describe the lane the repository actually
  runs on.** One implementation pull request per card, the archive as its last commit landing before
  review, a rework redoing it, and the reviewer reading the proposal delta first; an unattended worker
  decides within the card, records its `grill-me` answers under **Intent decisions** in the pull
  request (a missing list is a send-back), and keeps propose-and-stop only for an ADR-level decision;
  a conflict under `openspec/specs/` is reverted and re-archived, never hand-edited. `CONTRIBUTING.md`
  stops describing the proposal waiting on `main` and the archive added after review, and the
  `repo-quality` spec records the same rules (#849).

- **The merge queue runs the tree checks, and `ci-ok` requires them.** A `merge_group` run now runs
  ADR numbering, the spec-archive rule, the generated docs regions, the docs link/navigation check and
  the docs lint against the batch's base commit, so a batch in which two pull requests each took the
  same free ADR number is rejected by the queue instead of reaching `main` (#854).

- **One command list for the tree checks.** `scripts/tree-checks.sh` holds the five commands, and both
  the merge-queue job and the push-to-`main` workflow call it, so the two triggers cannot drift
  (#854).

### Fixed

- **Reasoning parameters follow the resolved API family.** A model whose declared `reasoning`
  style disagrees with its resolved API family now sends the parameter that family accepts —
  Anthropic extended thinking for `anthropic-messages`, reasoning effort for the OpenAI-compatible
  and Google families — with the declared style kept as metadata and fallback (#601).

- **`--help` names the `cache prune` and `extensions reload` subcommands.** Both commands ran but
  neither appeared in the process synopsis; `lunate --help` now lists each with its one-line
  description, and a test pins the two lines against the help output (#780).

- **The first-run flake no longer depends on which task the thread pool runs first.**
  `InteractiveSessionFirstRunTests.Escape_dismisses_the_first_run_offer_without_a_write` waited for
  the picker's frame and then typed `/quit` in the window before the walk behind the offer had
  cleared the session's turn flag; the quit took the follow-up queue's path, which ends the run
  without clearing the input line, so the prompt still held the text. The fixture now waits for the
  dismissal to finish, and says in the test and its support file what it depends on (#817).

- **The project MCP composition tests record trust where a session reads it.** The four
  project-file scenarios now key their trust record by the repository identity — the git common
  directory when the temp workspace sits inside a repository, the worktree root otherwise — and
  name the file by the canonical path the composition reads, so the suite passes in a checkout as
  well as outside one. A dedicated scenario pins that identity seam without relying on where
  `TMPDIR` points (#824).

- **No current document names the pre-move `docs/spec/`, `docs/spikes/` or `docs/eval/` paths.** The
  `extensions` spec points at the extensibility contract and the Roslyn migration baseline in their
  `docs/dev/` homes, and `scripts/docs-check` no longer lists the old prefixes as records or
  maintainer pages, so a page reintroduced there is reported instead of silently exempt (#825).

- **The scripted-setup example runs as written.** The `lunate setup` example in `docs/ci.md` names
  a model that declares a reasoning style, carries a placeholder key and `--no-test`, and is marked
  `run: offline`, so `scripts/docs-check --run` executes it instead of a reader meeting exit 2
  (#826).

- **`ci-ok` judges the live head, not the run's payload.** A rerun of a draft or older run now reads
  the current head's newest required-job attempts through the API, a still-running ready gate fails
  with the jobs named, and draft and ready runs use separate concurrency groups, so a draft run can
  neither redden nor cancel the ready verdict (#830).

- **An incomplete gate is red.** `ci-ok` reads the verify matrix's shard count from the legs that ran
  and fails, naming it, when a leg the plan requires has no check run on the head, so a missing shard
  can no longer ride on the shards that passed (#830).

- **The tool-environment tests no longer race a session's settings resolution.** The
  `tool-process-environment` collection now runs alone instead of merely sequentially with its own
  tests, so a session composed by another test cannot clear the process-wide pass-through list
  between a test's configure and its spawn, and a guard test pins the collection definition (#846).

- **The specs name the documentation pages that exist.** The CI prose example points at
  `docs/dev/guide.md`, and the key-table requirement names `docs/keybindings.md` instead of the guide,
  so the source of truth no longer sends a maintainer to a redirect stub or to a page that does not
  own the table (#848).

- **The CI-gate decision record gets its own number.** Two records landed on `main` as ADR-0036, which
  fails `scripts/check-adr-numbers.py` and so reddens the `verify-docs` profile; the later one, the
  per-event CI gate, becomes `adr/0037-ci-gate-sized-by-event.md` while the earlier tool-child record
  keeps `0036` (#848).

- **The `ask_user` repaint test no longer depends on the question picker's 300 ms arm window.**
  `LiveSurfaceRepaintTests.The_question_tool_repaints_without_leftover_rows_at_40_columns` advanced
  the fake clock exactly 300 ms past the picker's frame and then typed its answer. When the arm
  landed inside that advance the answer fell on the window's closed edge and was dropped, so the run
  waited for a decision no key could deliver and the next chat call never started. The picker now has
  the approval panel's seam (the next key's arm and an expire that does not move the clock), and the
  harness's `WaitForQuestionAsync` returns only once the next key decides (#863).

- **The spawn descriptor-list test no longer reads another test's pipe ends.** Its
  `spawn-descriptor-table` collection now runs alone, so no process another collection starts can
  leave a descriptor of its own in the parent's table while the probe children start, and a
  reflection test pins the collection definition (#865).

- **The setup-wizard multi-select test answers the prompt it waited for.** The routing-profile
  description loop waited on the generic `What is profile '` prefix, which the previous prompt's
  frame still carries, so a loaded runner could accept the typed answer before the next prompt
  opened and queue it as a follow-up instead of answering the prompt; the menu row then never
  arrived. Each round now waits for its own profile's prompt, and the test passes under de-AT and
  en-US (#866).

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
