## MODIFIED Requirements

### Requirement: Tool commands use the seam; host support stays local

`BashTool` and every process-backed tool command SHALL run through `IExecutionEnvironment`.
`BashTool` SHALL send its command through the environment, and the ripgrep-backed search SHALL send
its invocation through the same environment. A background `bash` start SHALL likewise map its
command through the environment and SHALL start it through the environment's own start hook, so a
container job keeps its container-side cancel step; the job family SHALL NOT spawn a child beside
the seam. Host support subprocesses SHALL remain local and SHALL NOT be routed through the
tool-command environment: the editor and clipboard helpers, guard mechanism probes, the `ps`
process-table read used by the local tree kill, and bash's post-call host change scan.

#### Scenario: Bash uses the environment

- **GIVEN** a `BashTool` composed with an execution environment
- **WHEN** the model calls `bash`
- **THEN** the command is mapped and run by that environment

#### Scenario: A background bash start uses the environment

- **GIVEN** a `BashTool` composed with an execution environment
- **WHEN** the model calls `bash` with `background: true`
- **THEN** the command is mapped and started by that environment, and its kill uses that environment's cancel hook

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
