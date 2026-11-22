## MODIFIED Requirements

### Requirement: System prompt and project instructions

The agent SHALL compose its system prompt from an embedded `system-prompt.md` template: identity, the available tools by name, and the working rules (read before edit, verify after change). The composer SHALL append the runtime facts (OS, resolved shell, canonical working directory, UTC date) and the contents of every `AGENTS.md` found from the repository root (worktree root when there is no repository) down to the working directory, in root-to-leaf order, each block introduced by its path. It SHALL then append the project notes file, `LUNATE.md`, from the repository root (the worktree root when the repository root is not an ancestor of the worktree), under its own heading: when both files exist, the `AGENTS.md` content comes first and its rules win if a note disagrees, because notes carry facts and `AGENTS.md` carries rules. A missing notes file SHALL add no block. The notes block SHALL be bounded to the first 8,000 characters; above that it SHALL carry a marker naming the omitted character count and pointing at `LUNATE.md` for the rest. When `LUNATE_DATE` is set to an ISO `yyyy-MM-dd` date, the composer SHALL use it as the prompt's UTC date; an invalid value SHALL fail with an error naming the variable and the expected format. The composed prompt SHALL stay under 1,000 tokens (asserted with a documented heuristic), excluding user `AGENTS.md` and `LUNATE.md` content. The composed prompt SHALL be delivered through the harness system prompt so that it leads the model request as its system message.

#### Scenario: Composed prompt leads the request

- **GIVEN** a harness configured with the composed prompt and a fake model client
- **WHEN** a run starts
- **THEN** the request's first message is the composed system prompt

#### Scenario: AGENTS.md chain is appended root to leaf

- **GIVEN** a repository with `AGENTS.md` at the root and in a nested directory, running in that directory
- **WHEN** the prompt is composed
- **THEN** both files appear, root first, each under its relative path

#### Scenario: Project notes follow the rules

- **GIVEN** a repository whose root carries `AGENTS.md` and `LUNATE.md`
- **WHEN** the prompt is composed from a nested working directory
- **THEN** the `AGENTS.md` blocks appear first and the notes block appears after them, headed as the project notes file from the repository root

#### Scenario: Missing notes file adds nothing

- **GIVEN** a repository with an `AGENTS.md` and no `LUNATE.md`
- **WHEN** the prompt is composed
- **THEN** no notes block or notes heading appears

#### Scenario: The notes read budget is explicit

- **GIVEN** a `LUNATE.md` longer than 8,000 characters
- **WHEN** the prompt is composed
- **THEN** the notes block carries the first 8,000 characters and a marker stating how many characters were omitted and naming `LUNATE.md` for the rest

#### Scenario: Budget holds with an empty chain

- **GIVEN** no `AGENTS.md` and no `LUNATE.md` files
- **WHEN** the prompt is composed
- **THEN** it measures under 1,000 tokens by the documented estimator

#### Scenario: Pinned date for deterministic sessions

- **GIVEN** `LUNATE_DATE=2026-10-18`
- **WHEN** the prompt is composed
- **THEN** the prompt carries `2026-10-18` as its UTC date

#### Scenario: Invalid date pin fails actionably

- **GIVEN** `LUNATE_DATE` set to a non-date value
- **WHEN** the prompt is composed
- **THEN** composition fails with an error naming `LUNATE_DATE` and the expected format
