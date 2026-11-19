# Contributing

Thanks for looking. Lunate is a small, MIT-licensed project and it is built change by change with
coding agents, from the specification up — the rules in [`AGENTS.md`](AGENTS.md) are the ones this
repository actually runs on, and this page is the path from a fresh clone to a merged change.

## Prerequisites

- [.NET 11 SDK](https://dotnet.microsoft.com/download) — **exactly** the version pinned in
  `global.json` (`11.0.100`, `rollForward: disable`, `allowPrerelease: false`). Install it with
  `dotnet-install.sh --version 11.0.100` (`dotnet-install.ps1 -Version 11.0.100` on Windows).
  A newer patch is not picked up automatically: Dependabot opens the bump as a PR against
  `global.json`, and that PR runs the full gate — together with any gate fix the newer SDK needs,
  because an SDK bundles targeting packs only for its own framework.
- [Node.js](https://nodejs.org/) — runs the documentation lint through a pinned `npx`
- [hyperfine](https://github.com/sharkdp/hyperfine) — measures the startup budget
- `jq` — reads the performance and memory samples (`scripts/perf.sh`, `scripts/memory.sh`)
- [GitHub CLI](https://cli.github.com/) — only for the release runbook in [`docs/dev/distribution.md`](docs/dev/distribution.md)

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

Prerequisite: the [.NET 11 SDK](https://dotnet.microsoft.com/download), pinned in
[`global.json`](global.json) at 11.0.100 — exactly, as above. Everything targets `net11.0`; nothing
in the repository multi-targets ([ADR-0041](adr/0041-net11-target-frameworks.md)). Running the built
binary on a machine with no tool installed yet:

```bash
src/Lunate.Coding/bin/Release/net11.0/lunate --version   # lunate.exe on Windows
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
PR's changed paths, deny-by-default: only `docs/`, `adr/`, `openspec/`, root-level `*.md` and
`changes/*.md` qualify, and a change to the workflows, a rename out of those trees or anything else
means the full gate. The default `--profile full` is the whole gate above, unchanged — and its
platform checks run `scripts/no-docs-references.sh` too, so a test that starts reading `docs/` is red
on the pull request that adds it, not only on a prose one.

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
`tests/Lunate.Coding.Tests/Process/WaitDiagnosis.cs` and `ProcessTable.cs`.

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
| `docs/` | the user pages; `docs/dev/` holds the documentation map, the guide, the architecture, the release runbook, the contracts, spikes and eval evidence |
| `templates/`, `samples/` | the extension template and the reference extensions (the fitness suite) |
| `extensions/` | the first-party supported extension tier, shipped as `Lunate.Extensions.*` packages |

A source file is named after a type it declares: the text before the first dot of its base name is a
type the file declares (a partial-class file such as `AgentHarness.Retries.cs`), or it names no type
at all (an aggregate file such as `HookPayloads.cs`). A file named after a type another file owns
resolves to the wrong file — `tests/Lunate.Agent.Tests/Loop/SourceFileNameTests.cs` enforces the rule and
names both files when it fails. The requirement lives in the `repo-quality` spec.

A folder groups files by the feature they serve — `Sessions/`, `Tools/`, `Mcp/Client/` — and a
folder never changes a file's namespace: every file in a project keeps the namespace its assembly
established, so `src/Lunate.Coding/Tools/ReadTool.cs` still declares `namespace Lunate.Coding;`.
A namespace records compatibility, not location: `src/Lunate.Contracts` deliberately declares
`Lunate.Agent` and `Lunate.Ai` types beside its own, so its `TypeForwardedTo` entries keep a
compiled consumer binding after the assemblies were split. Tests mirror `src` in both directions: a
feature folder has a folder of the same relative path under its test project, no test folder exists
without one, and a test file sits in the folder of the feature it exercises. Support files (a
`TempDirectory`, a scripted client, a collection definition) stay at the test project root.
`tests/Lunate.Coding.Tests/LayoutRuleTests.cs` enforces all of it, and a folder that tries to become a
namespace fails it by name.

## Documentation

Start at [`docs/dev/README.md`](docs/dev/README.md) — the map of every document kind and the writing
guideline. Key entry points:

- [Guide](docs/dev/guide.md) — what Lunate is, the plan and the roadmap
- [Architecture](docs/dev/architecture.md) — the component map and the runtime flows
- [Specs](openspec/specs/) — the behaviour source of truth
- [ADRs](adr/) — decisions with their context
- [Releases](https://github.com/lunate-agent/lunate/releases)

One home per fact: link to the document that owns a fact instead of copying it, and update the
documents a change invalidates.

## The OpenSpec workflow

Work happens through OpenSpec changes, and the artifacts are the source of truth:

1. **Propose** — describe the change: the proposal, the delta specs, the design and the tasks land on
   the change's branch, inside its one implementation pull request. `grill-me` stress-tests the plan
   first; an unattended run answers the interview itself and lists the answers under **Intent
   decisions** in the pull request, for the reviewer.
2. **Apply** — implement the tasks on that same branch from `main`, test-first, ticking tasks as they
   land. There is no separate proposal pull request and no wait for the proposal to reach `main`.
3. **Verify** — check the implementation against the change's artifacts before archiving
   (`openspec validate <change> --type change --strict`).
4. **Archive** — sync the delta specs into `openspec/specs/` and archive the change as the *final
   commit of the implementation pull request*, added before review, so the reviewer reads a tree in
   which the change is already archived and its specs already synced. A rework that a review requests
   redoes the archive as the last commit again.

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
touches source, tests, templates or workflows runs the fast gate: `plan`, the changed-path decision,
the changelog guard, the platform checks — which run that guard too, so a test that reads a docs file
cannot land on a code pull request — and build plus tests on `ubuntu-latest` only. The decision
lives in `scripts/ci-plan.sh` and is deny-by-default, so a path nobody thought about runs the full
gate rather than the cheap one. The single required `ci-ok` check judges the live head's newest
attempt of each required job, not the run's trigger payload: rerunning an older or draft run reports
the current verdict, and draft and ready runs have separate concurrency groups so a draft push cannot
cancel the ready gate. A gate that never carried a Ubuntu leg the plan put in its verify matrix is red
too, naming the missing leg, rather than green on the shards that did run.

The merge queue is the same diff one more time, so it runs the Ubuntu test gate and the cheap tree
checks, and skips what the pull request already proved: the changed-path decision, the changelog
guard, the platform checks, the docs profile and the Windows PowerShell twin. The tree checks belong
to the queue because the batch is the first tree on which two pull requests meet: `#840` took a free
ADR-0036 and `#843`'s full-profile gate never asked about ADR numbers, so their duplicate reached
`main` while both pull requests were green. `ci-ok` requires the `tree-checks` job, so a batch that
carries a duplicate is ejected instead of merged. The full matrix — Ubuntu, Windows and macOS with
every shard, the PowerShell twin and the dedicated perf measurement — runs on a push to `main` and the
nightly; CodeQL runs on a push to `main` and its weekly schedule, not on a pull request. A red push
to `main` and a red nightly each file one `kind/bug` card per failing job, naming the job and the run
link, and comment on the open card for that job instead of filing a duplicate. A job that never started
is not a failing job: GitHub concludes a job it never acquired a hosted runner for as `cancelled` with
`runner_id: 0` and an empty step list, and because no change in this repository can fix that — the remedy
is a rerun — the reporter files no card for it and names it, together with the run link, in the run's own
log and job summary instead, also when the run's other red job that ran is filed as its own card, so a
hosted-runner shortage no longer produces an unactionable card (#922). The rule fails closed: a job
cancelled after reaching a step, and a document whose step list or runner id is missing, still files. The
card takes the lowest open version milestone (`0.5.0` rather than `backlog`; a release closes its
milestone, so the next version becomes the target), read from the GitHub API, and an `area/tui` or
`area/core` label only when the failing test's project decides it: a `Lunate.Tui` test is `area/tui`, a
`Lunate.Coding` test is `area/tui` when its own source names `Lunate.Tui` and `area/core` otherwise, and a
job that fails without a failing test in those projects carries no area label. A card that names the job
still needs the step inside it, and a job's log on a public run needs an account while its check run's
annotations do not: `scripts/verify.sh` reports the step it stopped in, with its exit status and
duration, as an `::error::` annotation and in the job summary, so a red leg can be triaged from the run
instead of from a log nobody without a session can open (#942). A test step that failed also names the
tests it failed on, up to five of them, in the same annotations and summary, because naming only the
step still leaves the red to the log: `verify (macos-latest, 2/2)` of run 44189133429 could be located
and not attributed (#948). A pass that failed without naming a test at all — an aborted session, a
crashed host, a module that never started — names the modules that failed, with their passed, failed and
aborted counters, and the verdict the run ended with, for the same reason: the de-AT pass of that leg in
run 44466042157 failed twice and its annotation said only that the step had failed (#951). The `perf`
job's startup step reports the measurement it failed on the same way: its floor, median and budget,
plus the first attempt's floor when the noise policy retried, because the shared macOS runner's
300-330 ms floor and a doubled startup look alike from the run alone (#950).

A card can arrive stale. The reporter files one card per failing job, and one merge reddens several
legs at once: `verify (windows-latest, 1/4)`, `(2/4)` and `verify-powershell (windows)` all restore
the extension template against the feed the step has just packed, so a single fix lands for all three
while each leg keeps a card of its own. The card for a leg whose fix landed while its run was still
in flight is a duplicate from the start. Triage therefore begins at the head of `main`: when the most
recent push run of the same job is green and a sibling card from the same run was closed by the merge
that fixed it, no change is left to make here — close the card as a duplicate and name the sibling,
rather than open a pull request against a green tree (#944).

A run whose legs never acquired a hosted runner is a runner-capacity red, and it is retried instead of
reported: GitHub cancels a job when no macOS arm64 runner is free, before its first step starts, so nothing
the repository builds or tests ran. The reporter writes the abandoned legs into the run summary and fails,
`ci-capacity-retry.yml` reruns that run's failed jobs once when it completes, and the attempt the retry
budget does not cover says in the same summary that the retry is spent and that a rerun is the remedy. No
card is ever filed for such a leg, and the summary names it even when another red job that ran is filed as
its own card. The derived `ci-ok` aggregate and the reporter itself are never named
as abandoned legs, because they failed only because a leg did not succeed. A red job that ran is never
retried that way, so an executed failure is never retried away, and `ci-ok` still fails closed on an
abandoned leg in every attempt (#923).

Once a pull request lands, the same cheap checks that need no build run again on the push to `main`:
`tree-checks.yml` runs ADR numbering, the spec-archive rule, the generated docs regions, the
link/navigation check, the "no test reads `docs/`" guard and the docs lint in one job of a few
seconds. Both triggers call
`scripts/tree-checks.sh`, so the push to `main` and the queue run one command list. A docs-only push
skips the matrix, so without that job a check that fails on `main` stays invisible until the first
pull request that touches documentation — which is how `#738` (`check-adr-numbers.py` failing on
`main`) survived. A red `tree-checks` on a `main` commit means one of those rules broke on the merge,
not that the merge was fine.

Pull requests use the [pull request template](.github/pull_request_template.md): what the change is,
what lands, where to look first, and the verification you ran. Keep commits small and conventional
(`feat`, `fix`, `docs`, `test`, `chore`). Public API changes belong in
`PublicAPI.Unshipped.txt`, no new NuGet package arrives without an ADR, and a failing golden session
test means the session format changed: stop and report instead of editing the golden file.

Every pull request that has a release-note line writes it in `changes/<pr-number>.md` and never edits
`CHANGELOG.md`: put the line under `### Added`, `### Changed` or `### Fixed`, as a bullet ending with
one or more references, `(#<pr-number>)` or `(#681, #682)`. A fragment is prose, so such a pull request
gets the docs profile. The release
pull request — branch `release/<version>` or a title starting `chore(release)` — is the only one
that edits the changelog: it runs `scripts/changelog-assemble --write`, commits the result and
deletes the consumed fragments. `ci-ok` fails any other pull request that edits `CHANGELOG.md` and
names the fragment path to use.

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
