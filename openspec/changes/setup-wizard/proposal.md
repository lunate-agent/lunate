# Setup wizard: the first run configures a provider, a profile and a decision layer, once

## Why

A new user meets a clunky sequence with no way back: they learn `lunate auth login` from the
refusal text, then translate preset names and credential references into `providers.json` and
profile/usage keys into `settings.json` by hand, choosing between /model and a bare id (`model`
default vs. `usages.chat`), with no connection test and no single place that shows what will be
written (`docs/first-run.md`, IS.AuthPicker first-run offer design D4). The only first-run
assistance today is the auth picker, which configures a **credential** and nothing else: no model
step, no profile step, no connection test before committing

## What Changes

- When no provider or no chat profile is configured, an interactive `lunate` run opens a setup
  wizard instead of the tidy-sequential first-run refusal; `lunate setup` and `/setup` re-run it
  at any time; the wizard inherits the /auth picker rules (no prompting while busy; a terminal is required).
- Steps ([DETAILED IN DESIGN]): provider (presets + `custom` family selection per #474), credential
  (masked entry, masked completion, reviewable, optional `environment:` reference) never written to
  project settings / stored, connection test (real discovery `GET /models` call, failures are
  reportable and step-recoverable, not fatal), model (from the discovery listing or a manual id —
  discovery surfaced by the provider metadata contract in #474, model id entry with search/discovery
  unavailable fallback), chat profile (name + reasoning level per #476's profiled thinking),
  optional fast/decision profiles, decisions-all-off defaults (one explanatory line per option),
  and a summary step offering every collected value for final review/confirmation before writes; all
  values staged until the summary confirm; explicit abort/skip leaves the stores byte-identical.
- One writer: the wizard submits edits through the same stores/settings writers `/settings` and
  `auth login` consume (the `SettingsStore`/`AuthStore` and provider-store write paths), never a
  parallel writer.

### New Capabilities

- `setup-wizard`: the wizard flow itself — trigger conditions, the nine steps, test step, non-interactive
  equivalent, atomic no-write-until-confirmed and skip semantics.

### Modified Capabilities

- `tui`: `/setup` command, wizard interaction within the live-area picker/prompt flow, narrow-width
  (60/80 col) rendering rules, welcome-header fix text / first-run offer pointing to the wizard.
- `agent-config`: discovery of config state that triggers the wizard on the next interactive run, and
  clarifies the wizard + `/settings` + `setup` writers as the same store writes.

## Impact

- Affected code: `InteractiveSession` (first-run trigger; `/setup`; welcome-header fix routing),
  `AuthCommand` (custom writers reused), new `SetupWizard` implementation in `Lunate.Coding`/`Lunate.Tui`
  (wizard state/steps, no model calls — the connection test uses the same discovery client as
  `--discover`), `SettingsStore` (profile/usage writers), and fixture-driven wizard-session tests.
- No new NuGet packages. No alternative ADR: the profile schema is already approved (thinking-min
  per profile #476), and the provider/families model is shipped (#474/ADR-0029); the wizard
  is a consumer, not a new contract.
- Depends on the shipped #474 entry v2 (API families, placements, discovery metadata) and #476 (profile-level thinking); the wizard
  surfaces both but owns no schema of its own.
