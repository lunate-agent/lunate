# Tasks

Order follows the design's slices; each numbered group is a mergeable PR slice behind the same
proposal, each with tests and a green gate before the next starts. The provider-store entry v2
(families, placements, models URL, headers) is shipped in #474; the wizard consumes it.

## 1. Configured-state check and trigger

- [x] 1.1 `SetupState.Check(settings, providers)` — no provider entry, or no resolvable chat
      profile (`usages.chat` unset, absent, or unbound, and no `model` default); unit tests for
      configured / no-provider / no-profile / both-empty.
- [x] 1.2 Session start opens the wizard only in an interactive TTY when the check applies; print
      mode, ACP, piped stdin, CI and `TERM=dumb` never open it, keep the configure-it refusal with
      exit 2 and a message naming `lunate setup` and its flags; the wizard offer replaces the
      first-run auth-picker offer (not `/auth`).
- [x] 1.3 Welcome header: prints no fix block in the wizard state, keeps the workspace line; the
      credential-missing `/auth` fix is unchanged; tests pin both states.
- [x] 1.4 Fixture test: fresh machine → wizard opens; configured machine → no wizard; print/ACP →
      exit 2 with the flag hint; each at 80 columns through the real component stack.

## 2. Wizard steps — provider, credential, connection test, model, model check

- [x] 2.1 `SetupWizardModel` step state machine (`Step`, collected answers, skipped set); no file
      writes from any step.
- [x] 2.2 Provider step: `SelectList` over presets in `providers.json` order plus the custom row
      (endpoint, API family defaulting to `openai-chat`, credential placement defaulting per family,
      optional models URL, region — shown from the preset, asked for custom; unknown family refused
      by name).
- [x] 2.3 Credential step: when the provider's standard variable is set (`OPENAI_API_KEY`,
      `ANTHROPIC_API_KEY`, `GOOGLE_API_KEY`, `OPENROUTER_API_KEY`, …), offer
      "use `<VAR>` from the environment" first and store `env:<VARIABLE>`; otherwise the masked
      prompt, `env:` / `auth:` reference forms and the `--key-stdin`-equivalent script answer. The
      value never echoes, logs, or reaches `providers.json`/`settings.json`.
- [x] 2.4 Connection test: one discovery request through the discovery client with the injected
      transport; failure text names network / authentication (401, 403) / undecipherable response;
      go-back re-opens the credential step with earlier answers retained; **no automatic retry**;
      offline fixtures for each shape.
- [x] 2.5 Model step: picker over discovery's models with type-ahead search; manual id entry when
      discovery is unavailable or skipped; the resolved API family shown beside the model.
- [x] 2.6 Model check: one minimal real request through the resolved family (short completion for a
      chat profile, one tiny noul for a decision profile), cost stated before it runs, skippable and
      `--no-model-check` on the flag path; a wrong model id or family fails here with the model,
      family and status named, go-back to the model step; skipped/failed is marked unverified in the
      summary; test that discovery success alone does not mark the model verified.
- [x] 2.7 Tests: a scripted fixture walks steps 1–5 including a discovery failure and a model-check
      failure, each with go-back.

## 3. Wizard steps — profiles, decisions, summary, commit

- [x] 3.1 Chat profile step: name (validated like `SettingsStore.Profiles` names) and thinking level
      restricted to the levels the chosen model declares; the question is skipped for a model with no
      reasoning style; writing `profiles.*.reasoningLevel`.
- [x] 3.2 Optional fast and decision profile steps default to skip; a decision profile is offered as
      native Jev with the preset's pinned version when the provider exposes the `systemone` family,
      otherwise as an adapter labelled in the picker and summary; a chosen profile binds
      `decisions.profile` without enabling any consumer.
- [x] 3.3 Decisions step: every switch shown with its `off`/unset default and one explanatory line;
      accepting defaults writes nothing for the layer; when routing is enabled, ask each routing
      profile for a description (what it is for, what it is not, an example) and warn once if one is
      blank.
- [x] 3.4 Summary step: every collected value — including region, model-check result, decision
      profile kind and routing descriptions — and every skipped step shown; nothing written before
      Enter.
- [x] 3.5 Commit: one pass through `AuthStore`, the provider store save path and new
      `SettingsStore` profile/usage writers (`WriteTui`-style read-parse-write preserving untouched
      sections and `schemaVersion`); no wizard-owned JSON emitter.
- [x] 3.6 The abort test: skip everything, abort at the summary, `providers.json`, `settings.json`
      and `auth.json` byte-identical; and the skip-everything-confirm case writes nothing and loads
      as a valid empty configuration.
- [x] 3.7 A settings-writer pinning test fails if the wizard grows a second writer for its keys.
- [x] 3.8 The wizard's custom entry and `auth login --custom` produce the same `providers.json` entry
      for the same answers (one writer, one test).

## 4. `lunate setup` — the CLI and the non-interactive path

- [x] 4.1 `lunate setup` routed in `Cli.Run` next to `auth`; on a terminal it opens the entry menu on
      a configured install and the full walk on a fresh one; committing without changes writes
      nothing.
- [x] 4.2 Non-interactive flags `--provider --endpoint --api-family --credential-placement
      --models-url --credential --key-stdin --model --profile --thinking --fast --as-decision
      --no-test --no-model-check --force`; absent flag = skipped step; no value invented.
- [x] 4.3 Exit codes: 0 after a real commit or a no-op; 2 with the missing answer named when a value
      needs interaction and stdin is not a terminal; the connection test and model check present
      unless `--no-test` / `--no-model-check`.
- [x] 4.4 CI no-TTY test: a scripted `lunate setup` run without a terminal writes the full
      configuration through the store writers and prints no secret; plus the "needs interaction"
      exit-2 case and the flag hint in the message.

## 5. `/setup` and the render contract

- [x] 5.1 `/setup` dispatches in the idle session (`/setup` refused while a turn is running, added
      to the busy-refusal list; command registry entry with description; autocomplete row).
- [x] 5.2 `/setup` on a configured install opens the entry menu (add provider, add profile, change the
      chat profile, decisions, walk everything), prefilled with current values; each entry writes only
      what changed and takes effect for the session's composition like an equivalent `/settings`
      change; a first run keeps the full walk.
- [x] 5.3 Fixture-driven full-walk test at 80 and 60 columns through the real component stack; both
      render trees pinned; no line overflows either width.
- [x] 5.4 Docs: `docs/first-run.md` rewritten around the wizard (first run, `lunate setup`, `/setup`,
      the non-interactive flags, the model check and its cost); `docs/configuration.md` affected keys
      re-checked by the generated settings sync; `README.md` quick start if it names the old sequence.

## 6. Gate

- [x] 6.1 `openspec validate setup-wizard --type change --strict` passes before archive.
- [x] 6.2 `scripts/verify.sh` green on the final slice; PR records the run.
