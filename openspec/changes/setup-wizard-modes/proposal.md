## Why

Today's first run walks every step at the same depth: a user who only wants to be running still
meets the fast/decision profile and decisions questions, while the settings those steps default
silently are never named. #682 separates the two intents on one spine: a quick path for a working
configuration, and a detailed path that asks every setting.

## What Changes

- The first step offers a setup mode: **Quick** (the default) or **Detailed**; switching mode keeps
  every answer already collected.
- Quick asks provider, credential, connection test, model, model check and chat profile. Fast and
  decision profiles, the decisions layer and thinking level stay at their defaults; a custom
  provider takes its endpoint only, with family, placement, region and models URL left at defaults.
- Detailed asks every setting in spec steps 1–8, including custom provider fields, thinking level,
  fast and decision profiles, each decisions switch and routing descriptions.
- Quick's review adds a **Left at default** block naming every setting it did not ask, its default
  value and the command that changes it later.
- The `setup-wizard` spec gains a requirement listing each mode's settings; `docs/first-run.md`
  explains both modes.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `setup-wizard`: adds a quick/detailed mode requirement that names the settings each mode covers
  and the review's left-at-default block; the existing full-walk requirement becomes the detailed
  mode.

## Impact

`src/Lunate.Coding` (`SetupWizardModel`, the wizard driver and summary), the setup-wizard tests and
fixtures, and `docs/first-run.md`. No new package, public API, settings key or store format.
