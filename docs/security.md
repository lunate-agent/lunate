# Run Lunate safely

This page answers what stands between the model and your machine, and the short answer is: Lunate
runs with your user privileges, asks before writes and commands, and guards its own state at the OS
level — it is not a sandbox. To report a vulnerability, use the [security policy](../SECURITY.md).

## The trust model in one page

Lunate runs with your user privileges, and its security posture is deliberately that of a
single-user CLI tool, not a sandbox. The boundaries are worth knowing about.

**Approval prompts.** The `read`, `write`, `edit`, `bash`, `remember`, `web_search` and `web_fetch`
tools all run as you, on model-chosen arguments. `bash_output` is read-only and reads only the session's own
jobs; `bash_kill` is `Execute` risk because it ends processes, so it takes the same approval path as
`bash`. Every write, edit, command, kill and web call is shown as a prompt
— `Allow edit src/Order.cs?  [y]es  [n]o  [a]lways this session` — before it runs, and the
`approval` setting (`ask`, the default, or `auto-edit`) decides what still asks. `--yolo` approves
everything for one run and is never a saved setting. Print mode has nobody to ask, so it maps the same policy onto tool risk and refuses what it
may not approve, turning a refusal into an error result the model can read; ACP mode asks the
editor's own permission request for everything the policy does not already allow, and has no
`--yolo`.

The web tools have no workspace boundary: `web_fetch` can request any http or https URL the model
names, and `web_search` sends the query to the configured search backend — DuckDuckGo by default, or
a SearXNG, Brave or Tavily endpoint the user selected in the user-scoped `tools.webSearch` setting.
Both are `Execute` risk, so every call is approved. Neither reads the state root, and a declined call
sends nothing. A backend that needs an API key holds a credential *reference* (`auth:`, `env:` or
`helper:`), never the value: the reference is resolved when the approved call is sent, the value
appears only in the one header that backend's API expects, and an `env:` name it resolves is removed
from tool child processes like a provider key. A project settings file cannot select a backend, so a
repository cannot redirect a search or spend your key.

Provider-native search is the exception that needs an explicit opt-in: `tools.nativeSearch` is a
user-scoped boolean, off by default, that lets a model declaring the `web_search` feature run the
search inside the chat request. There is no per-call prompt for that search — it is part of the chat
call — which is exactly why a project settings file cannot switch it on. The local `web_search` tool
is not offered while it is active, and the search is billed and attributed to the chat call.

**The workspace boundary.** The file tools resolve paths canonically, following symlinks, and refuse
anything outside the working directory, so a model mistake cannot read or write next to your home
directory by accident ([ADR-0016](../adr/0016-workspace-boundary.md)). This is a guard rail, not an
isolation boundary: `bash` is intentionally not boundary-checked — the approval policy is its guard
— and time-of-check/time-of-use races are accepted. `remember` has no path argument: it targets only
the repository root's `LUNATE.md`, and refuses a symlink whose real target leaves that root.

**The OS-level guard.** Beyond the approval policy, `bash` is not path-confined, but on macOS and
Linux it runs under an OS-level guard ([ADR-0024](../adr/0024-bash-guard.md)) that refuses writes under
the state root (`~/.lunate`, `LUNATE_HOME`) and the install prefix and read-denies `auth.json` —
`sandbox-exec` on macOS, bubblewrap on Linux with `setpriv`'s Landlock rules as the fallback, so Lunate
guards its own state at the OS level. When the mechanism cannot start (for example Ubuntu
23.10+'s AppArmor restriction), the command still runs with a visible notice and the result says
so. Windows has no mechanism yet and reports it rather than running unconfined silently. A session
that already runs inside a sandbox is the nested case: macOS refuses to apply a second, nested
`sandbox-exec` profile (`sandbox_apply: Operation not permitted`), so Lunate cannot add its own
state-root, install-prefix and `auth.json` rules there. The guard says that once per session and
does not claim the command runs unconfined — the outer sandbox still applies. None of the guard
mechanisms makes Lunate a sandbox: the guard does not stop a reachable Docker socket, a
pre-existing hardlink into the state root, or anything inside the workspace. `bashGuard: false`
turns it off, and the header and tool results name that state.

**The container boundary.** Starting Lunate with `--env podman:<image>` or `--env docker:<image>`
(or the user-scoped `environment` setting) runs `bash` and every process-spawning tool through the
engine's `exec` in one long-lived container. Only the workspace is mounted, at the same absolute
path as on the host; the state root (`LUNATE_HOME`), the install prefix and `auth.json` are never
mounted. `read`, `write` and `edit`, write verification, approvals and the file-change bus stay on
the host and work on the mounted workspace; a file written by a container command is published by
the host's post-call scan. The OS-level guard still wraps the host `exec` client and refuses a
state-root write before the container is touched. It does not follow the command inside the
container: the accepted policy is process-only, so the mount boundary — the workspace, and nothing
of Lunate's state — is what limits that command's reach, and no guard runs inside the container.
The container is not a jail: the engine socket, a pre-existing hardlink and the workspace itself
remain reachable.
[Run tool calls in a container](execution-environments.md) has the flags, the flow and the
measured limits.

**Repository-provided code.** A project extension, a project `mcp.json` file, each server it defines
and a project skill are code from the repository, so each asks once per repository before it runs
and asks again when its content changes. Answers live in `~/.lunate/trust.json`, per user and
machine; `/trust` lists them and clears them, and `lunate trust` inspects and records them for
scripted runs. Denial refuses the load, and untrusted input is never auto-approved — an ephemeral CI
runner starts untrusted every time. In print and ACP mode an untrusted project item is refused with
the approval paths named.

**Extensions.** In-process extensions are fully trusted: the manifest and the loader isolate
dependencies, not permissions, and a malicious in-process extension is as powerful as Lunate itself
([ADR-0017](../adr/0017-extensibility-architecture.md)). An out-of-process extension is a separate
process but runs with your user privileges — the process boundary provides dependency, language and
crash isolation, and it must never be described or treated as a permission boundary. Global
extensions in `~/.lunate/extensions/` are not gated, because installing one is already a decision you
made; project extensions always ask.

### How the layers compose

The approval panel, the workspace boundary, the bash guard, verified edits, the file-change bus and
the container boundary answer different questions and compose; the container replaces neither the
approval panel nor the guard.

| Layer | Question it answers | What it does | What it does not do |
| --- | --- | --- | --- |
| Approval panel | Did you intend this command? | Shows the command or diff and applies the approval policy; a command that changes trust or writes the state root always asks. | Does not limit what the command can reach once you say yes. |
| Workspace boundary | Are the file tools inside the workspace? | Resolves real paths and refuses anything outside the working directory. | Does not apply to `bash`. |
| BashGuard | Can the command write Lunate's host state or read the key? | OS-level deny on the state root and install prefix and an `auth.json` read-deny; wraps the host process and names itself on refusal. | Does not follow an inner container command; Windows has no mechanism yet; a process already under `sandbox-exec` cannot take a nested profile, so the guard's own rules are absent there (it says so, once). |
| Verified edits | Was a host-side write correct? | Runs the verifier pipeline on `write` and `edit`, with `verify` as `block`, `warn` or `off`. | Does not verify arbitrary files a `bash` command writes. |
| File-change bus | What did this call change? | Publishes `write`/`edit` changes and the host post-call scan of `bash` changes, including a container Bash write through the mount. | Does not see files that exist only inside the image. |
| Container boundary | What can the command reach? | Mounts only the workspace (plus the image and runtime), so the state root, install prefix and credentials are absent. | Does not replace the approval panel or the guard, and is not a jail. |

The layers compose: the approval panel supplies intent, the guard protects host state, verified
edits and the bus keep host-side facts, and the container limits reach. When a container environment
is active, the bash guard refuses a state-root write on the host before the engine starts; it does
not constrain the inner `exec` command. That is the honest boundary this page records, not a claim
that the container is guarded by BashGuard.

## Secrets

API keys come from the environment (`OPENAI_API_KEY`, `ANTHROPIC_API_KEY`) or from
`~/.lunate/auth.json`, which Lunate writes owner-only (`rw-------`) on POSIX. Extension secrets live
in `~/.lunate/extensions-secrets/<extension-id>.json`, are never written to session files, and are
never printed by the `/extensions` view; errors name the missing key, never its value. Provider keys
are read by Lunate and sent only to the provider you configured. Configured stdio MCP servers run as
children that inherit your environment — existing MCP setups keep working — except for the values
above: the credentials Lunate itself knows (provider keys, `auth.json` keys, extension secrets and
the configured `env` values) are removed from that child's environment unless the server's own
`env` map names the variable explicitly. The trust prompt for a project server states the same.

## What is out of scope

- Anything that requires an attacker to already run code as you on the machine.
- A malicious model, provider, extension or MCP server you chose to trust — the trust prompt is the
  decision point, and the record of it is yours to clear.
- `bash` doing what you approved it to do, and time-of-check/time-of-use races inside the workspace
  boundary.
- An attacker who already controls the container engine or its socket, a workspace hardlink into the
  state root, or the image you chose to run.
- Exhausting the model's own step and token budgets — those are bounded, reported and yours to
  change, not a vulnerability.

## Related

- [Security policy](../SECURITY.md) — how to report a vulnerability
- [Run tool calls in a container](execution-environments.md) — the container flags, the flow and the
  measured limits
- [Use the terminal](usage.md) — approvals, trust prompts and the modes
- [Settings reference](settings.md) — `approval`, `bashGuard` and every settings key
- [Lifecycle and trust](extensions/lifecycle-and-trust.md) — the trust decision for repository code
