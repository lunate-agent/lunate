## Context

A provider entry has a name and a kind. The kind is a preset id, and the presets are twelve
templates of adapter and defaults (ADR-0027); since ADR-0029 the *family* a model speaks decides the
adapter and the URL, and a model that declares no family takes the provider's only one. A custom
entry — the one the store, the wizard and `auth login` write for a self-declared endpoint — is the
case where the kind carries no information beyond the adapter: `CustomProvider.KindFor` maps both
OpenAI families to `openai`, `anthropic-messages` to `anthropic`, `google` to `google`. The store
refuses a preset id as a custom provider id, so a custom entry's name is never a preset id.

`ModelProfileResolver` predates that: it looks the profile's model up in the catalogue and then
requires `catalogModel.Provider == providerEntry.Kind`. For a custom entry that is wrong twice. It
requires the catalogue to hold the model under a provider whose id happens to equal the adapter
name — true only for the vendor's own models — and it refuses the profile when the catalogue holds
the same bare id under another provider, which is the normal case for a local endpoint serving
`kimi-k2`:

```
Profile 'chat-main' has invalid 'provider': 'local' has kind 'openai',
but model 'opencode/kimi-k2' belongs to 'opencode'.
```

Maintainer decision on #708 is option 1: the profile resolves through the entry, and the check
compares the entry's family instead of its adapter kind; a matching catalogue entry under another
provider supplies metadata only.

What already exists and is reused:

- `ModelCatalog.FindFor(provider, id)` (#675) — the scope-ordered lookup with a candidate list.
- `ProviderEntry.ChatFamilies` — the families an entry serves, System One excluded.
- `ProviderFamilyResolver.Resolve` — profile override, then declared family, then the entry's only
  family; it is what routes the call.
- `CustomProvider.ToModelInfo` — the shape a custom entry's catalogue entry already takes
  (`<id>/<model>`, kind as provider, wire id, family, placement, models URL).

```
profile { provider: "local", model: "kimi-k2" }
    │
    ├─ FindFor("local", "kimi-k2") ──> local/kimi-k2   (the entry's own entry, when the store has one)
    │                                  └─ resolve as-is
    └─ no scoped entry
         ├─ candidates whose wireId is "kimi-k2"  = [opencode/kimi-k2]
         ├─ family check: openai-chat ∈ local's families   ✓
         └─ build local/kimi-k2 from the entry, facts from the one matching entry
```

## Goals / Non-Goals

- Goal: the configuration #708 describes — a custom OpenAI-family endpoint writing
  `profiles.<name> = { provider: "local", model: "kimi-k2" }` — resolves on the next start.
- Goal: the family check stays; a model of a different family is still refused, and the refusal says
  which family the entry serves.
- Goal: preset entries behave exactly as before: kind check, ambiguity refusal, same messages.
- Non-goal: changing `ModelCatalog.Find` or `FindFor`; the ambiguity refusal stays for callers
  without a provider context.
- Non-goal: writing a custom catalogue id at setup time (option 2 of the issue) or warning instead of
  resolving (option 3). The written settings keep the id the person picked.
- Non-goal: changing how a custom entry's own catalogue entry is produced by the wizard or the store.

## Decisions

**1. "Custom" is decided by the entry's name, not by its URL.** A custom entry is one whose name is
not a preset id — exactly the condition the store enforces when it refuses a preset id as a custom
provider id, and what `ProviderPresets.Find(entry.Name) is null` tests. *Alternatives:* comparing
the entry's URL with the preset's (`ProviderEndpoint.IsOwn`) — rejected, because re-pointing
`providers.openai` at a proxy would silently switch the semantics on a URL edit, and an entry that
reuses the vendor's URL would not be custom; persisting a `custom: true` key in the entry — rejected
as a file-format change (the store refuses unknown keys) for a fact the name already fixes.

**2. Resolution builds the model from the entry.** With no scoped catalogue entry, the resolved
`ModelInfo` is the entry's own: id `<entry name>/<model>`, `Provider = entry.Kind`, wire id the
profile's model id (or the matching catalogue entry's wire id when one matched), and the model facts
copied from that matching entry. Endpoint, family, headers and credential placement are then
resolved exactly as today's code does — the entry is still the routing authority. This keeps the
`ChatClientFactory` path identical: a custom entry's model looks exactly like the one
`CustomProvider.ToModelInfo` already injects, so the adapter, the placeholder-credential rule and
the headers behave the same either way.

**3. The family check replaces the kind check for custom entries only.** The catalogue entry's
family — its declared `apiFamily`, otherwise its provider preset's `DefaultFamily` — must be one of
the entry's `ChatFamilies`. A preset entry keeps the kind check, because there the kind *is* the
vendor and a preset profile naming another vendor's catalogue model is still a mistake.
*Alternative:* comparing families for every entry — rejected: it would accept a `moonshotai` entry
naming `gpt-4o` because both speak `openai-chat`, which is a wrong configuration, not an
OpenAI-compatible one.

**4. Metadata, not pricing.** Facts carried over are the ones a request and its validation need and
that belong to the model rather than to a seller: context window, tool and vision support, reasoning
style, maximum output, capabilities, the declared family and the wire id. Pricing is excluded on
purpose: a local endpoint serving a model the catalogue prices under `opencode` must not inherit
opencode's rates and report dollars in `/cost` for a request nobody bills. `ApiType` is excluded for
the same class of reason — it labels the decision method of a provider's own path, not of this
endpoint. A profile that needs a price gets it from the entry's own catalogue id or a `models.json`
entry.

**5. Several matching entries contribute nothing.** When a bare id is three providers' wire id and
all three speak the entry's family, none of them is *the* model this endpoint serves, so resolution
takes facts from none: the model resolves with unknown limits (window `0`, no tool support, no
pricing) as a discovery-only custom entry does today. Picking one would silently apply another
endpoint's limits. *Alternative:* take the first candidate — rejected as a silent wrong answer; the
existing `local/kimi-k2.7-code` path still wins when the store wrote the entry's own catalogue entry.

**6. The list of candidates is filtered by family before the decision.** A bare id whose candidates
span families (one `openai-chat`, one `anthropic-messages`) resolves with the facts of the single
candidate in the entry's family; when none of the candidates is in the entry's family the profile is
refused with the family message rather than with an ambiguity list, because the entry's family is
the fact that matters and it is what the person has to change.

## Risks / Trade-offs

- [A custom profile on an ambiguous bare id now has no context window, so a `contextWindow` or
  `maxOutput` override is refused as "no greater than window 0"] -> Documented behaviour of an
  unknown model: the fix is a `models.json` entry or the entry's own catalogue id, both already
  supported. It is still better than refusing the whole configuration.
- [A profile could now name a catalogue model of another family when the profile sets `apiFamily`
  explicitly] -> The profile override is the user's explicit routing intent and is already the first
  precedence step; the family check applies to what the entry serves, so an override the entry does
  not serve is still refused by the endpoint lookup.
- [The family of a catalogue entry is derived from its provider preset when it declares none] ->
  Same derivation `ProviderPresets.DefaultFamily` already provides, and the shipped catalogue
  annotates every multi-family preset, so the fallback only reaches single-family presets.
- [More than one place compares provider kinds] -> `WizardModelCheck.RunAsync` stages a model when
  `known.Provider != entry.Kind`; that branch only builds the check model and never refuses, so it
  stays. No other kind comparison exists (grep-checked in the apply step).

## Migration Plan

None. No file-format change, no public API change (`ModelProfileResolver` is internal), no session
format change, no golden sessions. Configurations that were refused start loading; preset
configurations resolve exactly as before. Rollback is the revert of one commit.

## Open Questions

None. ADR-0029's "the model carries the family" stays the resolution authority; this change makes
the custom-entry check honour it instead of the old kind equality, so no ADR is superseded.
