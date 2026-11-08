# Design: the setup wizard's manifest and receipt

## Context

The setup wizard already stages every answer in `SetupWizardModel` and commits them in one pass
through `AuthStore`, `ProviderStore.Save` and the `SettingsStore` writers
(`src/Lunate.Coding/SetupWizardCommit.cs`). The summary step is a flat list of values
(`SetupWizardSummary.Render`): a provider's custom fields are one parenthesised line, profiles,
usages and decisions are prefixes (`decisions.approvals: …`), and nothing says which file a value
reaches. The real terminal stack wraps those lines at the scrollback width, so at 40 columns a
single provider line becomes several unrelated-looking rows. The S-8 report
(`docs/spikes/S-8/report.md`) names the gap: the review is a value list, not a file manifest.

The commit's writers are the authority for which file a value reaches:

- a preset entry, or a custom entry with an `env:`/`auth:` reference, goes to `providers.json`
  through `ProviderStore.Save`;
- a typed custom key plus a model goes to the settings `providers.<id>` section through
  `SettingsStore.WriteProvider` (the same shape `auth login --custom` writes), and the key goes to
  `auth.json`;
- profiles, usages and every `decisions` key go to `settings.json`.

The design must keep that mapping in one place: a manifest that drifts from the commit is a review
that lies about the write.

## Goals / Non-Goals

**Goals:**

- A pre-write manifest grouped by file, naming every staged provider, credential, profile, usage
  and decisions value under the file the commit will write it to.
- A **Left at default / unchanged** block for every walk setting this run did not collect or
  skipped, each with the command or file that changes it later.
- A short post-write receipt: one line per touched file, the change-later commands, and the return
  to the session.
- Both surfaces render through the real TUI at 40 columns; every line fits the width, long values
  wrap at word boundaries and are indented, and no secret value appears.
- A test maps every staged write to its file, derived from `SetupWizardCommit`'s own staged state.

**Non-Goals:**

- No new settings keys, no store writer, no public API, no ADR. The manifest describes writes that
  already exist.
- No change to the commit, the staged model's answers, or the abort semantics.
- No new width rig: the manifest renders through the existing scrollback/live-area path and the
  existing `WrappedLiveAreaTests`-style terminal model, using the repo's `CellText.Wrap`.
- No interactive paging component: the manifest is committed scrollback, so a long list is
  readable by scrolling; it never becomes a table or a clipped row.

## Decisions

### 1. A structured write list rendered into lines

Add `SetupWizardManifest` with two record types — `SetupWrite(File, Label, Value)` and
`SetupUnchanged(Label, Value)` — and two renderers. `Writes(model)` walks the same staged answers
`SetupWizardCommit` walks and returns one entry per value with the file the commit uses. The
renderer groups by `SetupFile` in the fixed order `providers.json`, `auth.json`, `settings.json`,
so the output has stable file headings and the mapping test can assert on the data, not on prose.

Alternative considered: keep a single string list and add file prefixes. Rejected because the
mapping test would parse prose, and a label rename would silently break the guarantee the card
asks for.

### 2. The file mapping mirrors the commit, not an independent guess

`Writes` decides the provider's target with the same predicates as `SetupWizardCommit`:
`Provider` not carried, `Custom`, `Credential.Key` typed and `Model` present select the settings
section; otherwise the provider entry is a `providers.json` entry. A typed key is always an
`auth.json` write, a reference is a provider-entry fact, and a carried answer is never a write.
The manifest test walks one rich model and asserts the exact `(file, label, value)` set; the
commit tests already pin the file bytes, so the two cannot drift silently.

### 3. One fact per line, wrapped with `CellText.Wrap`

The renderer emits a file heading at column 0 and each fact as `  label: value` when it fits. When
it does not fit, it emits `  label:` and then the value wrapped at `width - 4`, indented four
spaces. `CellText.Wrap` already breaks on spaces, `/` and `\`, and hard-breaks a token that cannot
fit, so a long URL or model id cannot overflow. This is the same cell-width measure the live area
and the terminal-screen tests use; no second width rig is introduced.

The interactive surface passes `_console.Size.Columns`. The non-interactive `lunate setup` path
has no terminal width, so it renders at 80 columns (the CLI is not the phone-width surface the
card names).

### 4. Left at default / unchanged is derived from the walk, not a settings dump

`Unchanged(model)` lists only the settings the walk owns:

- a step whose answer was not staged (skipped, never reached because the provider was skipped, or
  carried from the current configuration), with its state and the command/file that changes it;
- the four `decisions` switches when the step accepted the defaults, plus the routing keys when
  routing stayed off;
- the fast and decision profiles when their steps skipped.

Nothing outside the wizard's step list is listed. The block answers "what did this run not ask
about, and where do I change it later?", not "what can settings.json hold?". A carried answer is
named "unchanged"; a skipped answer is named "skipped"; an unasked one is named "not set".

### 5. The receipt is the commit's short record

`SetupWizardReceipt.Render(model, width)` groups the staged writes by file and emits one short
line per touched file: the file name and what landed there (`provider openai`, `key for openai`,
`profile main; usages chat`, `decisions`). It then prints the change-later commands
(`/setup`, `/settings`, `lunate auth login`) and `back to the session`. A run that staged no writes
says `setup written: nothing — every step was skipped` instead, so a no-op is never reported as a
write. The receipt never enumerates values, so it stays short on a phone-width terminal; the full
manifest above it remains the confirm gate.

The interactive session writes the receipt to scrollback after the commit; the CLI prints it to
stdout after the commit, replacing the old `written: …` lines. Both use the same renderer, so the
two write paths cannot describe the same commit differently.

## Risks / Trade-offs

- **The manifest grows with a detailed walk.** The 40-column renderer keeps one fact per line, and
  the receipt stays short; the full list is scrollback, not a modal. The user can scroll the
  confirm gate before pressing Enter.
- **The file mapping can drift from the commit.** Mitigated by deriving the mapping from the same
  staged predicates and by a test that walks the rich model and compares the manifest's targets
  with the commit's observable writes.
- **A long provider id or URL eats the label line.** Mitigated by `CellText.Wrap`'s path and hard
  breaks; the test asserts every rendered row fits the width.
- **The unchanged block could imply a default was written.** Its wording names the state
  (`skipped`, `unchanged`, `not set`) and the file that would change it; the pre-write heading says
  nothing has been written yet.

## Migration Plan

No migration. The commit path and file formats are unchanged; only the text rendered before and
after the commit changes. Rollback restores the old summary renderer without touching stored data.

## Open Questions

None. The layout follows the S-8 report's phone-width rule and the existing scrollback path.
