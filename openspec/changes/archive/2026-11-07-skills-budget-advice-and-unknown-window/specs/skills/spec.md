## MODIFIED Requirements

### Requirement: The skills index budget scales with the model

The skills index SHALL keep every model-visible skill's full index line while its estimated token count fits the resolved budgets, so a normal-size pack is never truncated by an arbitrary fixed cap. The budgets SHALL derive from the model's context window with the same precedence as the session model: an extension-registered model's declared window, else the configured catalog entry. When the context window is unknown, both budgets SHALL be disabled — the complete index with no skills-index notice and no hard-ceiling warning — and the session SHALL never assume a default window for the budget. The soft threshold SHALL be 2% of the known window and the hard ceiling SHALL be 5%. The `skills.indexBudget` setting SHALL override the computed values: a positive integer estimated-token budget applies a single budget (fitting skills keep their line, overflow above it follows the hard-ceiling behaviour, and the context-window thresholds do not apply), and the literal `unlimited` indexes every skill at any size with no notice and no warning. The setting accepts user and project scope. Below the soft threshold the session SHALL emit no skills-index notice.

#### Scenario: 21 skills on a large-context model are all indexed without warnings

- **GIVEN** a trusted workspace with 21 valid project skills and a model whose context window is large enough that 2% of it exceeds the pack's estimated tokens
- **WHEN** the session starts
- **THEN** every skill's index line carries name, description and path, the footer is absent, and no skills-index notice or warning is emitted

#### Scenario: A pack above the soft threshold stays complete and warns once

- **GIVEN** a trusted workspace whose skill pack indexes above the soft threshold but below the hard ceiling
- **WHEN** the session starts
- **THEN** every skill keeps its full index line, a one-time notice names the index size and the soft threshold, and a later `/context` reports the index size with that advice; the prompt-section entry carries no footer

#### Scenario: The unlimited budget indexes everything in silence

- **GIVEN** a trusted workspace whose skill pack indexes above the computed hard ceiling with `skills.indexBudget` set to `unlimited`
- **WHEN** the session starts
- **THEN** every skill keeps its full index line, no footer is appended, and no skills-index notice or warning is emitted

#### Scenario: A numeric budget replaces the computed thresholds

- **GIVEN** a trusted workspace with a large context-window model and `skills.indexBudget` set to a positive integer below the pack's estimated tokens
- **WHEN** the session starts
- **THEN** the index overflows at that budget with the hard-ceiling behaviour — the names-only footer plus the overflow warning — and the 2%/5% context-window thresholds are not applied

#### Scenario: An unknown context window disables the budgets instead of assuming a default

- **GIVEN** a trusted workspace whose skill pack indexes far above the 2%/5% thresholds of any default window, with no resolvable context window (no configured model, or none whose declared window is known)
- **WHEN** the session starts
- **THEN** the full index is sent with no skills-index notice and no hard-ceiling warning, and the composition never derives thresholds from a 128,000-token default assumption

### Requirement: The soft threshold notices and advises, never truncates

Above the soft threshold the index SHALL remain complete — the threshold only informs: session start SHALL emit a one-time notice per composition that names the index size and names the `skills.indexBudget` setting (a positive estimated-token budget or the literal `unlimited`, user scope) as the only shipped control, and `/context` SHALL report the index size with the same setting-naming advice as long as the index fits below the hard ceiling. Because no command or extension currently disables a discovered skill or package, the notices SHALL NOT instruct a disable action.

#### Scenario: /context advises at an oversized but complete index

- **GIVEN** a session whose skills index is complete but above the soft threshold
- **WHEN** `/context` is submitted
- **THEN** the notice includes the skills count and index size and names `skills.indexBudget` as the control, and no model call happens

#### Scenario: The notices name the setting and never a disable action

- **GIVEN** a session whose complete index measures above the soft threshold, and one whose index crosses the hard ceiling
- **WHEN** the session starts
- **THEN** the soft notice and the hard-ceiling warning name `skills.indexBudget` and neither names a disable command or a package action that does not exist
