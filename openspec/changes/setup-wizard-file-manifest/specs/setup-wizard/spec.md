## ADDED Requirements

### Requirement: The file manifest and the receipt

The wizard SHALL render its summary as a manifest grouped by the file the confirmed commit will
write: `providers.json` for provider entries, `auth.json` for a typed key, and `settings.json` for
the profiles, usages, decisions keys and the custom provider section a typed key pairs with. Every
provider, profile, usage and decisions setting staged by the walk SHALL name its file under that
file's heading, one fact per line. A carried answer is not a staged write and SHALL appear only in
the unchanged block. The manifest SHALL write nothing; confirming it remains the only write gate.

The manifest SHALL include a **Left at default / unchanged** block naming every setting the walk
did not collect — skipped, carried or left at its default — with the command or file that changes
it later (`/setup`, `/settings`, `lunate auth login`, or the store file).

After a confirmed commit the wizard SHALL render a short receipt: one line per touched file naming
what landed there, the change-later commands, and a return to the session. The receipt SHALL be the
record of the write; the manifest remains the confirm gate. A confirmed run that staged no write
SHALL say that nothing was written instead of naming a file.

The manifest and the receipt SHALL be readable at 40 columns: a fact is one line when it fits,
otherwise a label line followed by its value wrapped at word boundaries and indented two more
spaces. Neither surface SHALL use a table, overflow the width, or contain a secret value; a stored
key SHALL be named by its provider, never shown.

#### Scenario: Every staged value names its file

- **GIVEN** a full walk that stages a preset provider, an environment credential reference, a chat
  profile, a fast usage, a decision profile and non-default decisions switches
- **WHEN** the summary renders
- **THEN** the provider entry and credential reference appear under `providers.json`, the profile,
  usages and decisions keys under `settings.json`, and a typed key would appear only under
  `auth.json`

#### Scenario: A typed custom key names both files it reaches

- **GIVEN** a custom provider with a typed key and a model
- **WHEN** the summary renders
- **THEN** the provider section and its model appear under `settings.json`, the key appears under
  `auth.json` named by the provider, and the manifest maps every staged write to exactly one file

#### Scenario: Unasked settings name how to change them later

- **GIVEN** a walk that skipped the fast and decision profiles and accepted the decisions defaults
- **WHEN** the summary renders
- **THEN** the unchanged block names fast usage, decision profile and each default decisions switch,
  with `/setup` or `/settings` and the store file beside each

#### Scenario: The receipt records the touched files only

- **GIVEN** a confirmed walk that wrote a provider entry, a typed key and a chat profile with the
  chat usage
- **WHEN** the receipt renders
- **THEN** it names `providers.json`, `auth.json` and `settings.json` once each with what landed,
  prints the change-later commands, and returns to the session

#### Scenario: A no-op confirmation writes no file

- **GIVEN** a walk that skipped every step and confirmed the summary
- **WHEN** the receipt renders
- **THEN** it says nothing was written, names no file, and the stores stay untouched

#### Scenario: A secret never appears

- **GIVEN** a walk with a typed credential and a typed key for a custom provider
- **WHEN** the manifest and the receipt render
- **THEN** neither contains the key value, and the typed key is named only as stored under its
  provider in `auth.json`

#### Scenario: Both surfaces fit forty columns

- **GIVEN** a walked configuration with a long custom endpoint, a long models URL and a long routing
  description
- **WHEN** the manifest and the receipt render through the real terminal stack at 40 columns
- **THEN** no rendered row exceeds 40 cells, each fact starts on its own line, and a value that does
  not fit is indented on the lines below its label

## MODIFIED Requirements

### Requirement: Narrow widths and fixtures

The wizard SHALL render correctly at 80, 60 and 40 columns through the real TUI component stack at
the existing narrow-terminal breakpoints, using the same truncation rules the pickers already apply;
no step's prompt, list, or summary SHALL exceed the terminal width. A fixture-driven test SHALL walk
the full ten steps through the real component stack at 80 and at 60 columns and pin both render
trees. The pre-write manifest and the post-write receipt SHALL both render through the same stack at
40 columns, with no row wider than the terminal and no secret value.

#### Scenario: Sixty columns stays readable

- **GIVEN** a fixture run of the full walk with discovery data and entered values
- **WHEN** the walk renders at 60 columns
- **THEN** no summary or list line overflows the width and the pinned render matches the fixture

#### Scenario: Forty columns stays readable

- **GIVEN** a fixture run of the full walk with discovery data and entered values
- **WHEN** the manifest and the receipt render at 40 columns
- **THEN** no row overflows 40 cells, the manifest names each file and setting, and the receipt names
  each touched file once
