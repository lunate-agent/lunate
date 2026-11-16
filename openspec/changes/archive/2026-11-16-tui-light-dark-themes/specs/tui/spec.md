## MODIFIED Requirements

### Requirement: Theme tokens

Colour use SHALL be routed through a semantic token set — `accent`, `muted`, `error`, `success`, `warning`, `code`, `diff.add`, `diff.remove`, `prompt`, `notice` — with five built-in themes (`default`, `dark`, `light`, `ansi16`, `mono`) and a user file (`~/.lunate/theme.json`) that maps those tokens to an ANSI colour name, a 256-colour index or a hex value. `dark` SHALL paint the same tokens as `default`, so the shipped palette is nameable; `light` SHALL paint dark inks for a light-background terminal. Selecting `light` or `dark` SHALL choose a palette and SHALL NOT change the colour depth the terminal reports — `ansi16` and `mono` remain the depth policies. The renderer SHALL downgrade by the detected colour capability: hex to the nearest 256-colour entry, then to the nearest of the sixteen, then to no colour. An unknown token SHALL fail the load with the valid tokens listed. The existing `NO_COLOR` and capability rules SHALL take precedence over any theme, so a theme can never re-enable colour in a colourless session. Selection order SHALL be `--theme`, then the `tui.theme` setting, then `default`.

#### Scenario: A hex colour is downgraded to the terminal

- **GIVEN** a theme mapping `accent` to `#ff8800` and a 256-colour terminal
- **WHEN** an accented element renders
- **THEN** it renders in the nearest 256-colour entry, not in raw hex escape codes

#### Scenario: NO_COLOR beats the theme file

- **GIVEN** `NO_COLOR` set and a theme that colours every token
- **WHEN** the session renders
- **THEN** no colour escape is emitted anywhere

#### Scenario: An unknown token lists the valid ones

- **GIVEN** a theme file containing `"highlight": "red"`
- **WHEN** it is loaded
- **THEN** the load fails and the message lists the ten valid tokens

#### Scenario: The light selection paints the light palette

- **GIVEN** `--theme light` and a true-colour terminal
- **WHEN** an accented element renders
- **THEN** it renders in the light theme's accent, a dark ink, not in `default`'s accent

#### Scenario: The dark selection paints the shipped palette

- **GIVEN** the `tui.theme` setting set to `dark`
- **WHEN** the session renders a transcript
- **THEN** every token renders exactly as it does under `default`

#### Scenario: A light selection keeps the depth the terminal reports

- **GIVEN** `--theme light` on a terminal that reports true colour
- **WHEN** the session starts
- **THEN** the colour plan is true colour, and under `NO_COLOR` it is no colour

#### Scenario: The theme file overlays the light selection

- **GIVEN** `--theme light` and a `theme.json` that names `accent`
- **WHEN** the session renders
- **THEN** `accent` is the file's colour and the other nine tokens keep the light palette's
