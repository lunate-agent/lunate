# Tasks — custom-profile-resolves-through-entry

## 1. Red: pin the reported behaviour

- [ ] 1.1 Add the reported case to `ModelProfileResolutionTests`: a custom `local` entry of kind
      `openai` and a profile naming `kimi-k2`, which the shipped catalogue holds only as
      `opencode/kimi-k2`. It fails before the fix with
      `'local' has kind 'openai', but model 'opencode/kimi-k2' belongs to 'opencode'`.
- [ ] 1.2 Add the metadata case: the matching catalogue entry supplies the context window, tool and
      vision support, reasoning and maximum output, and its pricing does not come across.
- [ ] 1.3 Add the family refusal: a `local` entry of kind `openai` naming `claude-sonnet-4-5`
      (provider `anthropic`) is still refused, naming `local`, `openai-chat` and
      `anthropic-messages`.
- [ ] 1.4 Keep the preset-entry refusal cases: a preset whose provider owns the bare id resolves, a
      preset naming another vendor's model is refused by kind, and a preset naming an ambiguous bare
      id still names every candidate.

## 2. Green: resolve a custom entry's model through the entry

- [ ] 2.1 Add the internal helper that decides "custom entry" (the entry's name is not a preset id)
      and builds the entry's own model: id `<entry>/<model>`, provider = entry kind, wire id from the
      match or the profile, facts copied from a single matching catalogue entry, never pricing.
- [ ] 2.2 Replace the kind check with the family check for custom entries in
      `ModelProfileResolver.Resolve`; a preset entry keeps the existing check and the existing
      ambiguity refusal. The family refusal names the entry, the families it serves and the family
      the model speaks.
- [ ] 2.3 Keep the resolution path after the check untouched: family (`ProviderFamilyResolver`),
      endpoint precedence, System One alias refusal, option validation, headers and placement.

## 3. Record: docs, gate and review

- [ ] 3.1 Update `docs/first-run.md`'s profile paragraph to the wider rule: a custom entry's bare
      model id resolves through the entry, a matching catalogue entry supplies model facts only.
- [ ] 3.2 Run the targeted test classes (the resolver's, and every class that references
      `ModelProfileResolver` / `ResolvedModelProfile`), a Release build, `dotnet csharpier format .`
      then `dotnet csharpier check .`.
- [ ] 3.3 Run `openspec validate --all --strict` and `bash scripts/check-openspec-skills.sh`; answer
      the card's acceptance checks by number on the PR.
- [ ] 3.4 Sync the delta specs and archive the change as the final commit of the branch.
