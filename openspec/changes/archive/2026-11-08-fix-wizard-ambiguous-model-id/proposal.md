## Why

On the setup wizard's custom-endpoint route, discovery lists bare model ids (that is the wire form such an endpoint serves), and the model catalogue can hold the same bare id under several provider prefixes. Choosing such an id aborted the whole walk with an internal message — `setup failed: Model id 'kimi-k2.7-code' is ambiguous: moonshotai/kimi-k2.7-code, opencode/kimi-k2.7-code, opencode-go/kimi-k2.7-code` — because `WizardModelCheck.ModelFor` looked the id up in the catalogue without the provider the walk already knows it is configuring (#675). The same unqualified lookup fails on `lunate setup --model <bare-id>` and on the next session start, because a written profile's model is looked up unqualified too.

## What Changes

- The wizard's model resolution becomes provider-scoped: a discovered or typed id is resolved as `<walked provider>/<id>` first, then as an exact catalogue id, then — only when the walk's provider owns no such entry — as a bare id across the catalogue.
- A bare id that resolves to exactly one catalogue entry under the walked provider is staged silently (no new prompt); the walk continues to the model check.
- A bare id that no entry of the walked provider serves, but that several catalogue entries hold, offers those qualified ids in a picker; the chosen id is staged.
- A custom endpoint's model is never treated as ambiguous: the entry being configured defines it (`<provider id>/<model>`), so the walk neither asks nor aborts, and the written profile stays resolvable.
- The same resolution replaces every unqualified catalogue lookup in the wizard and the flag path (`WizardModelCheck.ModelFor`, `RunAsync`, `DecisionModelFor`, `SetupCommand`'s model family, the `/setup` entry prefill): no step aborts on an ambiguous id.
- Profile resolution resolves a profile's `model` in its `provider`'s scope before the unqualified lookup, so a profile written against a provider that owns the bare id resolves instead of failing (spec: an explicit `provider/model` resolves through that provider).
- The model check keeps costing exactly the model the walk staged; no new NuGet package, no public API change, no session-format change.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `setup-wizard`: the model step resolves the chosen id against the walked provider's catalogue entry first and asks only where the catalogue holds several unrelated entries; no step in the walk aborts on an ambiguous catalogue id.
- `ai-layer`: the catalogue gains a provider-scoped lookup and profile resolution uses it, so a profile naming a provider that owns a shared bare model id resolves without a qualified id.
- `provider-store`: a profile whose `model` is a bare id its `provider` owns resolves through that provider instead of being refused as ambiguous.

## Impact

- Affected code: `src/Lunate.Ai/ModelCatalog.cs` (provider-scoped lookup), `src/Lunate.Ai/ModelProfile.cs` (`ModelProfileResolver`), `src/Lunate.Coding/InteractiveSession.SetupWizard.cs` (model step), `src/Lunate.Coding/InteractiveSession.SetupWizardEntries.cs` (entry prefill), `src/Lunate.Coding/WizardModelCheck.cs`, `src/Lunate.Coding/SetupCommand.Walk.cs`, and a new wizard model-lookup helper.
- Tests: `SetupWizardModelStepTests`, `SetupWizardAmbiguousModelTests` (new), `WizardModelCheckWireTests`/`SetupWizardModelCheckTests` as touched, `SetupCommandTests`, `Lunate.Ai.Tests/ModelProfileResolutionTests`, plus the classes that reference the touched types.
- No new NuGet packages. Culture-invariant formatting only. No golden session files.
