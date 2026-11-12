## Why

The 0.3.0-rc.1 review (finding N-5, `src/Lunate.Coding/SetupWizardModel.cs:71`) found four small
defects in the setup wizard's user-facing text and the wizard's own answer types: the positional
`WizardCredentialChoice` record prints a typed key in its generated `ToString()`, the `lunate setup`
usage line omits the accepted `--thinking off` value, a chat profile the walk names `decisions` is
replaced by the decision profile without a word, and a failed model check's recorded detail names
neither the API family nor the HTTP status the summary is supposed to show. Each is a one-line
repair in behaviour the shipped specs already describe — except the profile-name clash, which no
requirement covered.

## What Changes

- `WizardCredentialChoice.ToString()` names the credential's reference and marks a typed key as
  redacted instead of printing it; no surface that renders an answer can echo the key.
- The `lunate setup` usage line lists every accepted thinking level — `off|low|medium|high` — as the
  `agent-config` spec's setup command line already requires.
- A walk whose chat profile carries the decision profile's own name (`decisions`) no longer lets the
  decision step write over it: the decision step is skipped with one notice naming the clash, so the
  chat profile and `usages.chat` keep pointing at the profile the user configured.
- A failed model check records a detail naming the model id, the resolved API family and the HTTP
  status or response shape, redacted for the credential as before; the interactive notice and the
  `lunate setup` summary print that same detail, so both entry points name the same three facts.

## Capabilities

### New Capabilities

<!-- none -->

### Modified Capabilities

- `setup-wizard`: three additions — the rendered credential answer never carries a typed key, the
  decision profile never replaces a chat profile that shares its name, and a check failure's detail
  names the model, the family and the status.
- `agent-config`: one addition — the `lunate setup` usage line names every value `--thinking` accepts.

## Impact

- `src/Lunate.Coding/SetupWizardModel.cs` (the credential answer's text, the decision profile's name as
  one constant), `src/Lunate.Coding/InteractiveSession.SetupWizard.cs` and
  `InteractiveSession.SetupWizardUsages.cs` (the decision step's name-clash guard and the notice that
  prints a failure detail), `src/Lunate.Coding/WizardModelCheck.cs` (the failure detail),
  `src/Lunate.Coding/SetupCommand.cs` (the usage line).
- Tests in `tests/Lunate.Coding.Tests` for each of the four: the answer's text, the usage line, the
  profile-name clash through a scripted walk, and the model-check failure on the flag path.
- `docs/quickstart.md` names the decision profile's fixed name where it describes the walk.
- No public API change (every touched type is `internal`), no new package, no session-format change.
