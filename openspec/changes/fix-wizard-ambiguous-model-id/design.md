## Context

The setup wizard walks one provider entry, one credential, one discovered model and one profile, then writes them through the store writers. Discovery for a custom endpoint returns the endpoint's own bare wire ids (`kimi-k2.7-code`); the shipped catalogue stores non-native models as `<provider>/<model>` with the upstream id in `wireId`, so one bare id can be several entries' wire id. `ModelCatalog.Find` refuses that on purpose — a bare id with no provider context must never guess — and `WizardModelCheck.ModelFor` called it before it took the custom-endpoint branch, so the walk died (#675). The same unqualified lookup sits behind `lunate setup --model <bare id>` and behind profile resolution on the next start.

The walk always knows the provider it is writing. That is the context the lookup was missing.

```
InteractiveSession.SetupWizard ─┐
lunate setup (SetupCommand) ────┼─> WizardModelLookup ─> ModelCatalog.FindFor(provider, id)
WizardModelCheck ───────────────┘                              │
                                                               ├─ <provider>/<id>  (the walked entry)
Session start: ModelProfileResolver ───────────────────────────┤─ exact id
                                                               └─ single bare-wire-id entry, else candidates
```

## Goals / Non-Goals

- Goal: a walk never aborts on a catalogue id; the staged id is what the person picked or what the catalogue resolves it to in the walked provider's scope.
- Goal: the configuration the walk writes resolves on the next start without a qualified id.
- Non-goal: changing `ModelCatalog.Find`'s refusal for callers with no provider context.
- Non-goal: rewriting existing catalogs, profiles or the discovery draft shape.

## Decisions

**1. A provider-scoped lookup in `Lunate.Ai`, not wizard-local glue.** `ModelCatalog` gains an internal `FindFor(provider, id)` that answers `<provider>/<id>`, then the exact id, then the single entry whose `wireId` the id is, and otherwise the candidate list (no throw). The wizard needs the candidates for its picker; `ModelProfileResolver` needs the match. Keeping one implementation in the catalogue keeps the wizard and the session resolving a profile the same way. *Alternative:* a bare-id branch inside `ModelFor` — smaller, but leaves the flag path and the next session still broken.

**2. The walked provider's own entry wins, so a custom endpoint never asks.** A custom endpoint's catalogue id is `<provider id>/<model>` by construction (`CustomProvider.CatalogName`), so its bare listing id is never ambiguous in that flow: the wizard stages the id as listed and lets the model check decide. The picker only appears for a provider that has no matching entry and where several unrelated entries hold the id — the case where the person has to say which catalogue model they mean.

**3. Profile resolution reads the model in its provider's scope before the unqualified lookup.** The provider-store spec already promises that an explicit `provider/model` resolves through that provider; a profile names both. The exact id still wins over the fallback so existing profiles resolve exactly as before, and a bare id that no provider entry owns keeps the refusal that names every candidate.

**4. The wizard stages the id it was given; it does not rewrite catalogue ids.** `--model` and a picked listing row stay what the person chose. Resolution belongs to lookups, not to the write, so the summary and the stored settings show the id the person saw.

## Risks / Trade-offs

- [A picker choice names a catalogue model of another provider, so the check runs against the walked provider and may fail] -> The model check is the gate the walk already has: it names the model, the family and the status, and offers going back to the model step. The alternative (rebinding the provider entry to the chosen catalogue model) would invent a provider answer nobody gave.
- [Provider-scoped profile resolution silently resolves an id that used to be refused] -> Only when the profile's own provider owns the id, which is exactly the configuration the wizard now writes; a bare id no provider entry owns still names every candidate.
- [`FindFor` scans `Models` for the bare case] -> The catalogue is hundreds of entries and the call is per walk or per profile load, not per request; `Find` keeps its dictionary fast path.

## Migration Plan

None. No file format changes, no golden session files, no public API change; existing profiles and catalogs resolve as before, and only the previously failing configurations start to load.

## Open Questions

None.
