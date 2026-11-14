# Lunate — rules for coding agents

Lunate is a native C# coding agent for the terminal. Background: `docs/dev/guide.md`.
This repo is developed with OpenCode and OpenSpec (schema: `intent-driven`).

- Documentation map and writing guideline: `docs/dev/README.md`. Start there when adding, moving or looking for documentation.

## Workflow (OpenSpec)

- OpenSpec is the only plan format. Plans, ledgers and resume notes for a change live in its `openspec/changes/<name>/` folder (design.md, tasks.md, or notes.md there), never in a skill's or tool's own directory. A session never descopes a maintainer requirement on its own; it files the gap as an issue in the same milestone and says so in the PR.
- Work happens through OpenSpec changes. Never hand-write files under `openspec/changes/` or `openspec/specs/`; use the `openspec` CLI and the lifecycle skills in `.agents/skills/`, in order: `openspec-new-change` → `openspec-propose` → `openspec-continue-change` → `openspec-apply-change` → `openspec-verify-change` → `openspec-sync-specs` → `openspec-archive-change`. They work the same in every coding harness.
- For propose/apply/verify/archive workflows, use the local `openspec-git-discipline` skill. A card is one implementation pull request: proposal, apply and archive land in it together, never in separate pull requests.
- The archive is the last commit, and it lands before review, so the reviewer reads a tree whose change is already archived and its delta specs already synced. A rework that a review requests redoes the archive, or its update, as the last commit again.
- The reviewer reads the proposal delta under `openspec/changes/<change>/` before the implementation, so a mismatch between intent and code is a review finding, not a merge-order accident.
- Nobody answers an unattended run: decide inside the card's scope, commit on your branch and put the question in the pull request for the reviewer rather than stopping to ask. The one exception is an architecture decision (below): propose it and stop for the maintainer's sign-off.
- `openspec/config.yaml` requires `grill-me` for a proposal, and `grill-me` interviews a user. Unattended, run it in self-answer mode: answer each question from the card, the specs and the code, and list every question with the answer you chose under the heading **Intent decisions** in the pull request description. The reviewer checks that list; a missing list sends the pull request back.
- A conflict under `openspec/specs/**` is never resolved by editing a spec by hand: `git revert` the archive commit, merge `origin/main`, then re-run `openspec archive <change> --yes` as the last commit again. A hand-edited spec is indistinguishable from a synced one until someone reads the archive.
- OpenSpec specs are the source of truth for behaviour. If spec and code disagree, stop and report — fix the spec first.
- One change per session. Do only what the change's `tasks.md` asks; anything else becomes a follow-up change.
- src work goes through the `senior-dev` subagent (test-first, `test-driven-development` skill); acceptance-test work through `senior-qa`.
  - In an unattended run no one can observe a subagent: wait at most 20 minutes for its result, then continue the work directly and record in the PR that you did.

## Build

- .NET 10 (pinned in `global.json`), C# 14, nullable, warnings as errors.
- Iterate behind the fast gate: `dotnet csharpier check .`, `openspec validate <change> --strict`, a Release build, and the test classes that cover what you touched.
- `scripts/verify.sh` (Windows: `scripts/verify.ps1`) must pass before a change is marked ready for review or done: build, tests, single-file publish, performance budgets, public API, formatting (CSharpier) and documentation lint checks.
- `ci-ok` must be green before a merge.
- C# code is formatted with CSharpier (`dotnet csharpier format .`); never hand-format. The gate checks it (`dotnet csharpier check .`); analyzer and style rules run via `dotnet format style` / `dotnet format analyzers` (whitespace formatting and IDE0055 are off).

## Architecture

- `Lunate.Ai` <- `Lunate.Agent` <- `Lunate.Protocols` / `Lunate.Coding`. Never reference upward.
- `Lunate.Tui` references no other Lunate project.
- `Lunate.Agent` references `Microsoft.Extensions.AI.Abstractions`, `Lunate.Ai`, `Lunate.Ai.Decisions`, `Lunate.Contracts` and the BCL.
- Model types are Microsoft.Extensions.AI types (`IChatClient`, `ChatMessage`, `AIContent`). Do not add parallel message types. Tools (`ITool`) and events (`AgentEvent`) are ours.
- Never use `FunctionInvokingChatClient` or `AIFunctionFactory`. The loop in `Lunate.Agent` runs tools, because approvals, events, steering, cancel, error messages and compaction all happen between tool calls. See ADR-0003.
- `Lunate.Roslyn`, MCP servers and extensions load on first use, never at startup.

## Rules

- Architecture decisions (loop, tool contracts, protocols, dependencies) require maintainer sign-off: propose and stop, even unattended — this is the one exception to deciding inside the card's scope.
- No new NuGet packages. Ask first.
- Public API changes appear in `PublicAPI.Unshipped.txt`. Change public types only if the change says so; otherwise propose the change and stop.
- A failing golden session test means the session format changed. Stop and report; never update golden files to make a test pass.
  - Exception: a change whose approved proposal states that it changes the session format updates the golden files under `tests/fixtures/sessions/` in the same PR and shows the golden diff in the PR description. Any other golden failure: stop and report.
- In ACP mode nothing writes to stdout except the protocol.
- Tool error messages are read by a model: say what failed and what to do next.
- Format all technical output (numbers, percentages, dates, sizes) with `CultureInfo.InvariantCulture`. The product is English-first (user- and model-facing strings in English) and must behave identically under any machine culture. Tests must not depend on the machine locale.
- Line endings and encoding are part of a file's contract. Files Lunate owns or generates — the install record, locks, trust.json, session files, fixtures, exports, logs, anything hashed or compared across machines — are LF and UTF-8 without BOM on every platform; their bytes must not depend on where they were written. Files the user owns — anything the edit or write tools change, AGENTS.md — keep the line endings and BOM they already have; a new file follows `.editorconfig`, then `.gitattributes`, else the platform default. A hash or digest over user-owned content normalizes line endings to LF before hashing.
- Records are history; instructions are current. A record — an archived change under `openspec/changes/archive/**`, a spike under `docs/dev/spikes/**`, a changelog entry — keeps the paths, names and commands that were true when it was written; never rewrite one to match today. Anything a reader is meant to follow — this file, `docs/**` guides, READMEs — names what exists now.
- Every behaviour change has a test. Tests use fixtures in `tests/fixtures/`, never live APIs.
- Keep files under about 300 lines. Prefer plain code over abstractions.
- Use current C# language features where they simplify the code. Keep sibling code paths (for example provider adapters) structurally symmetric.
- No secrets in git. Commit small, conventional-commit messages.
