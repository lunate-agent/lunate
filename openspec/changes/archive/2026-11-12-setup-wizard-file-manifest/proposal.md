# Setup wizard: a file manifest before the write and a receipt after it

## Why

The wizard's review names values but not the file each value lands in. A user who confirms the
walk sees `provider: openai` and `chat profile: main` in one long line, with no way to tell that
the provider entry goes to `providers.json`, the typed key to `auth.json`, and the profile and
usage to `settings.json`. At phone width the long parenthesised lines reflow into a wall, so the
one screen that is meant to be the write gate is the hardest one to read. The S-8 comparison
(`docs/spikes/S-8/report.md`) found the same gap: `openspec init` reports the files it wrote and
the next command, while Lunate's summary reports neither.

## What Changes

- The pre-write summary becomes a manifest grouped by the file it will touch — `providers.json`,
  `auth.json`, `settings.json` — with one fact per line (a short label and its value), so every
  provider, profile, usage and decisions setting names the file it lands in.
- A **Left at default / unchanged** block names the settings the walk did not collect or skipped,
  with the command or file that changes each one later (`/setup`, `/settings`,
  `lunate auth login`).
- A short post-write receipt replaces the scattered "written: …" lines: one line per file with
  the change-later commands and a return to the session. The full manifest remains the confirm
  gate; the receipt is the record.
- The manifest and the receipt render through the real terminal stack at 40 columns. Long values
  wrap at word boundaries and are indented; there are no tables and no secret values.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `setup-wizard`: the summary step becomes a file-grouped manifest, a receipt follows the write,
  and the narrow-width contract adds 40 columns to the existing 80/60 fixtures.

## Impact

- Code: `src/Lunate.Coding/SetupWizardModel.cs` (unchanged state), a new manifest/receipt renderer,
  `InteractiveSession.SetupWizardSummary.cs` and `SetupCommand.Walk.cs` consume it.
- Tests: the existing summary, commit and width tests move to the new lines; new tests map every
  staged write to its file, pin both surfaces at 40 columns, and assert a secret never appears.
- Docs: `docs/first-run.md` describes the file manifest and the receipt.
- No new NuGet packages, no settings-schema change, no public API change, no new ADR: the manifest
  is a rendering of the staged model the existing store writers already commit.
