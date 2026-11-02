# Context

Issue #476's check located the shipped thinking precedence:

- resolved once per session from the global `thinking` user setting with the `--thinking` flag over
  it (`SettingsStore.ApplyLaunch`, `SettingsStore.cs:1382`), consumed interactively at
  `InteractiveSession.cs:482`, in print at `PrintMode.cs:748`, in ACP at `AcpMode.cs:370`, and from
  the request side at `AgentHarness.cs:1325` (`ReasoningParameters.Apply`);
- `/thinking` mutates the running session only (`InteractiveSession.Commands.cs:311-323`);
- the profile's `reasoningLevel` / `reasoningBudget` are read, merged and validated
  (`SettingsStore.Profiles.cs:44-64`, `SettingsStore.Project.Merge.cs:102-103`,
  `ModelProfile.cs:93-133`) and applied by nothing.

The intended precedence (the issue): profile default → `/thinking` session-only → `/settings` (and
the wizard) persistent → an unsupported model refuses by name.

# Goals / Non-Goals

**Goals:**
- The chat profile's `reasoningLevel` seeds the session's initial thinking level.
- `/thinking` keeps being session-only and gains the refusal for a model without reasoning support,
  naming the model and the setting.
- `/settings thinking <level>` persists the level into the chat profile.
- One test per rule; the docs paragraph in `docs/configuration.md`; the guide's `/thinking` wording
  agrees.

**Non-Goals:**
- Applying a profile's arbitrary `reasoningBudget` to requests: the request-time parameter is
  `ThinkingLevel` (fixed 2,048/8,192/32,768 budgets in `ReasoningParameters.Apply`); an explicit
  budget path is a `Lunate.Ai` API change, staged with the budget work, not here. The budget stays a
  validated configuration fact.
- The setup wizard: it does not exist yet (#475); the wizard will write the same key once it lands.
- Changing ACP semantics: the editor owns levels there; ACP gets the same seeded default through the
  shared `ApplyLaunch`.
- New settings keys, session-format changes, or `Lunate.Ai` public-API changes.

# Decisions

**Seed inside `SettingsStore.ApplyLaunch`.** It is the one place every mode resolves launch values
over the file (interactive `InteractiveSession.cs:474`, print `PrintMode.cs:109`, ACP
`AcpMode.cs:271`), so all three modes seed identically without per-mode code:

```
Thinking = launch.Thinking ?? SeedReasoningLevel(settings) ?? settings.Thinking
```

`SeedReasoningLevel` returns the chat usage's profile `reasoningLevel` parsed to `ThinkingLevel`
when that profile is configured and the literal is one of `low`/`medium`/`high`; otherwise null.
Relative decisions:

- The launch flag above the profile: `ApplyLaunch` already treats the flag as run-scoped user
  instruction (`launch.Thinking ?? settings.Thinking`), and `--thinking off` is a non-null
  `ThinkingLevel.Off`, so it correctly refuses the profile default for that run.
- The profile above the global `thinking` key: the profile is the more specific fact (the issue's
  wording: "the default for every session using that profile"); the global key keeps seeding every
  session whose profile does not name a level, which preserves today's behaviour there.
- Seeding is applied at session start only. A `/model` switch mid-session does not re-seed; the
  session keeps the level the user has in hand, and `/thinking` and `/settings` stay the two levers.

**`/thinking` refusal.** The set form refuses non-`off` when the session's resolved model declares
no reasoning style (`ModelInfo.Reasoning` is null) with a notice naming the model id and the
`thinking` setting. `inline` models accept the level (reasoning is supported; no provider parameter
is sent, as the shipped bare-notice mapping already says). `off` is always accepted. The session
level and the profile are untouched by a refusal.

**`/settings thinking <level>` persistence.** A new internal store writer
(`SettingsStore.WriteProfileReasoningLevel`) does a read-modify-write over the user settings file —
the same pattern as `WriteTui`: unrelated keys survive, the write is atomic, failures leave the file
intact and keep the session's own change with a saved-failed notice. Semantics:

- The chat usage's profile name must exist **in the user settings file's `profiles` section** to be
  written. A selection-derived profile that is not a configured profile (the `selection-*` names)
  and a profile that comes only from a project overlay are refused with the profile and location
  named, pointing at the settings file — with `/thinking` remaining the per-session lever.
- `<level>` `low`/`medium`/`high` writes `profiles.<chat>.reasoningLevel`; `off` removes the key
  (a profile has no `off`, and removing is how the profile stops forcing a level). The write refuses
  when the session's model declares no reasoning style — the same model-named refusal as `/thinking`
  — because a persisted level for a level-less model would turn into a settings load error on the
  next start.
- The running session applies the new level immediately (same level as the persisted one), so a
  `/settings thinking` write never lies on the footer.

**Wizards and other writers.** None today; #475 writes the same key after this change.

# Risks / Trade-offs

- The global `thinking` key becomes dead for sessions on profiles that name a level. The docs
  paragraph states this explicitly, and the `/settings` display keeps showing the *resolved session*
  level, so what the user sees stays truthful.
- A project overlay can shadow a persisted level (`MergeProfile` prefers the overlay value for
  `reasoningLevel`). Persisting is refused for profiles that exist only as an overlay; a profile
  present in both keeps the overlay as the winner, matching the shipped merge semantics — but the
  persistence write in the user file may be shadowed; the risk note records this, and the docs
  paragraph covers it.
- Seeding at `ApplyLaunch` runs before catalog/model resolution. The seed reads the profile's own
  string, not a resolved model's capability, so an unresolvable profile seeds nothing new: the
  profile's load resolution already fails the session start with a named error before any request.

# Migration Plan

No file or schema migration: `profiles.<id>.reasoningLevel` already exists; sessions without
configured profiles keep today's behaviour bit for bit. Rollback is the revert.

# Open Questions

- None blocking. The `reasoningBudget` application path stays with the budget work, and the wizard
  key write rides #475.
