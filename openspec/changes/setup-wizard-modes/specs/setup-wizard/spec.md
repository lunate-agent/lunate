## ADDED Requirements

### Requirement: Quick and detailed modes

The first step SHALL offer one setup mode and SHALL default to **Quick**. Both modes walk the same
ten-step list; Quick leaves the detailed-only steps at their defaults instead of asking them.

Quick covers these settings:

1. provider (a preset, or a custom provider's id and endpoint URL),
2. credential (environment reference, stored key, typed key or an `env:`/`auth:` reference),
3. connection test,
4. model,
5. model check,
6. chat profile name.

Quick leaves these settings at their defaults without asking: a custom provider's API family
(`openai-chat`), credential placement (bearer), region (unset) and models URL (unset); the chat
profile's thinking level (unset); the fast usage (not bound); the decision profile (not configured);
and the decisions layer's switches — approvals off, routing unset, questions off, decide tool off —
and its routing descriptions.

Detailed covers every setting in the full walk's steps 1–8: provider and a custom provider's
endpoint, API family, credential placement, region and models URL; credential; connection test;
model; model check; chat profile name and thinking level; fast profile; decision profile; each
decisions switch and routing description.

The mode SHALL be switchable on the first step, and switching SHALL keep every answer already
collected rather than restarting the walk. A scoped `/setup` entry walk SHALL keep its detailed step
range; the mode choice applies to the full walk.

The review (summary) in Quick SHALL carry a **Left at default** block that names every setting
Quick did not ask, the default it left, and the command that changes it later (`/setup`,
`/settings`, or the file path). Quick SHALL NOT hide a default.

#### Scenario: Quick is the first-step default

- **GIVEN** a fresh machine with terminals on stdin and stdout
- **WHEN** the wizard opens
- **THEN** the first step shows the mode as Quick and lists the provider choices under it

#### Scenario: Quick reaches a working configuration

- **GIVEN** a thrown-away home, a scripted discovery response and a scripted model check
- **WHEN** the Quick walk completes and the summary is confirmed
- **THEN** `providers.json`, `auth.json` and `settings.json` hold a provider, a credential reference
  and a chat usage that the next session resolves, and no detailed-only setting was written

#### Scenario: Quick review names every default it skipped

- **GIVEN** a completed Quick walk
- **WHEN** the review renders
- **THEN** the Left at default block names the fast usage, the decision profile, each decisions
  switch and — for a custom provider — the API family, credential placement, region and models URL,
  each with the default left and the command that changes it later

#### Scenario: Detailed reaches every setting Quick hides

- **GIVEN** a model that declares a reasoning style and a fresh machine
- **WHEN** the Detailed walk runs
- **THEN** it asks the thinking level, the fast profile, the decision profile and every decisions
  switch that Quick never asks

#### Scenario: Switching mode keeps answers

- **GIVEN** a walk that has already collected a provider, credential and model
- **WHEN** the user returns to the first step and switches from Quick to Detailed
- **THEN** every collected answer is still present and the walk continues from the next unasked
  detailed setting

#### Scenario: A scoped entry stays detailed

- **GIVEN** a configured install where `/setup` chose "add a profile"
- **WHEN** the entry walk runs
- **THEN** it still walks the profile, fast and decision steps regardless of the remembered mode
