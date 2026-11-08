## ADDED Requirements

### Requirement: The decision check carries a request identity

The decision-profile check SHALL send its request with a request identity: the current
session's id when the wizard runs inside a session (including `/setup`), and a fresh ephemeral
probe id minted for the check when it runs standalone through `lunate setup`. The identity
SHALL travel through the same resolved profile, provider entry, API family and client path a
session uses for the same profile; the check SHALL NOT patch a provider header at its call
site. A provider whose profile declares no session header SHALL send the same request it sends
today.

#### Scenario: The OpenCode Go decision check passes

- **GIVEN** the `opencode-go` preset and a native `jev-1.13` decision profile
- **WHEN** the decision check runs outside a session through `lunate setup`
- **THEN** the check passes and its request carries a non-empty `x-opencode-session` probe id

#### Scenario: An in-session decision check names that session

- **GIVEN** a session with an id and a header-templated provider chosen through `/setup`
- **WHEN** the wizard runs the decision check
- **THEN** the request on the wire carries exactly that session's id in `x-opencode-session`

#### Scenario: A provider without a session header is unaffected

- **GIVEN** a provider whose profile declares no session header
- **WHEN** the decision check runs
- **THEN** the check passes and the request carries no session header
