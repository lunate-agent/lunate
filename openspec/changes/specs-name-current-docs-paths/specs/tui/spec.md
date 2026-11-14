## RENAMED Requirements

- FROM: `### Requirement: The guide's key table matches the catalog`
- TO: `### Requirement: The keybindings reference table matches the catalog`

## MODIFIED Requirements

### Requirement: The keybindings reference table matches the catalog

`docs/keybindings.md`'s key table and `KeyBindingCatalog` SHALL be checked against each other by a test that fails in both directions: every key in the catalog SHALL appear in the table, and every key in the table SHALL have a catalog row. The check SHALL read only the table under the page's `**Key bindings (one meaning each)**` heading and only its Key column, so a key named inside an Action cell is not a documented binding. A Key cell MAY list several keys separated by `/`, each spelled exactly as `KeyPatternFormatter` writes it, and a merged cell passes when every key in it is covered. Exempt from the table-to-catalog direction SHALL be exactly the panel-owned approval decisions `y`, `n` and `a`, the prompt-gated `?` whose empty-idle condition is not a catalog context, and the deliberately unbound `Ctrl+P`, each carried in the test's exemption table with a one-line reason; a new exemption is a change to this requirement. The check SHALL fail when the heading, the table or its `| Key | Action |` header is missing, so deleting the table cannot pass it, and each failure SHALL name the key, the direction and the row: `docs/keybindings.md documents 'Ctrl+Left' and no catalog row has it (row: 'Alt+B / Alt+F' — Move back / forward by word)` for a ghost and `KeyBindingCatalog's 'Ctrl+Enter' (Insert a newline) has no docs/keybindings.md row` for a gap.

#### Scenario: A catalog key the table omits fails

- **GIVEN** a catalog row whose key has no cell in the table
- **WHEN** the check runs
- **THEN** it fails naming that key and the catalog's description of it

#### Scenario: A table key with no catalog row fails

- **GIVEN** a table cell naming `Alt+R`, which no catalog row carries
- **WHEN** the check runs
- **THEN** it fails naming `Alt+R`, the direction and the row that documents it

#### Scenario: Merged cells are read key by key

- **GIVEN** a cell reading `Ctrl+A` / `Ctrl+E` and another reading `Alt+Enter` / `Shift+Enter` / `Ctrl+J` / `Ctrl+Enter`
- **WHEN** the check runs
- **THEN** every key in both cells is covered, and a key named only in an Action cell counts for nothing

#### Scenario: The exemptions are exact

- **GIVEN** `y`, `n`, `a`, `?` and `Ctrl+P` in the table with no catalog row
- **WHEN** the check runs
- **THEN** they pass with their stated reasons — the approval letters decide locally, `?` shows the approval legend and the empty-prompt help, `Ctrl+P` is deliberately unbound — and any other table key without a catalog row fails

#### Scenario: A deleted table fails instead of passing vacuously

- **GIVEN** a page whose key heading, table or header row is gone
- **WHEN** the check runs
- **THEN** it fails naming what is missing, and does not report success
