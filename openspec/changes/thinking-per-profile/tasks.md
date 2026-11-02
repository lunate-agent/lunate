# 1. Store: the profile level seeds the session default

- [ ] 1.1 Add `SettingsStore`-side seeding so `ApplyLaunch` resolves `launch.Thinking ?? profile reasoningLevel ?? settings.Thinking` (the chat usage's configured profile; `low`/`medium`/`high` literals only; `off` stays falsy in a profile).
- [ ] 1.2 Test-first: one test proving the profile default applies to an interactive session (footer at start, and the request carries the effort), one proving the launch flag wins over the profile, one proving the profile wins over the global `thinking` key, one proving the global key still fills where the profile is silent.

# 2. `/thinking`: session-only and refusing an unsupported model by name

- [ ] 2.1 Refuse a non-`off` level on a model that declares no reasoning style, naming the model id and the `thinking` setting; `inline` and `off` keep working.
- [ ] 2.2 Test-first: the refusal names the model and the setting, changes nothing, and never starts a run; a test proving the profile and the settings file survive `/thinking` unchanged (and a later session re-seeds the profile level).

# 3. `/settings thinking <level>` persists the chat profile

- [ ] 3.1 Add the read-modify-write store writer for the chat profile's `reasoningLevel` (`off` removes the key; unrelated keys survive; atomic; failed write keeps the file and the session's own change).
- [ ] 3.2 Wire `/settings thinking <level>`: refuse while busy, refuse a chat selection that is not a configured profile in the user file (naming profile and location) and a model without reasoning style, apply to the running session, then write.
- [ ] 3.3 Test-first: the persistence test (write, then a later session starts at the persisted level), the refusal tests, and the `off`-clears-the-key test.

# 4. Docs

- [ ] 4.1 `docs/configuration.md`: one paragraph stating where thinking resolves from (launch flag > chat profile `reasoningLevel` > global `thinking` > off; `/thinking` session-only; `/settings thinking` persists; unsupported model refuses by name).
- [ ] 4.2 `docs/guide.md`: the `/thinking` and `/settings` wording agrees with that paragraph (the setup wizard half of the persistence rule rides issue #475).

# 5. Gate and archive

- [ ] 5.1 `openspec validate thinking-per-profile --strict`, a Release build of the touched project and test project, the covering test classes, `dotnet csharpier check .`.
- [ ] 5.2 Sync the deltas into `openspec/specs/` and archive the change in the PR's final commit (`openspec sync` / `openspec archive thinking-per-profile --yes`).
