## ADDED Requirements

### Requirement: The model step resolves the id in the walked provider's scope

The model step SHALL resolve the chosen model id against the provider entry the walk is configuring before it resolves anything else: the catalogue entry `<provider id>/<model id>` first, then the exact catalogue id, then the single catalogue entry whose wire id the chosen id is. A discovered listing of a custom endpoint SHALL keep serving bare wire ids, and such an id SHALL never abort the walk. Where the walked provider's own entry answers, the walk SHALL stage the id without an extra prompt; where it does not, and the catalogue holds the id under several providers, the walk SHALL offer those qualified ids in a picker and stage the chosen one. Where nothing in the catalogue matches, the walk SHALL stage the id as given and let the model check decide. No step of the walk SHALL fail with an internal ambiguity message.

#### Scenario: A custom endpoint's bare id that several catalogue providers serve

- **GIVEN** a custom endpoint whose discovery listing offers `kimi-k2.7-code`, which the catalogue holds as `moonshotai/kimi-k2.7-code`, `opencode/kimi-k2.7-code` and `opencode-go/kimi-k2.7-code`
- **WHEN** the walk stages that id at the model step
- **THEN** the walk continues to the model check without a prompt, and the summary's model is `kimi-k2.7-code`

#### Scenario: An id one catalogue entry of the walked provider answers

- **GIVEN** the `moonshotai` preset and a discovery listing that offers `kimi-k2.7-code`
- **WHEN** the walk stages that id at the model step
- **THEN** it stages it without a picker, and the model check resolves `moonshotai/kimi-k2.7-code`

#### Scenario: An id several unrelated catalogue providers serve

- **GIVEN** the `openai` preset and a model id the catalogue holds under `moonshotai`, `opencode` and `opencode-go`
- **WHEN** the walk stages that id at the model step
- **THEN** the step offers `moonshotai/kimi-k2.7-code`, `opencode/kimi-k2.7-code` and `opencode-go/kimi-k2.7-code` and stages the one the user picks

#### Scenario: The flag path takes the same resolution

- **GIVEN** `lunate setup --provider local --endpoint <url> --model kimi-k2.7-code`
- **WHEN** the walk resolves the model's API family and runs its checks
- **THEN** it completes and exits 0 instead of failing with an ambiguity error

#### Scenario: The written configuration resolves on the next start

- **GIVEN** a walk that staged a custom endpoint with the bare id `kimi-k2.7-code`
- **WHEN** the confirmed configuration is loaded for the next session
- **THEN** the profile resolves that id through its own provider entry, not through the whole catalogue
