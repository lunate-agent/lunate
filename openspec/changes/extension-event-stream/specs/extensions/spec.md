## ADDED Requirements

### Requirement: Observable event stream

Next to the intercepting hook catalogue the contract SHALL offer one observing surface:
`IExtensionContext.Events`, a `System.IObservable<AgentEvent>` typed with the contract's event union.
The observable SHALL be BCL `IObservable<T>` so the contract adds no package and pins no reactive
library; an extension that wants operators brings its own. Subscribing SHALL be registration-only and
SHALL NOT start a process, socket or timer. The stream SHALL be hot and session-scoped: one
subscription per extension, live until the extension unloads or the session ends, carrying every
run, every child run with its ids intact and `ParentRunId` set, and every extension event.

Each subscription SHALL have its own bounded queue drained by its own pump, so a slow subscriber
delays only itself and the run's path never waits on an observer. When a subscription's queue is
full, the oldest event SHALL be dropped, counted per subscription, warned about once per
subscription naming the extension, and reported by `/extensions`. `OnNext` SHALL be called from the
subscription's own pump, not from the run. A throw from any observer callback SHALL be caught,
logged once naming the extension, and drop that subscription only; the run and the other
subscriptions SHALL be unaffected. `OnError` SHALL never be used for a failed run (a failed run is a
`RunError` event); `OnCompleted` SHALL fire once per subscription at session end or when the
extension unloads, after the events already queued, and a publisher SHALL NOT deliver after it.
Disposing the subscription SHALL stop its pump.

The stream SHALL carry the same `AgentEvent` records `--json` and ACP render. It SHALL NOT introduce
a second event model, a hook payload per event kind, an ordering guarantee across observers, or any
way for an observer to change a run.

#### Scenario: An extension observes a complete turn

- **GIVEN** an installed in-process extension that only subscribes to `IExtensionContext.Events` and
  registers no hook handler
- **WHEN** a session runs one turn
- **THEN** the extension observes the turn's events in emission order, from its `RunStarted` to its
  `RunFinished`, without having intercepted or changed anything

#### Scenario: A child run's events arrive on the same stream

- **GIVEN** a subscription during a run that spawns a subagent child
- **WHEN** the child run emits
- **THEN** the subscriber sees the child's events with the child's run id and `ParentRunId` set, in
  the same stream as the parent's

#### Scenario: A slow subscriber loses events, not the run

- **GIVEN** a subscriber whose `OnNext` is slower than the run
- **WHEN** more events are published than its queue holds
- **THEN** the oldest events are dropped, the drop is counted, exactly one warning names the
  extension, `/extensions` shows the count, and the run and the other subscribers are unaffected

#### Scenario: A throwing observer drops only its own subscription

- **GIVEN** two subscriptions in one session, one of which throws from `OnNext`
- **WHEN** the next event is published
- **THEN** the throwing subscription is dropped with one log line naming the extension, and the other
  subscription keeps receiving events

#### Scenario: Completion fires once at session end

- **GIVEN** a live subscription
- **WHEN** the session ends or the extension unloads
- **THEN** the subscription receives `OnCompleted` exactly once after its queued events, and later
  publishes reach it no more

#### Scenario: The intercepting hooks are unchanged

- **GIVEN** an extension registering hook handlers and no subscription
- **WHEN** a turn runs
- **THEN** the hook catalogue keeps its ordering, timeouts and failure policies, and the extension
  behaves exactly as it did before the stream existed

## MODIFIED Requirements

### Requirement: Extension contract assembly

Extensions SHALL compile against `Lunate.Extensibility.Abstractions`, `Lunate.Contracts` and
`Lunate.Decisions.Model`, plus `Microsoft.Extensions.AI.Abstractions`; they SHALL NOT reference
`Lunate.Agent` or other Lunate assemblies directly. The tool contract types (`ITool`, `ToolContext`,
`ToolResult`, `ToolResultView`, tool declarations, and tool annotations including `ToolAnnotations`,
`ToolExposure` and `ToolRisk`) and the complete `AgentEvent` union SHALL live in
`Lunate.Contracts`; the old namespaces SHALL remain reachable through type forwards in their former
assemblies. `Lunate.Extensibility.Abstractions` SHALL reference `Lunate.Contracts` and
`Lunate.Decisions.Model`, and SHALL NOT reference `Lunate.Agent` or `Lunate.Ai.Decisions`.
`Lunate.Contracts` SHALL depend only on the BCL and `Microsoft.Extensions.AI.Abstractions`. The
extension contract SHALL be semver'd with a PublicAPI-tracked surface, and its entry point SHALL be
`IExtensionFactory.Create(IExtensionContext)` returning an `IExtension`. The context SHALL expose the
extension id, settings, secrets, a log sink and the observable event stream. The current API version
SHALL be a single constant. `Lunate.Contracts`, `Lunate.Extensibility.Abstractions` and
`Lunate.Decisions.Model` SHALL be versioned in lockstep under `ExtensionApi`.

#### Scenario: An extension compiles against the contract only

- **GIVEN** an extension project referencing `Lunate.Contracts`,
  `Lunate.Extensibility.Abstractions`, `Lunate.Decisions.Model` and
  `Microsoft.Extensions.AI.Abstractions`
- **WHEN** it is built
- **THEN** it compiles without a direct reference to `Lunate.Agent`, `Lunate.Ai.Decisions` or another
  Lunate assembly

#### Scenario: The API version is queryable

- **GIVEN** the extension contract assemblies
- **WHEN** the current API version is read
- **THEN** it is a single semver constant that the range matcher validates against, and all three
  assemblies have that API version

#### Scenario: The observing surface is a BCL observable

- **GIVEN** a source file that names `IExtensionContext.Events`
- **WHEN** it is compiled against the contract
- **THEN** it needs no reactive package: the property's type is `System.IObservable<AgentEvent>`,
  which the BCL provides

### Requirement: Extension manifest

An extension SHALL declare an `extension.json` manifest with `id` (lowercase, `[a-z0-9-]+`),
`version`, `apiVersion` (a range), exactly one of `entryAssembly` (an in-process extension) or
`runtime` (an out-of-process extension) and optional `tools`, `commands`, `hooks`, `services`,
`events`, `writeVerifiers`, `settingsSchema` and `capabilities`. `events` SHALL be the `--json` kind
vocabulary or `["*"]`: an array of non-empty strings without duplicates, where `*` is accepted only
alone. Each `writeVerifiers` entry SHALL declare a unique `name` and one or more filename patterns.
Patterns SHALL be non-empty filename globs without path separators and SHALL match normalized
basenames case-insensitively. `runtime` SHALL mirror the MCP server registration shape — `command`,
`args`, `workingDirectory`, `env` (values to set), `envPassthrough` (names inherited from the
parent), `callTimeout` — and a manifest with both `entryAssembly` and `runtime`, or with neither,
SHALL be refused naming the file and the conflict. Unknown top-level fields SHALL be ignored;
malformed known fields SHALL fail with an error naming the file and the field; duplicate
declarations SHALL be rejected. The API-version range grammar SHALL be exact `x.y.z` or caret
`^x.y.z`; other forms SHALL be refused as unsupported. An incompatible range SHALL be refused with a
message naming the extension, its range and the current API version.

#### Scenario: A valid manifest parses

- **GIVEN** a manifest with all required fields, a `writeVerifiers` entry with `name` and `patterns`,
  and an `events` list
- **WHEN** it is parsed
- **THEN** the parsed model carries every declared field and defaults optional arrays to empty

#### Scenario: Unknown fields are forward-compatible

- **GIVEN** a manifest with an unknown top-level field
- **WHEN** it is parsed
- **THEN** it parses successfully and the unknown field is ignored

#### Scenario: An incompatible range is refused

- **GIVEN** a manifest whose `apiVersion` excludes the current API version
- **WHEN** it is validated
- **THEN** the refusal names the extension, its range and the current API version

#### Scenario: A wildcard event declaration stands alone

- **GIVEN** a manifest whose `events` list is `["*", "run_started"]`
- **WHEN** it is validated
- **THEN** it is refused with the manifest path and the `events` field in the diagnostic

#### Scenario: A remote manifest parses

- **GIVEN** a manifest with `runtime` instead of `entryAssembly`
- **WHEN** it is parsed
- **THEN** the parsed model carries the runtime command, args, working directory, env, envPassthrough
  and call timeout

#### Scenario: A manifest must declare exactly one runtime

- **GIVEN** a manifest with both `entryAssembly` and `runtime`
- **WHEN** it is validated
- **THEN** the refusal names the file and the conflict

#### Scenario: Invalid verifier patterns name the field

- **GIVEN** a manifest with an empty pattern, a path separator, or a duplicate verifier name
- **WHEN** it is validated
- **THEN** it is refused with the manifest path and `writeVerifiers` field in the diagnostic

### Requirement: Header directives declare what the file provides

The header SHALL mirror the packaged manifest's declaration fields one to one — `#:tool`,
`#:command`, `#:hook`, `#:service`, `#:event`, `#:capability` and `#:provider` for the manifest's
`tools`, `commands`, `hooks`, `services`, `events`, `capabilities` and `modelProviders` — so a single
file can never be unable to register something a package can. The header is the only declaration
source for a single file; there is no sidecar manifest in this version. Directives SHALL be parsed
before any compilation and their bytes SHALL be part of the trusted source digest. A registration the
header does not declare SHALL refuse the load, naming the registration and the missing directive. A
declared registration the file never makes SHALL be warned and shown by `/extensions` as
`declared, not provided`.

#### Scenario: An undeclared tool registration is refused

- **GIVEN** a single-file extension whose header declares `#:tool fetch_issue` only
- **WHEN** the file also registers a tool named `close_issue`
- **THEN** the load is refused, naming `close_issue` and the missing `#:tool close_issue`

#### Scenario: Every manifest declaration kind has a directive

- **GIVEN** the packaged manifest's set of declaration fields
- **WHEN** the header grammar is inspected
- **THEN** each field maps to a directive, and a test fails if a manifest field is added without one

#### Scenario: A declared tool that is never registered is a warning

- **GIVEN** a header declaring `#:tool fetch_issue` and a body that registers nothing
- **WHEN** the extension loads
- **THEN** the extension is loaded, and `/extensions` shows `fetch_issue` as `declared, not provided`

### Requirement: The `/extensions` view

The CLI SHALL provide an interactive `/extensions` command and a `lunate extensions [--json]`
subcommand listing every discovered extension — failed first, then trust-denied, then loaded, each
group by id — with its id, version, scope (`global`/`project`/`builtin`) and state (`loaded`,
`trust-denied` or `failed`). Loaded extensions SHALL show their load time in milliseconds (a load
above 500 ms marked `(slow)`) and their **registered** tool, command, hook and service counts, a
field differing from the manifest's declared list carrying `(N declared)`; trust-denied and failed
extensions SHALL show the manifest's **declared** counts, and a failed extension's message SHALL be
truncated to its first sentence followed by ` — see the log` when truncated. A loaded extension that
declares `events` SHALL show what it watches, and one with a live subscription SHALL show how many
events it has seen and how many were dropped, the drop count appearing only when it is non-zero; an
extension that declares and sees none gains no clause, so a quiet extension stays one line. The
interactive form SHALL render one segment per extension in the single-line notice discipline
(segments joined `;`, prefixed `extensions: `); the subcommand SHALL print one line per extension
with load times labelled `(CLI-measured)`, or with `--json` a machine-readable array of the same
fields (`loadMs`, `slow` where applicable; `events` and the subscription counters where they apply),
and SHALL compose the host in register-only inspect mode — loading and registering trusted
extensions but starting no session events, background services or MCP servers, unloading each
extension and stopping the host before exit — and SHALL never prompt, so an extension it cannot
trust reads `trust-denied`. It SHALL exit non-zero when any extension failed. A built-in extension
SHALL appear with scope `builtin`, state `loaded`, its registered counts, and never `pending` or
`trust-denied`; the interactive form SHALL offer an unload/reload action for it and
`lunate extensions reload <id>` and unloading by id SHALL be refused with a teaching message naming
the built-in and the reason (a built-in is process-lifetime code with no ALC to release). With no
extensions discovered, the interactive form SHALL read `extensions: none configured` and `--json`
SHALL print `[]`; with extensions disabled (`--no-extensions`) the interactive form SHALL read
`extensions disabled (--no-extensions)` and the composed built-in segments (scope `builtin`, state
`loaded`) SHALL follow the same reading and listing as with extensions enabled. A loaded extension's
enabled tool state SHALL be visible: a declared tool whose `toolActivation` rule is false for the
run's workspace SHALL be reported as `inactive (<condition>): <reason>` after the tool counts, and a
true rule SHALL count as active, with the registered/declared tool counts unchanged by the rule. The
view SHALL never print settings values, secrets or environment values, and numbers SHALL be formatted
culture-invariantly.

#### Scenario: A session shows each extension's scope, state, load time and contributions

- **GIVEN** a session with one loaded global extension and one project extension whose trust was denied
- **WHEN** `/extensions` is submitted
- **THEN** one segment per extension appears ordered failed, trust-denied, loaded — the loaded one showing `(global) loaded in N ms` and its registered counts, the denied one showing `(project) trust-denied` and its declared counts

#### Scenario: A built-in segment cannot be unloaded or reloaded

- **GIVEN** a session with one composed built-in extension
- **WHEN** `/extensions` is submitted and unload or reload is attempted for its id
- **THEN** the segment reads `(builtin) loaded` with its registered counts, the attempt is refused with a teaching message naming the built-in, and the built-in stays loaded

#### Scenario: The subcommand is register-only and never prompts

- **GIVEN** a store with one trusted global extension whose background service and session hooks write markers, and one untrusted project extension
- **WHEN** `lunate extensions` runs non-interactively
- **THEN** the factory ran (registration marker present) but no service-start or session-hook marker exists, nothing prompted, no trust was recorded, the host stopped, and the exit code is 0; with `--json` the same data prints as an array of objects

#### Scenario: A failed extension makes the subcommand exit non-zero

- **GIVEN** a store with an extension whose load fails
- **WHEN** `lunate extensions` runs
- **THEN** its line reads `failed` with the first sentence of the message and ` — see the log` when truncated, and the exit code is non-zero

#### Scenario: The empty and disabled views

- **GIVEN** no extensions are discovered (or the run carries `--no-extensions`)
- **WHEN** `/extensions` is submitted (or `lunate extensions` runs)
- **THEN** the interactive form reads `extensions: none configured` (`extensions disabled (--no-extensions)` when disabled) and `--json` prints `[]`, and with a built-in composed under `--no-extensions` the built-in segments follow the same reading and listing as with extensions enabled

#### Scenario: An inactive tool is reported with its rule

- **GIVEN** a loaded extension whose manifest declares a tool with a false activation rule and a tool with a true rule
- **WHEN** `/extensions` is submitted (or `lunate extensions` runs)
- **THEN** the false-rule tool reads `inactive (<condition>): <reason>` and the registered/declared tool counts are unchanged for both tools

#### Scenario: A watcher's declarations and drops are visible

- **GIVEN** a loaded extension that declares `events` and has subscribed
- **WHEN** `/extensions` is submitted
- **THEN** its segment names what it watches and how many events it has seen, and the dropped count
  appears only when events were dropped

### Requirement: Hook contract
The contract assembly SHALL define typed payload/result DTOs and handler interfaces for every hook in the catalogue (`ProjectTrust`, `SessionStarted`, `SessionEnding`, `InputReceived`, `RunStarting`, `ContextBuilding`, `ProviderStreamEvent`, `MessageCompleted`, `ToolCalling`, `ToolResultReady`, `TurnEnded`/`RunSettling`, `RunSettled`, `Compacting`, `ModelChanged`/`ToolsChanged`). Every payload and result SHALL round-trip through `System.Text.Json` (conformance-tested). Extensions SHALL register handlers through `IExtensionContext.Register` at creation time only — registration SHALL NOT start processes, sockets or timers. The API version SHALL move as a semver-minor addition. Observing SHALL NOT be expressed as hooks: the event stream is the observing surface, and the catalogue SHALL NOT gain a payload or a result per event kind.

#### Scenario: Every hook DTO round-trips through JSON
- **GIVEN** a payload or result DTO of any catalogue hook
- **WHEN** it is serialized and deserialized
- **THEN** the values are identical

#### Scenario: Registration is registration only
- **GIVEN** an extension registering handlers
- **WHEN** its factory runs
- **THEN** no processes, sockets or timers are started
