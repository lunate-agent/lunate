# Contributing

Thanks for looking. Lunate is a small, MIT-licensed project and it is built change by change with
coding agents, from the specification up — the rules in [`AGENTS.md`](AGENTS.md) are the ones this
repository actually runs on, and this page is the path from a fresh clone to a merged change.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) — **exactly** the version pinned in
  `global.json` (`10.0.402`, `rollForward: disable`, `allowPrerelease: false`). Install it with
  `dotnet-install.sh --version 10.0.402` (`dotnet-install.ps1 -Version 10.0.402` on Windows).
  A newer patch is not picked up automatically: Dependabot opens the bump as a PR against
  `global.json`, and that PR runs the full gate.
- [Node.js](https://nodejs.org/) — runs the documentation lint through a pinned `npx`
- [hyperfine](https://github.com/sharkdp/hyperfine) — measures the startup budget
- `jq` — reads the performance and memory samples (`scripts/perf.sh`, `scripts/memory.sh`)
- [GitHub CLI](https://cli.github.com/) — only for the release runbook in [`docs/distribution.md`](docs/distribution.md)

## Quick start

```bash
git clone https://github.com/lunate-agent/lunate
cd lunate
dotnet tool restore                       # CSharpier, pinned as a local tool
dotnet build lunate.slnx -c Release
dotnet test --solution lunate.slnx -c Release
bash scripts/verify.sh                    # the full gate (Windows: scripts/verify.ps1)
```

`scripts/verify.sh` is the done-gate: it restores the tools, builds, runs every test (including a
`de-AT` culture pass and the performance-trait tests), publishes the single-file binary for this
platform, measures the startup budget, exercises the install scripts against a fixture release,
packs and reinstalls the `dotnet tool`, checks the packaging files and both shipped packages'
metadata (licence, readme, repository) inside the `.nupkg` itself, formats (CSharpier plus
`dotnet format style` / `dotnet format analyzers`), lints the Markdown and diffs the public API. A
change is not done until it is green — on the CI matrix, all three operating systems count.

Prerequisite: the [.NET 10 SDK](https://dotnet.microsoft.com/download), pinned in
[`global.json`](global.json) at 10.0.402 — exactly, as above. Running the built binary on a
machine with no tool installed yet:

```bash
src/Lunate.Coding/bin/Release/net10.0/lunate --version   # lunate.exe on Windows
```

The release archives are self-contained single files; publish one the same way (Windows: `win-x64`,
and the binary is `lunate.exe`):

```bash
dotnet publish src/Lunate.Coding/Lunate.Coding.csproj -c Release -r osx-arm64 \
  --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true \
  -o artifacts/publish/osx-arm64
artifacts/publish/osx-arm64/lunate
```

To install your built tool locally instead of publishing it, pack it and install from that feed:
`dotnet pack src/Lunate.Coding -c Release`, then `dotnet tool install --global --add-source
<path-to-the-nupkg> lunate`.

A docs-only or proposal-only pull request runs `bash scripts/verify.sh --profile docs` instead:
markdownlint, `openspec validate --all --strict`, `scripts/docs-sync --check`,
`scripts/docs-check --check` and `scripts/no-docs-references.sh` (which fails if anything under
`docs/` becomes a build input or a test's subject). `scripts/ci-plan.sh` picks the profile from the
PR's changed paths, deny-by-default: only `docs/`, `adr/`, `openspec/` and root-level `*.md` qualify,
and a change to the workflows, a rename out of those trees or anything else means the full gate. The
default `--profile full` is the whole gate above, unchanged.

Useful narrower commands while working:

```bash
dotnet test --project tests/Lunate.Coding.Tests/Lunate.Coding.Tests.csproj -c Release
dotnet csharpier format .                # never hand-format C#
npx --yes markdownlint-cli2@0.23.3       # documentation lint only
python3 scripts/docs-check --check       # links, anchors, redirects and the navigation manifest
bash scripts/verify.sh --profile docs    # the prose gate: lint, OpenSpec, generated regions
bash scripts/perf.sh artifacts/publish/<rid>/lunate   # startup budget only
bash scripts/gate-tests.sh               # the gate's own self-tests (needs a published binary)
```

When an interactive-session test fails on a wait, read the message before rerunning: it names the
condition that was polled, how many times it was observed and the longest gap between observations,
the thread pool's state, the test process's process tree (with `/proc` descriptors on Linux) and the
session state the condition reads. Many polls with no long gap is a hang; few polls with long gaps is
a starved runner. Collecting that diagnosis is bounded by `WaitBudgets.Diagnosis` and lives in
`tests/Lunate.Coding.Tests/WaitDiagnosis.cs` and `ProcessTable.cs`.

## Repository map

| Path | What lives there |
| --- | --- |
| `src/Lunate.Ai/` | `IChatClient` setup, model catalog, record and replay clients |
| `src/Lunate.Agent/` | the loop on `IChatClient`, events, tools, sessions, compaction |
| `src/Lunate.Extensibility/` | the extension host: discovery, loading, hooks, trust |
| `src/Lunate.Extensibility.Abstractions/` | the semver'd contract extensions compile against |
| `src/Lunate.Extensibility.Remote/` | the out-of-process extension host and its wire contracts |
| `src/Lunate.Extensibility.Testing/` | the test kit that loads a built extension through the real loader |
| `src/Lunate.Protocols/` | MCP client and ACP server |
| `src/Lunate.Tui/` | Spectre output, input line and live area |
| `src/Lunate.Roslyn/` | built-in semantic C# extension, loaded on first use |
| `src/Lunate.Coding/` | the `lunate` executable: TUI, print and ACP modes |
| `tests/` | one test project per src project, plus fixtures |
| `openspec/specs/` | current behaviour, the source of truth |
| `openspec/changes/` | in-flight proposals and tasks |
| `adr/` | durable architectural decisions |
| `docs/` | guide, spikes, eval write-ups and the documentation map |
| `templates/`, `samples/` | the extension template and the reference extensions (the fitness suite) |
| `extensions/` | the first-party supported extension tier, shipped as `Lunate.Extensions.*` packages |

A source file is named after a type it declares: the text before the first dot of its base name is a
type the file declares (a partial-class file such as `AgentHarness.Retries.cs`), or it names no type
at all (an aggregate file such as `HookPayloads.cs`). A file named after a type another file owns
resolves to the wrong file — `tests/Lunate.Agent.Tests/SourceFileNameTests.cs` enforces the rule and
names both files when it fails. The requirement lives in the `repo-quality` spec.

## Documentation

Start at [`docs/README.md`](docs/README.md) — the map of every document kind and the writing
guideline. Key entry points:

- [Guide](docs/guide.md) — what Lunate is, the plan and the roadmap
- [Architecture](docs/architecture.md) — the component map and the runtime flows
- [Specs](openspec/specs/) — the behaviour source of truth
- [ADRs](adr/) — decisions with their context
- [Releases](https://github.com/lunate-agent/lunate/releases)

One home per fact: link to the document that owns a fact instead of copying it, and update the
documents a change invalidates.

## The OpenSpec workflow

Work happens through OpenSpec changes, and the artifacts are the source of truth:

1. **Propose** — describe the change; the proposal, the delta specs and the tasks land on `main`
   before any implementation.
2. **Apply** — implement the tasks on a branch from `main`, test-first, ticking tasks as they land.
3. **Verify** — check the implementation against the change's artifacts before archiving
   (`openspec validate <change> --type change --strict`).
4. **Archive** — sync the delta specs into `openspec/specs/` and archive the change as the *final
   commit of the implementation pull request*, added after review and before merge, so one reviewer
   sees the work and the bookkeeping that closes it.

A pull request that only archives a change is refused by the `archive-only` check. A standalone
archive is still legitimate when a change merged before its archive existed: label the pull request
`standalone-archive` and say why in the body.

One change per session. Do only what the change's `tasks.md` asks; anything else becomes a follow-up
change. `openspec/specs/` is the source of truth for behaviour — if code and spec disagree, stop and
fix the spec first. Architectural decisions (the loop, tool contracts, protocols, dependencies)
require maintainer sign-off: propose and stop.

### Skills

The OpenSpec lifecycle skills and the companion skills live together in `.agents/skills/` — one
source, read by every harness (OpenCode, Pi, Lunate itself). There is no `.opencode/skills` copy; the
`.opencode/` directory holds only the OpenCode subagents.

In an OpenCode session the OpenSpec workflows are invoked as **skills** (`openspec-propose`,
`openspec-continue-change`, `openspec-apply-change`, `openspec-verify-change`, `openspec-sync-specs`,
`openspec-archive-change`, …), discovered from `.agents/skills/`. The `/opsx:*` slash commands are
gone: they were generated into `.opencode/commands/`, which this change removes so OpenSpec stops
re-creating an OpenCode copy of the skills.

Regenerate the lifecycle skills after upgrading the OpenSpec CLI:

```bash
bash scripts/check-openspec-skills.sh
```

That script runs `openspec update` and fails when `.agents/skills/` differs from the generated
result, so generated skills never carry a hand edit and never go stale. It runs in the `docs` and
`platform` verify profiles. The workflow set comes from the OpenSpec CLI's *global* profile, which is
per machine (a fresh CI runner defaults to `core`), so this repo pins it in `openspec/cli-config.json`
and the script injects that file through `XDG_CONFIG_HOME`. To change the set, edit
`openspec/cli-config.json`, run the script, and commit the result.

`.agents/skills/.openspec-target` pins the shared root to the universal `agents` renderer: keep it at
`agents`. If it reads `codex`, OpenSpec rendered the Codex variant of the skills instead, which is
not what the other harnesses expect — set it back to `agents` and re-run `openspec update`.

## Pull requests

A pull request that changes only prose — `docs/`, `adr/`, `openspec/`, or a root-level Markdown
file — runs the docs profile: markdownlint, the generated-region check, the link/navigation check,
the "no test reads `docs/`" guard and `openspec validate --all --strict`, in seconds. Anything that
touches source, tests, templates or workflows runs the full matrix. The decision lives in
`scripts/ci-plan.sh` and is deny-by-default, so a path nobody thought about runs the full gate rather
than the cheap one.

Once a pull request lands, the same cheap checks that need no build run again on the push to `main`:
`tree-checks.yml` runs ADR numbering, the spec-archive rule, the generated docs regions, the
link/navigation check and the docs lint in one job of a few seconds. A docs-only push skips the
matrix, so without that job a check that fails on `main` stays invisible until the first pull
request that touches documentation — which is how `#738` (`check-adr-numbers.py` failing on `main`)
survived. A red `tree-checks` on a `main` commit means one of those rules broke on the merge, not
that the merge was fine.

Pull requests use the [pull request template](.github/pull_request_template.md): what the change is,
what lands, where to look first, and the verification you ran. Keep commits small and conventional
(`feat`, `fix`, `docs`, `test`, `chore`). Public API changes belong in
`PublicAPI.Unshipped.txt`, no new NuGet package arrives without an ADR, and a failing golden session
test means the session format changed: stop and report instead of editing the golden file.

## Trust in scripted runs

A repository's project extensions and its project `.lunate/mcp.json` run repository-provided code, so
each needs a one-time trust decision. Records live in `~/.lunate/trust.json` — per user and machine —
so an ephemeral CI runner starts untrusted and runs the trust step in every job:

```bash
lunate trust            # inspect: the records, the pending decisions and one digest
lunate trust --yes      # record the pending decisions
lunate trust --json     # the inspection as one JSON document, for scripts
lunate trust --clear    # remove this repository's records
```

`lunate trust` exits 3 when decisions are pending, so it doubles as a CI check. `--yes` records
exactly what it prints: use it only for checkouts you control, never on untrusted pull requests or
forks. To review before trusting in CI, inspect `lunate trust --json` on the reviewed revision, pin
the digest it prints, and let the job run `lunate trust --yes --digest <sha>`: it records only when
the recomputed digest matches, and a stale digest records nothing and exits 4.

## Security

Do not report vulnerabilities in a public issue — see [`SECURITY.md`](SECURITY.md).

License: MIT — see [`LICENSE`](LICENSE).
