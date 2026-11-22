## ADDED Requirements

### Requirement: Remote observable event stream

The observable event stream SHALL reach an out-of-process extension over the wire as a declared
projection, never as a second event model. The projection SHALL be the same JSON object one
`--json` line carries — `type`, `runId`, `sessionId` when present, `parentRunId` on a child run,
`source` when it is not `core`, and the event's own fields — sent host → child as an `event/<kind>`
notification whose params are that object, where `<kind>` is the `--json` type name. The host SHALL
price a `usage_updated` record from the session catalog, so the params carry the same optional
numeric `costUsd` the `--json` line carries and omit it for an unpriced record, never `0`. Event
notifications SHALL never be awaited, never be answered and never carry a result: an observer has no
opinion and no place on the run's path.

The handshake SHALL negotiate the projection in both directions: the `initialize` request SHALL
carry the manifest's `events` watch list, and the result SHALL carry the event kinds the child
accepts (or `["*"]`). The wire protocol version SHALL be 3. A child that answers a version below 3
SHALL still load when the manifest declares no `events`; when the manifest declares `events`, the
handshake SHALL refuse the extension with a reason naming protocol version 3, never ignore the
declaration. A version-3 child that accepts no kinds while the manifest declares some SHALL be
warned about and sent none; the manifest's declaration SHALL be the projection filter, so a child
that accepts more kinds than the manifest declares SHALL never receive them.

The host SHALL subscribe to the session's event stream once per started child and SHALL drain the
subscription through one pump with a bounded queue of 256 events, so the run never waits on a
child's stdin. On overflow the oldest event SHALL be dropped, counted and warned about once naming
the extension; an event whose line exceeds the wire's line cap SHALL be dropped with one warning
naming the extension and the kind rather than breaking the wire. The subscription SHALL be disposed
when the child stops, crashes or is restarted, and a restart SHALL subscribe again. A remote
extension whose manifest declares `events` SHALL start at session start even when it declares no
hook, service or command, so an observer-only extension is live before the first run emits.

The wire DTOs of the projection SHALL live in `Lunate.Extensibility.Remote`; no wire type SHALL be
added to `Lunate.Extensibility.Abstractions`, which SHALL NOT reference the wire package. A
conformance inventory SHALL cover every `AgentEvent` record so a new event type cannot ship without
a wire round-trip.

#### Scenario: A remote extension observes a turn end to end

- **GIVEN** an installed remote extension declaring `events` whose child accepts them and speaks
  protocol 3
- **WHEN** a session runs one turn through the host
- **THEN** the child receives `event/run_started` through `event/run_finished` in emission order,
  each carrying the `--json` line object with its run id, and has changed nothing about the run

#### Scenario: A version-2 child still loads, and names protocol 3 when events are declared

- **GIVEN** a remote child that answers protocol 2
- **WHEN** the manifest declares no `events`
- **THEN** the extension loads and its hooks and services work exactly as before
- **AND WHEN** the manifest declares `events`
- **THEN** the handshake refuses the extension with a reason naming protocol version 3, and the
  session continues without it

#### Scenario: The manifest declaration filters the wire only

- **GIVEN** a remote manifest declaring `["tool_call_result"]` whose child accepts `["*"]`
- **WHEN** a turn emits `run_started`, `tool_call_result` and `run_finished`
- **THEN** the child receives only `event/tool_call_result`, while an in-process subscription in the
  same session still receives every event

#### Scenario: A slow child loses events, not the run

- **GIVEN** a started remote extension whose child stops reading stdin
- **WHEN** more events are published than the projection's queue holds
- **THEN** the oldest events are dropped, the drop is counted, exactly one warning names the
  extension, and the run and the other extensions are unaffected

#### Scenario: An oversized event is dropped, not the wire

- **GIVEN** a started remote extension and an event whose JSON line exceeds the wire's line cap
- **WHEN** it is projected
- **THEN** that event is dropped with one warning naming the extension and the kind, and the next
  event still reaches the child

#### Scenario: A priced usage line is the same object with its cost

- **GIVEN** a started remote extension watching `usage_updated` and a session whose catalog prices the
  record
- **WHEN** the run emits that `usage_updated`
- **THEN** the notification's params carry the `costUsd` number the `--json` line carries, and an
  unpriced record's params omit the field

#### Scenario: Every observable record round-trips and the boundary holds

- **GIVEN** the observable event conformance inventory
- **WHEN** every `AgentEvent` record is projected and sent over a real wire pair
- **THEN** each notification's method and params parse to the same JSON object `--json` writes, the
  inventory covers every event record, and no wire type is defined in or referenced by
  `Lunate.Extensibility.Abstractions`

## MODIFIED Requirements

### Requirement: Out-of-process extension host

An extension whose manifest declares `runtime` SHALL run as a child process instead of an assembly load: discovery, scope and failed-extension containment SHALL be identical to in-process extensions, except that trust SHALL hash the manifest together with the extension directory contents (entry script and bundled files) — swapping a bundled file after trust SHALL re-prompt — and the trust prompt SHALL state that the extension runs with the user's privileges. The host SHALL build hook and service proxies from the manifest's declarations without starting anything, and SHALL start the process lazily on the extension's first use — never during discovery, load, registration or composition — through the core process capability; a manifest startup mode `background` SHALL instead start it after the interactive UI is ready, never on the startup path, and a subscribed `session-started` SHALL be delivered when the session is live (the same post-ready point) or on the child's first successful start, whichever comes first, so no child ever starts on the startup path. The one exception is the observable projection: an extension whose manifest declares `events` SHALL start at session start — even without a hook, service or command, and even when it also declares `background` — so the projection is live before the first run emits (see *Remote observable event stream*), and `background` then still decides when the deferred `session-started` is delivered. A relative `command` or argument path SHALL resolve against the extension directory and the default working directory SHALL be the extension directory. The child's environment SHALL be exactly the minimal base set (PATH, HOME/USERPROFILE, SYSTEMROOT, TEMP/TMP/TMPDIR, LANG) plus `envPassthrough` plus `env`, with no inheritance — Lunate's provider keys SHALL NOT leak — built by the shared scrubber (the code-mode sample's implementation, promoted into the contract assembly; one implementation). The wire SHALL be newline-delimited JSON-RPC 2.0: UTF-8, no BOM, no batches; stdout SHALL be protocol-only — a non-JSON line is a protocol error and a line over 16 MB is a protocol error, never an OOM — and stderr SHALL be captured into the extension log, capped. The first call SHALL be an `initialize` request carrying `protocolVersion` and capabilities, the manifest echo, the workspace info and the declared hooks and services; a version mismatch SHALL refuse the extension with a clear status — never best effort — and the result SHALL match the manifest: a hook or service the child implements but the manifest does not declare SHALL be refused, a declaration the child does not implement SHALL be warned (the in-process rules, unchanged). The channel SHALL be bidirectional and reentrant — the child may call host services while handling a hook — with separate id spaces per direction and concurrent requests allowed. Cancellation SHALL map `$/cancelRequest` notifications to `CancellationToken` in both directions. The host SHALL forward only the hooks the manifest subscribes to: observer hooks (`SessionStarted`, `SessionEnding`, `ProviderStreamEvent`, `RunSettled`, `ModelChanged`, `ToolsChanged`, `FileChanged`) SHALL be notifications — fire-and-forget, and `ProviderStreamEvent` deltas SHALL not be forwarded unless explicitly subscribed — while interceptor hooks (`ProjectTrust`, `InputReceived`, `RunStarting`, `ContextBuilding`, `MessageCompleted`, `ToolCalling`, `ToolResultReady`, `TurnEnded`, `Compacting`) SHALL be requests. Timeouts and failures SHALL follow the catalogue: observers drop with a warning; interceptors take the hook's no-opinion semantics (keep original / skip) with a warning, except the gating hooks — `ToolCalling` blocks fail-safe and `ProjectTrust` denies. Every call SHALL carry the run's ids (RunId, and ParentRunId when present) and the workspace; one child SHALL serve the whole session, subagent and worktree runs included. Service calls SHALL cross both ways — consumption by name through the registry, and calls on services the remote extension registered — as JSON-RPC requests with JSON arguments and results, and services a remote extension registers SHALL be async-only (in-process consumers SHALL NOT call them on a synchronous hot path). A process that exits SHALL fail in-flight calls immediately, without waiting for their timeouts, with one notice, flip the extension to a failed status with the reason `the extension process exited`, and the session SHALL continue; the host SHALL attempt at most one automatic restart per session, after which the extension reads failed until a manual restart from `/extensions`. At session end the host SHALL send the shutdown request, allow a bounded grace, then kill the process tree — a Job Object on Windows, the children-first POSIX walk on Unix. The process boundary SHALL provide runtime isolation — dependencies, language, crash containment — and SHALL NOT be described or treated as a permission boundary.

#### Scenario: A remote extension starts lazily and answers a hook

- **GIVEN** an installed remote extension whose manifest declares a hook
- **WHEN** the hook first fires in a session
- **THEN** the process starts, the handshake succeeds and the hook's result crosses back and takes effect

#### Scenario: A protocol version mismatch refuses

- **GIVEN** a remote extension whose `initialize` result reports a different protocol version
- **WHEN** the extension first runs
- **THEN** it is refused with a clear status, never driven best-effort, and the session continues

#### Scenario: A hook handler calls back into the host

- **GIVEN** a started remote extension handling a hook
- **WHEN** its handler calls a host service before answering
- **THEN** the service call is answered while the hook call is in flight, and the hook's result still lands

#### Scenario: Declarations and the initialize result must agree

- **GIVEN** a remote extension whose initialize result implements a hook its manifest does not declare
- **WHEN** the handshake completes
- **THEN** the extension is refused; a declared-but-unimplemented hook only warns

#### Scenario: A timed-out gating hook fails closed

- **GIVEN** a remote `ToolCalling` handler that does not answer within its timeout
- **WHEN** the tool call dispatches
- **THEN** the call is blocked with a reason and the failure is reported

#### Scenario: A crash fails fast and restarts at most once

- **GIVEN** a started remote extension with a hook call in flight
- **WHEN** its process exits
- **THEN** the in-flight call fails immediately with one notice, at most one automatic restart is attempted, and after that the extension reads failed until a manual restart from `/extensions`

#### Scenario: The child environment is exactly base plus passthrough plus env

- **GIVEN** a remote extension whose manifest declares one environment variable and one passthrough name, and a parent process holding a provider key
- **WHEN** the child starts
- **THEN** the declared variable and passthrough name are present, the provider key is absent, and nothing else is inherited

#### Scenario: Session end stops the process within the grace

- **GIVEN** a started remote extension
- **WHEN** the session ends
- **THEN** the shutdown request is sent, the process exits within the grace window, and a process that ignores it is killed children-first

#### Scenario: A non-JSON stdout line is a protocol error

- **GIVEN** a remote extension that writes a non-JSON line to stdout
- **WHEN** the host reads it
- **THEN** it is treated as a protocol error, never as protocol data

#### Scenario: A background extension starts after the UI is ready

- **GIVEN** a remote extension whose manifest declares the `background` startup mode and no `events`
- **WHEN** an interactive session starts
- **THEN** the process starts only after the UI is ready, and the startup path and its budget are untouched

#### Scenario: Trust covers the extension directory

- **GIVEN** a trusted remote extension whose bundled entry script changes
- **WHEN** the next session starts
- **THEN** the trust prompt fires again
