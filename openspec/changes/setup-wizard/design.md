# Design: setup wizard

## Context

The first run today has three disconnected mechanisms: the welcome header's one-fix line
(`WelcomeHeaderRenderer.cs` / `ProviderKeyProbe.FixFor`), the in-session auth picker
(`InteractiveSession.AuthPicker.cs`) that stores only a credential, and the CLI (`lunate auth login`,
`auth login --custom`) that stores one credential or one custom OpenAI-compatible endpoint
(`AuthCommand.Login.cs`, `CustomProvider.cs`, `CustomProvider.cs` written through
`SettingsStore.WriteProvider`). Nothing helps a new user cross the rest of the distance — a profile
in `settings.json` that binds provider, model and (per #476) a thinking level to a usage — and no
step verifies the credential against the real endpoint before the user leaves. The provider store and
the profile schema that the wizard needs already exist (ADR-0027, schema in
`src/Lunate.Coding/settings-schema.json`: `profiles.*.provider`, `profiles.*.model`,
`profiles.*.reasoningLevel`); the API-family model it surfaces is shipped (ADR-0029; entry v2 with
`baseUrls` per family, `credentialPlacement`, optional `modelsUrl` and headers, #474 slices 1-3).

## Goals / Non-Goals

**Goals:**

- A zero-config interactive run opens a wizard that ends with `providers.json`, `auth.json` and
  `settings.json` in a state a request can succeed against — or sends the user away with both files
  untouched.
- Every step skippable and re-runnable (`lunate setup`, `/setup`), with the summary naming what was
  skipped.
- One connection test that reads like the discovery the session itself will make.
- A non-interactive equivalent so CI and scripts never depend on the wizard being the only way in.

**Non-Goals:**

- No settings-schema change: the profile, usage and `decisions` keys it writes are shipped; this is
  a consumer, which is why no ADR accompanies the change.
- No new credential storage: keys go through the existing `AuthStore`; the wizard never writes a
  resolved secret to `providers.json` or `settings.json` (unchanged rule).
- No replacement of `/settings`: the wizard's writes are `/settings`' writes.

## Decisions

### 1. One configured-state check answers three entry points

`SetupState.Check(settings, providers)` answers *"does the wizard apply, and why"* — no provider
entry, or no resolvable chat profile — from configuration alone. Three callers consume it: the
session start (open the wizard), the welcome header (print no fix when the wizard will run), and
`lunate setup`. One check, so the header's fix text, the wizard and the CLI can never disagree about
what "not configured" means.

### 2. The wizard is a state machine driven by the existing interaction stack

The wizard is a component model (`SetupWizardModel`) plus a driver that runs inside the session's
live area exactly as the auth picker does — `SelectList` for choices, the masked prompt for
credentials, `SelectListQuestion` for the decisions switches — with no nested read loop, no alternate
screen. States are `(Step, Collected, Skipped)`; answers are staged in memory only. This is what
makes the fixture-driven nine-step walk at 80 and 60 columns a test of the real component stack:
the same renderers a session uses render the wizard.

```
flowchart LR
    S[InteractiveSession start / lunate setup / setup command] --> C{SetupState.Check}
    C -- no provider or no chat profile --> W[SetupWizardModel]
    W --> P[Provider step: presets + custom (family, placement, models URL)] --> K[Credential step: masked prompt or env: reference]
    K --> T[Connection test: discovery client] -- failure, go back --> K
    T --> M[Model step: picker with search text or manual id] --> F[Profile step: name + thinking level]
    F --> O[Optional fast/decision profiles] --> D[Decisions switches: SelectListQuestion]
    D --> SU[Summary: every value, nothing written]
    SU -- confirmed --> WR[(SettingsStore / ProviderStore / AuthStore writers)]
    SU -- Esc / Ctrl+C --> X[(no file changed)]
```

### 3. The connection test is the discovery call the session would make

The test performs one real discovery request through the same discovery client `/discover` uses
(`DiscoverCommand` today, the per-model metadata client shipped in #474) with the credential the
credential step resolved. The transport and clock are injected (`AuthCommandSeams`-style), so
fixtures exercise every failure shape offline. A failure prints what failed — network, HTTP status
(authentication named at 401/403), or an undecipherable response — and offers *go back to the
credential step* before anything else; `Esc` at that notice is *stop the wizard*, never *abort the
session*. Skipped or failed tests do not block later steps: the model step degrades to manual id
entry and the summary marks the test skipped/failed.

### 4. Writes go through the store writers, staged in memory

The commit step calls, in one pass: `AuthStore` for the key; the provider store's save path for the
provider entry (`ProviderStore.Save` — the same writer `auth login --custom` uses through
`SettingsStore.WriteProvider`); and new `SettingsStore` profile/usage writers that follow the
`WriteTui` pattern — read the file, apply the parsed change through the same parser, write the JSON
tree back with the file's `schemaVersion` and every untouched section preserved. There is no wizard
emitter, and a confirmed run whose staged changes are all skips writes nothing at all (a valid but
empty configuration — the current files are already valid, so "skip everything" cannot be an error
and cannot be a rewrite). The stomp risk — a `/settings` write racing a wizard commit — is bounded
to the wizard's own window and accepted: an interactive wizard run and a simultaneous settings edit
are not one session's flow; the store writers verify-and-replace as they do today.

### 5. The non-interactive path is flags, not a second format

`lunate setup --provider <id> [--endpoint <url>] [--api-family <family>]
[--credential-placement <placement>] [--models-url <url>] [--credential <ref>|--key-stdin]
--model <id> --profile <name> [--thinking <level>] [--fast] [--as-decision] [--no-test] --force`
mirrors the naming the card asks for and the conventions `auth login --custom` already established —
same family, placement and models-url flags, defaulting a custom endpoint to `openai-chat` so an
existing custom provider is never silently re-routed: values as
flags, the secret on stdin (`--key-stdin`), exit 2 for "this answer needs interaction or a flag",
exit 0 only after a real commit or a no-op with everything skipped. A settings file (`--from
<file>`) was the alternative; flags win because they need no file format, no loader and no new schema,
and the settings writers are already the machine-readable contract.

### 6. Narrow widths come for free from the components, and are pinned anyway

Sixty and eighty columns are inside the existing narrow-terminal breakpoints the pickers already
honour: descriptions truncate, list windows bound themselves, and the summary wraps. The wizard adds
no width-aware layout of its own; the fixture-driven test walks the nine stepping inputs at 80 and
60 columns through the real stack and pins both render trees, so a component regression cannot show
the wizard readable at one width and broken at the other.

## Risks / Trade-offs

- **The wizard can feel like a wall on re-runs.** Mitigation: `/setup` re-runs it intentionally;
  the trigger never fires when configuration resolves, and Esc at the first prompt is always an exit.
- **The connection test sends the credential to the endpoint.** By design — it is the same call the
  session will make — but it must never print the key, log it, or put it in a process argument; the
  existing redaction tests extend to the wizard's outputs.
- **Entry v2 is the writer's contract, not a wizard concern.** The custom step collects the family
  (default `openai-chat`), the credential placement (default bearer; `x-api-key` for an Anthropic
  endpoint) and an optional models URL, and hands them to `SettingsStore.WriteProvider` — the same
  writer `auth login --custom` uses, so the two entry points cannot drift. An unknown family is
  refused by name before anything is written; discovery stays inside the configured host. Steps
  degrade to manual model entry where discovery is absent.

## Migration Plan

No data migration: the wizard writes schema versions that already exist and readers already accept.
Rollback is trivial — the wizard is additive; removing it returns the session to today's first-run
fix line and auth picker.

## Open Questions

- Exact summary layout and copy (one value per line vs. grouped) — spec pins content, not layout;
  layout lands with the fixture render and review.
- Whether the connection test retries transient network failures once (the decision layer retries a
  malformed response once) or fails immediately with go-back. Proposed: no automatic retry — the
  go-back loop is the retry the user controls.
