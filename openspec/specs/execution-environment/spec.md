# execution-environment Specification

## Purpose
Where a tool command's process runs. The seam maps the executable, argument prefix, working
directory and environment; it owns how a started command is cancelled and describes itself in one
line. Local execution is the only implementation in this change, and no tool-command process may
bypass the seam. Host support processes and the host-side file tools are deliberately outside it.

## Requirements

### Requirement: One tool-command execution-environment seam

Lunate SHALL define exactly one internal seam for tool-command processes:
`IExecutionEnvironment`. The seam SHALL expose:

- a one-line `Description` for a later header or `/context` line (`local` for the only
  implementation in this change);
- a mapping from a command's executable, argument prefix, working directory and environment to the
  concrete values the environment runs — with no path-translation table, because a bind-mounted
  workspace keeps one absolute path on both sides;
- a cancel hook for a command it started, so an implementation may end a container's process group
  instead of a host process tree;
- batch and interactive execution used by the same `IProcessRunner` seam the extension/MCP session
  and restore/Roslyn paths already consume.

`ProcessRunner` SHALL delegate its `RunAsync` and `StartSessionAsync` calls to the environment, so
the extension path cannot bypass the seam.

#### Scenario: A tool command is mapped before it runs

- **WHEN** a tool command reaches the environment
- **THEN** the environment receives its executable, argument prefix, working directory and
  environment through the mapping, and runs the mapped command

#### Scenario: The extension process path uses the same seam

- **GIVEN** an extension or MCP session that consumes `IProcessRunner`
- **WHEN** it starts a batch command or an interactive process
- **THEN** `ProcessRunner` delegates that call to the same execution environment

#### Scenario: The environment describes itself

- **WHEN** the active environment is read for a future header or `/context` line
- **THEN** it returns one short description, and the local implementation returns `local`

#### Scenario: Cancellation is the environment's hook

- **GIVEN** a tool command started by an environment
- **WHEN** the command is cancelled or times out
- **THEN** the environment's cancel hook ends it with that environment's semantics; the local
  implementation keeps the existing process-tree kill

### Requirement: Local execution is the only implementation

`LocalExecutionEnvironment` SHALL be the only `IExecutionEnvironment` implementation in this
change. It SHALL map commands unchanged and SHALL preserve today's spawn, output cap, timeout,
line-pump, environment, exit-code and tree-kill behaviour. The change SHALL NOT add an environment
flag, setting, public API or file-system abstraction.

#### Scenario: The local mapping changes nothing

- **GIVEN** a command with an executable, arguments, working directory and environment
- **WHEN** the local environment maps it
- **THEN** the executable, arguments, working directory and environment are unchanged

#### Scenario: Existing process behaviour is unchanged

- **GIVEN** the existing `ProcessRunner` and `ProcessSession` suites
- **WHEN** they run through the local environment
- **THEN** exit codes, streams, output caps, line exchange, environment replacement and tree kills
  behave exactly as before

### Requirement: Tool commands use the seam; host support stays local

`BashTool` and every process-backed tool command SHALL run through `IExecutionEnvironment`.
`BashTool` SHALL send its command through the environment, and the ripgrep-backed search SHALL send
its invocation through the same environment. Host support subprocesses SHALL remain local and SHALL
NOT be routed through the tool-command environment: the editor and clipboard helpers, guard
mechanism probes, the `ps` process-table read used by the local tree kill, and bash's post-call
host change scan.

#### Scenario: Bash uses the environment

- **GIVEN** a `BashTool` composed with an execution environment
- **WHEN** the model calls `bash`
- **THEN** the command is mapped and run by that environment

#### Scenario: Process-backed search uses the environment

- **GIVEN** the ripgrep search engine composed with an execution environment
- **WHEN** a grep search runs
- **THEN** the ripgrep invocation is mapped and run by that environment

#### Scenario: The bash change scan stays on the host

- **GIVEN** a `BashTool` composed with an execution environment in a git workspace
- **WHEN** the command changes a tracked file
- **THEN** the post-call change scan runs locally, outside the tool-command environment, and still
  publishes the change

#### Scenario: Host support is not routed through the seam

- **GIVEN** the editor, clipboard, guard-probe and `ps` process starts
- **WHEN** they run
- **THEN** they start locally and never call an `IExecutionEnvironment`

### Requirement: File tools stay host-side and the inventory has no silent bypass

The direct file tools `read`, `write`, `edit`, `ls`, `glob` and `grep`'s file reads SHALL remain on
host filesystem access and SHALL NOT be routed through the execution environment, because they
never spawn a process and the workspace is mounted at the host's absolute path. Every process-start
site in `src/` SHALL be classified as either a tool command (through the seam) or host support
(local), and a test SHALL fail when a new site appears without a classification.

#### Scenario: File tools keep host access

- **GIVEN** a read, write or edit call
- **WHEN** the workspace files are accessed
- **THEN** the tool uses host filesystem access and never asks an execution environment to run a
  process

#### Scenario: An unclassified process start fails the inventory

- **GIVEN** a new direct process start under `src/`
- **WHEN** the inventory test runs
- **THEN** it fails until the site is classified as a tool command or as host support
