## Why

The 0.3.0 review found the family set disagreeing across the provider-store spec, the ai-layer spec, the settings schema and the code. `systemone` is a real fifth family in the decision layer and in `ProviderFamilyResolver`, while the specs and schema still described four.

## What Changes

- Name `systemone` as the fifth API family in the provider-store and ai-layer requirements.
- State that `systemone` selects the native System One decision transport, not a chat client.
- Regenerate the settings reference from the schema descriptions.
