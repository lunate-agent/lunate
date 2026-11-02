# Thinking per profile: the shipped precedence

## Why

The approved profile model (ADR-0027, the provider-store change) gives every model profile an
optional reasoning level and budget, and the issue behind this change records the intended
precedence: the profile's thinking level is the default for every session using that profile,
`/thinking` changes the current session only, `/settings` and the setup wizard persist it, and a
model that does not support reasoning refuses the setting by name.

The check (issue #476) against the shipped code found a different precedence:

- Thinking resolves once per session from the **global** user setting `thinking` (default `off`,
  `SettingsStore.cs:126-127`), with the `--thinking` launch flag overriding it
  (`SettingsStore.ApplyLaunch`, `SettingsStore.cs:1382`). The same resolved value seeds the
  interactive session (`InteractiveSession.cs:482`), print mode (`PrintMode.cs:748`) and ACP
  (`AcpMode.cs:370`), and `/thinking` changes the running session in place
  (`InteractiveSession.Commands.cs:311-323`) without writing anything.
- The profile fields `reasoningLevel` and `reasoningBudget` are read (`SettingsStore.Profiles.cs:44-64`),
  merged from project overlays (`SettingsStore.Project.Merge.cs:102-103`) and validated against the
  selected model when the profile resolves (`ModelProfileResolver.ValidateReasoning`,
  `ModelProfile.cs:93-133`; the refusal names the model and the field). **No request path applies
  them** — the only request-time reasoning parameter is the session level
  (`AgentHarness.cs:1325` → `ReasoningParameters.Apply`).
- `/settings` shows the session level (`InteractiveSession.Settings.cs:96`) and refuses a write with
  a pointer at the `/thinking` command (`InteractiveSession.Settings.cs:230-235`). The setup wizard
  does not exist yet (it is issue #475).

So the shipped precedence is **launch flag > global `thinking` key > off**, and the profile's
reasoning settings are dead beyond validation. That is the gap this change closes.

## What Changes

1. **Profile default applies.** A session whose chat profile names a `reasoningLevel` starts with
   that level as its thinking default; the launch `--thinking` flag still wins for the run, and a
   profile without a reasoning level falls back to the global `thinking` key exactly as today. A
   profile `reasoningBudget` stays a validated configuration fact and is not applied at session
   level — the request-time parameter is level-shaped (`ThinkingLevel`), so an arbitrary explicit
   budget would be a `Lunate.Ai` API addition, recorded as the follow-up for the budget work.
2. **`/thinking` stays session-only and refuses an unsupported model by name.** The set form names
   the model and the `thinking` setting when the model declares no reasoning style, instead of
   silently accepting. `inline` still accepts (reasoning is supported, parameter-less); `off` is
   always accepted. The profile is never touched.
3. **`/settings thinking <level>` persists into the chat profile.** A new store writer records the
   level in the profile's `reasoningLevel` key (`off` removes the key) and the running session picks
   it up; the old pointer-at-`/thinking` refusal is dropped for `thinking`.

The wizard half of the fourth rule belongs to the setup wizard itself (#475): when it lands it
writes the same key through the same precedence.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `provider-store`: a profile's reasoning level is the session's default thinking level for every
  session on that profile, over the global `thinking` key and under the launch flag.
- `tui`: `/thinking` refuses an unsupported model by name and stays session-only; `/settings
  thinking <level>` persists the chat profile's reasoning level instead of pointing at `/thinking`.

## Impact

- `Lunate.Coding`: `SettingsStore.ApplyLaunch` gains the profile-level seed, `/settings`'s `thinking`
  key gains the persistent write (one new internal store writer), `/thinking` gains the named
  refusal.
- `Lunate.Agent` / `Lunate.Ai`: unchanged — the harness keeps consuming `ThinkingLevel`.
- No settings-schema change, no new keys, no public-API change, no session-format change.
- Tests: one per intended rule, in `Lunate.Coding.Tests`; docs: the resolution paragraph in
  `docs/configuration.md` and the guide's `/thinking` wording.
