# Tasks

Order follows the design's slices; each numbered group is a mergeable PR slice behind the same
proposal, each with tests and a green gate before the next starts. #474's provider-store contract is
a dependency for the family/discovery surfaces; slices degrade to manual entry where it is absent.

## 1. Configured-state check and trigger

- [ ] 1.1 `SetupState.Check(settings, providers)` — no provider entry, or no resolvable chat
      profile (`usages.chat` unset, absent, or unbound, and no `model` default); unit tests for
      configured / no-provider / no-profile / both-empty.
- [ ] 1.2 Session start opens the wizard when the check applies, stdin and stdout are terminals and
      the session is not resuming; print mode, ACP, piped stdin and `TERM=dumb` keep today's
      refusals; the wizard offer replaces the first-run auth-picker offer (not `/auth`).
- [ ] 1.3 Welcome header: prints no fix block in the wizard state, keeps the workspace line; the
      credential-missing `/auth` fix is unchanged; tests pin both states.
- [ ] 1.4 Fixture test: fresh machine → wizard opens; configured machine → no wizard; each case at
      80 columns through the real component stack.

## 2. Wizard steps — provider, credential, connection test, model

- [ ] 2.1 `SetupWizardModel` step state machine (`Step`, collected answers, skipped set); no file
      writes from any step.
- [ ] 2.2 Provider step: `SelectList` over presets in `providers.json` order plus the custom row
      (endpoint, API family defaulting to `openai-chat`, credential placement defaulting per family,
      optional models URL; unknown family refused by name).
- [ ] 2.3 Credential step: masked prompt, `env:<VARIABLE>` / `auth:<key>` reference forms, the
      `--key-stdin`-equivalent script answer; the value never echoes, logs, or reaches
      `providers.json`/`settings.json`.
- [ ] 2.4 Connection test: one discovery request through the discovery client with the injected
      transport; failure text names network / authentication (401, 403) / undecipherable response;
      go-back re-opens the credential step with earlier answers retained; offline fixtures for each
      shape.
- [ ] 2.5 Model step: picker over discovery's models with type-ahead search; manual id entry when
      discovery is unavailable or skipped; the resolved API family shown beside the model.
- [ ] 2.6 Tests: a scripted fixture walks steps 1–4 including a test failure with go-back.

## 3. Wizard steps — profiles, decisions, summary, commit

- [ ] 3.1 Chat profile step: name (validated like `SettingsStore.Profiles` names) and thinking level
      (`off|low|medium|high` from the profile schema); writing `profiles.*.reasoningLevel`.
- [ ] 3.2 Optional fast and decision profile steps default to skip; a chosen decision profile binds
      `decisions.profile` without enabling any consumer.
- [ ] 3.3 Decisions step: every switch shown with its `off`/unset default and one explanatory line;
      accepting defaults writes nothing for the layer.
- [ ] 3.4 Summary step: every collected value and every skipped step shown; nothing written before
      Enter.
- [ ] 3.5 Commit: one pass through `AuthStore`, the provider store save path and new
      `SettingsStore` profile/usage writers (`WriteTui`-style read-parse-write preserving untouched
      sections and `schemaVersion`); no wizard-owned JSON emitter.
- [ ] 3.6 The abort test: skip everything, abort at the summary, `providers.json`, `settings.json`
      and `auth.json` byte-identical; and the skip-everything-confirm case writes nothing and loads
      as a valid empty configuration.
- [ ] 3.7 A settings-writer pinning test fails if the wizard grows a second writer for its keys.

## 4. `lunate setup` — the CLI and the non-interactive path

- [ ] 4.1 `lunate setup` routed in `Cli.Run` next to `auth`; on a terminal it opens the interactive
      wizard with existing values as the current ones (re-running allowed); committing without
      changes writes nothing.
- [ ] 4.2 Non-interactive flags `--provider --endpoint --api-family --credential-placement
      --models-url --credential --key-stdin --model --profile --thinking --fast --as-decision
      --no-test --force`; absent flag = skipped step; no value
      invented.
- [ ] 4.3 Exit codes: 0 after a real commit or a no-op; 2 with the missing answer named when a value
      needs interaction and stdin is not a terminal; the connection test present unless
      `--no-test`.
- [ ] 4.4 CI no-TTY test: a scripted `lunate setup` run without a terminal writes the full
      configuration through the store writers and prints no secret; plus the "needs interaction"
      exit-2 case.

## 5. `/setup` and the render contract

- [ ] 5.1 `/setup` dispatches in the idle session (`/setup` refused while a turn is running, added
      to the busy-refusal list; command registry entry with description; autocomplete row).
- [ ] 5.2 `/setup` re-runs the same wizard; confirmed changes take effect for the session's
      composition the same way an equivalent `/settings` change does.
- [ ] 5.3 Fixture-driven nine-step walk at 80 and 60 columns through the real component stack; both
      render trees pinned; no line overflows either width.
- [ ] 5.4 Docs: `docs/first-run.md` rewritten around the wizard (first run, `lunate setup`, `/setup`,
      the non-interactive flags); `docs/configuration.md` affected keys re-checked by the generated
      settings sync; `README.md` quick start if it names the old sequence.

## 6. Gate

- [ ] 6.1 `openspec validate setup-wizard --type change --strict` passes before archive.
- [ ] 6.2 `scripts/verify.sh` green on the final slice; PR records the run.
- [ ] 3.7 The wizard's custom entry and `auth login --custom` produce the same `providers.json` entry
      for the same answers (one writer, one test).
