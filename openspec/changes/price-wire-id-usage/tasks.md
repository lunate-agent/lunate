# Tasks — price-wire-id-usage

## 1. The lookup

- [x] 1.1 Add the internal, never-throwing `ModelCatalog.FindUsageModel(id, provider)`: the
      provider-scoped resolution first, then the catalog-wide rules; `null` for no entry or an id
      that stays ambiguous.
- [x] 1.2 Tests in `Lunate.Ai.Tests/ModelCatalogTests.cs`: a bare wire id served by two presets
      resolves to the selected provider's entry; the same id with no scoped entry stays null when
      other presets make it ambiguous; an unknown id is null.

## 2. The JSON cost

- [x] 2.1 `UsageCost.ForRecord` takes the selected model's provider and resolves through
      `FindUsageModel`; every existing caller keeps its behaviour by passing no provider.
- [x] 2.2 `PrintEventJson.UsageBody` passes the selected catalog model's provider (resolved from the
      fallback model id), so a bare wire id prices as the selected preset.
- [x] 2.3 Tests in `Lunate.Coding.Tests/PrintEventJsonTests.cs`: a run whose selected model's wire id
      differs from its catalog id and a second preset serving that wire id carries the selected
      preset's `costUsd`; a bare wire id outside the selected provider that other presets make
      ambiguous keeps the field absent.
- [x] 2.4 Test in `Lunate.Coding.Tests/RunLimitsCodingTests.cs`: a print-mode `--json` run on such a
      model reports `costUsd` on `usage_updated`, and a `--max-cost` equal to it exits `3` naming
      that spend.

## 3. The cost limit

- [x] 3.1 `AgentHarness.FindModel` resolves through `FindUsageModel` with the selected model's
      provider, so spend and the resumed-spend seed price a wire-id usage.
- [x] 3.2 Tests in `Lunate.Agent.Tests/AgentLimitsTests.cs`: a harness on a model whose wire id
      differs from its catalog id reaches `CostLimitReached` with the catalog cost when the provider
      echoes the wire id; a resumed session recording that wire id keeps its spend.

## 4. Docs and close-out

- [x] 4.1 `docs/json-events.md`: state which provider the `costUsd` lookup resolves against.
- [ ] 4.2 `dotnet csharpier format .`, the fast gate, `openspec validate --all --strict`.
- [ ] 4.3 Sync the delta specs, archive the change as the last commit, add the `changes/<pr>.md`
      changelog line before the archive.
