# S-7 report — execution environments

- Date: 2026-11-08
- Card: [#625](https://github.com/lunate-agent/lunate/issues/625), spike only — no product change,
  no OpenSpec delta, no ADR (the decision it needs is a scope decision on three cards, not a new
  architecture rule; see [Why no ADR](#why-no-adr))
- Question: can Lunate's tools run inside a Docker/Podman `exec` with the workspace bind-mounted,
  what happens to the surrounding machinery (approvals, bash guard, verified edits, file-change
  bus), which platforms are feasible, and what does it cost?
- Method: the real `WriteTool`, `ReadTool` and `BashTool` driven from `S7.Harness` against a
  long-lived Podman container with the workspace bind-mounted **at the same absolute path**
  (`-v $WT:$WT -w $WT`); the bash tool through a host shim that `exec`s `podman exec`. One run on
  macOS 26.3.1 arm64, Podman 6.1.0, Apple-VM `podman machine` (4 vCPU, 4 GiB), image
  `mcr.microsoft.com/dotnet/sdk:10.0` (Ubuntu 24.04, aarch64). Raw output:
  [`evidence/`](evidence/).
- Outcome: **the file tools stay on the host; only the process-spawning tools move, and the
  abstraction #626 introduces must carry a working-directory mapping, an environment pass-through
  and a cancel hook — because the host-side process-tree kill provably does not stop the container's
  processes.** Per-call cost is **~126 ms** on macOS (one earlier run measured 97 ms; treat it as 100–130 ms), the container is cheap to create (146 ms) and
  expensive to stop/start (10.3 s).

## Findings

### Q1. Do file tools run on the host or in the container?

**On the host — no change to `read`, `write`, `edit`, `ls`, `glob` or `grep`, and none of them enter
the environment abstraction.**

The bind mount is the whole answer, and it only works because the mount is at the *same absolute
path* on both sides:

- The `write` tool wrote `.s7-evidence/host-write.txt` on the host; the container's `sha256sum` of
  the same path returned the identical digest
  (`1ae36b24…4237fc2`), `stat` saw `mode=644 size=52` and `cat` printed the non-ASCII line intact —
  the mount carries bytes, not a locale.
- The container wrote `.s7-evidence/container-write.txt`; the host's `read` tool read it back with
  no delay and no refresh step (3 ms).
- The path is one path: `/workspace/<file>` — the spelling a container-first design would invent —
  is **refused** by the workspace boundary on the host, while the host absolute path resolves. So
  the abstraction must not invent a container-side mount point; it must mount the workspace where
  the host has it. That removes a path-translation table from #626 entirely on macOS and Linux.

Limits this buys: a file tool cannot see anything that exists only inside the image (`/etc`,
`/usr/include`, the container's own toolchain). That is the same behaviour as today (the boundary
already limits file tools to the workspace), so it is a documented limitation rather than a
regression.

### Q2. Approvals, bash guard, verified edits, the file-change bus

- **Approvals** are untouched: the decision happens in the loop before any spawn
  (`src/Lunate.Coding/BashTool.cs:148` is the guard's refusal, `:156` the spawn). The only #627 work
  is presentation — the header and `/context` naming the environment — plus the approval text
  staying the command, not the `podman exec` argv.
- **Bash guard** keeps working and keeps being the host-side wrapper: with the container
  environment active, a command that would write under the state root was refused before it reached
  `podman exec` ("refused by the bash guard (macOS sandbox-exec): …"). Its *file* guarantee also
  becomes structural, which is the part that matters: the state root is not in the container at all
  (`test -e <state root>` inside → absent). So #627 does not need a guard mechanism inside the
  image — the image had neither `bwrap` nor `sandbox-exec` — it needs the state root, the install
  prefix and the credentials to stay unmounted, and the guard to keep wrapping the host half.
- **Verified edits** are host-side and stay that way, which is exactly why they keep meaning
  something: the file the verifier opens is the file the container reads.
- **File-change bus**: the `bash` post-call scan is host-side `git status` over the mounted
  workspace (`src/Lunate.Coding/BashChangeTracker.cs:231`) and it published a file the container's
  bash created (`bash:Created:s7-scan-probe.txt` from the real sink). It needs no container
  awareness.
  - Two wrinkles found, both worth fixing:
    1. `LUNATE_SESSION` does not reach the container (`session=unset` inside): `podman exec` does not
       forward the host environment, and `BashTool.SessionEnvironment` is set on the spawned process
       only. #627 must pass `-e LUNATE_SESSION=1` explicitly, or the marker's meaning silently
       disappears under `--env`.
    2. A file created inside an *already-untracked directory* is invisible to the bash change scan:
       `git status -unormal` collapses the directory to one entry, so `.s7-evidence/container-bash.txt`
       was published as nothing while `s7-scan-probe.txt` at the root was published. This is
       container-independent host-side behaviour (pre-existing), and it is filed as a bug rather than
       fixed here.

### Q3. macOS / Linux / Windows feasibility

- **macOS: works, through a VM.** Podman runs an Apple-VM machine (4 vCPU / 4 GiB here); the
  worktree is visible inside because the machine shares it. Two consequences for the product: the
  machine must already be running, and the failure when it is not has to read like a message a model
  can act on ("start it with `podman machine start`"). Container files stay owned by the host user
  (`uid_map` maps container `0` to the host uid on this setup), so the host-side file tools and
  `git` keep working on what the container writes.
- **Linux: works natively and is the cheap case** — the same argv, no VM, and the rootless user
  namespace is what keeps host-owned workspace files writable from inside.
- **Windows: not measurable on this host, and structurally different.** A Windows container cannot
  expose `C:\…` at the same absolute path, so the "one path on both sides" property is
  macOS/Linux only. The supported route is the WSL2 backend with a Linux container and a translated
  mount (`/mnt/c/...`); that needs the translation the same-path mount avoided. The honest scope is
  therefore: Linux (CI-tested) and macOS first, Windows-WSL2 second, and #628 must say so instead of
  promising three platforms.

### Q4. Startup cost (15 samples per row unless noted)

| Row | min | median | p95 | max |
| --- | --- | --- | --- | --- |
| `bash` tool, local shell (includes the post-call change scan) | 93.4 ms | 163.5 ms | 211.7 ms | 218.3 ms |
| `bash` tool, container through the shim (same code path) | 191.3 ms | 289.8 ms | 378.5 ms | 739.4 ms |
| `podman exec` client + `/bin/bash -c true` | 105.2 ms | 134.8 ms | 146.7 ms | 150.5 ms |
| `podman exec` client + `true` (no shell) | 128.9 ms | 138.0 ms | 152.5 ms | 155.1 ms |
| cold container: `run -d` create + start + remove | 136.3 ms | 146.2 ms | — | 164.1 ms |
| `stop` + `start` of an existing container (n=3) | 10.26 s | 10.28 s | — | 10.34 s |

- **A bash call costs ~126 ms more** in the container (median 163.5 → 289.8, 1.8×; the earlier run
  measured +97 ms and 1.6×, so the honest budget is 100–130 ms). The `exec` client alone is
  ~135 ms, so the overhead is the CLI round trip, not the command.
- **Create is cheap, restart is not.** Create+start is 146 ms, but stopping and starting an existing
  container is **10.3 s** — so the container's lifetime is the session's, never the tool call's.
- File tools are unaffected: `write` 31 ms, `read` 5 ms, host-side.
- Not measured: image pull (the image was local), `podman machine` cold start, the Docker engine,
  a Linux host, and Windows. All four are named as gaps in the cards rather than assumed.

## What each follow-on card now carries

### [#626](https://github.com/lunate-agent/lunate/issues/626) — environment abstraction refactor

Unchanged in shape, but the spike fixes what the interface has to expose and one thing it must not
take on:

- **In:** one seam for "where tool commands run", still local-only in this card, used by `bash` and
  by every other tool that spawns a process. That is `BashTool`'s `ProcessRunRequest`
  (`src/Lunate.Coding/BashTool.cs:156`), the MCP/extension process sessions and the Roslyn/restore
  path — the interface has to sit under the same `ProcessRunner` seam those already share, not only
  under `BashTool`.
- **The mapping is `(executable, argument prefix, working directory, environment)`** — the same four
  fields the shim proved sufficient. No path translation table: the mount is the mapping.
- **A cancel hook belongs in the interface, not in #627.** The measured fact is that the existing
  tree kill stops the host-side client and leaves the container's process group alive
  (`33 sleep 4271` still running after the tool reported "timed out after 2s; process tree killed"),
  and that killing the reported pgid afterwards works. A local implementation keeps process-tree
  semantics; a container implementation kills the pgid. If #626's interface hard-codes "kill the
  tree of the process I spawned", #627 cannot satisfy its own cancel criterion.
- **Out:** file tools. They never spawn a process and the bind mount makes them correct untouched;
  the card's "every tool that spawns processes" is the right boundary and should say why file tools
  are not in it.
- Also needed for #627's header/`/context` line: the seam must be able to describe itself in one
  short string ("local" or the environment's name). That is a property, not a new flag.

### [#627](https://github.com/lunate-agent/lunate/issues/627) — docker/podman tool runner

The acceptance criteria stay; the spike makes four of them concrete and adds one:

- **`--env docker:<image>`** gains a sibling: `--env podman:<image>`. Both engines are shipped and
  the only difference is `argv[0]`; a card that names only Docker is a card that will not run on the
  maintainer's own Mac. The settings key selects the same thing.
- **The mount is the host absolute path**, not `/workspace`: `-v <worktree>:<worktree> -w <worktree>`.
  This is what makes file tools, verified edits and the change scan work without touching them.
- **Cancel kills the container's process group**, and the pgid has to be captured (the command
  wrapper prints it as the first line, or the runner records it) because the host-side `podman exec`
  client's death stops nothing: measured, the survivor list after the tree kill was
  `33 sleep 4271`, and `kill -TERM -- -33` inside the container then cleaned it up.
- **One container per session**, created before the first tool call (~146 ms) and kept — the 10.3 s
  stop/start is the reason the criterion is "long-lived", and it is also what lets the header name
  the environment before the first tool call.
- **The session marker is forwarded** (`-e LUNATE_SESSION=1`); the guard keeps wrapping the host half
  and the state root, the install prefix and `auth.json` are simply not mounted. No guard mechanism
  is expected inside the image.
- **The file-change scan and verified edits are untouched** — and the card should say so, so nobody
  re-implements them on the container side.
- Windows stays out of this card: E2E on Linux CI is unchanged, and the WSL2 path with a translated
  mount is named as the follow-up rather than half-built here.

### [#628](https://github.com/lunate-agent/lunate/issues/628) — execution environments: docs

Same three criteria, plus the facts the page has to carry (all measured above): file tools stay
host-side because the mount is the workspace; the state root, keys and prefix are not mounted; one
bash call costs ~+100 ms on macOS and the container must already be running (10 s to restart);
cancel semantics; and the platform table — Linux and macOS with the same mount path, Windows only
through WSL2 with a translated path. The security page's comparison gains a fourth line: what each
layer is for (approval panel: intent; bash guard: host state; container: what the command can even
reach), and that they compose.

## Why no ADR

The spike's decision is a scope decision on three existing cards — where the seam sits, what it maps,
what cancels how, which platform is first — not a new architecture rule, and it is fully reversible
once #626 lands. If #627 changes the shape further (for example a per-call container instead of a
per-session one), that is the moment for an ADR, and it should be proposed on that card's proposal
step, not recorded speculatively here.

## What is not evidence

- One machine (macOS arm64 + Podman 6.1.0 + Apple VM), one engine. Not Docker, not Linux, not
  Windows, not a CI runner.
- The bash-tool rows include the tool's own post-call change scan, which is why the local median is
  164 ms and not ~5 ms; both sides pay it, so the delta is the honest number.
- 15 samples per row (3 for stop/start, 5 for cold create) — enough to show the order of magnitude,
  not enough for a budget. If #627 wants a startup budget, it should measure on CI, where the
  numbers will be different and CI is where they matter.
