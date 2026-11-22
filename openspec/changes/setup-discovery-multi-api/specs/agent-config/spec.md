## MODIFIED Requirements

### Requirement: Model discovery helper

`lunate --discover <name-or-url>` SHALL list the models of an OpenAI-compatible endpoint (catalog name resolves endpoint and key reference; a preset name resolves the preset base URL and key variable; a direct URL is used as given) and SHALL print a `models.json`-shaped draft for the user catalog to standard output, without writing any file. A credential SHALL be sent only to an endpoint the provider owns: a preset's base URL, or a catalog entry's endpoint that equals or is under its preset's base URL. A direct URL and a declared third-party endpoint SHALL send no credential unless the catalog entry carries an explicit `auth` reference (then that named key is sent); a 401/403 from such an endpoint SHALL say that no key was sent and name the `auth` opt-in, never a key value. The listing response is `{ "data": [ { "id": ... } ] }`; an entry whose `api` object declares several API families SHALL be read through the model's `recommended_api` when that names one of the declared families and maps to a supported family; otherwise that entry SHALL be skipped and the rest of the listing SHALL still succeed. Unknown metadata (`contextWindow`, `supportsTools`) SHALL be omitted from the draft. A preset target SHALL emit qualified `<provider>/<model>` ids with their `wireId`; a direct URL SHALL keep the bare `openai`-provider draft. HTTP, authentication and payload errors SHALL exit non-zero with a message naming the target and the failure.

#### Scenario: Draft is printed, not written

- **GIVEN** a reachable endpoint listing two models
- **WHEN** `--discover` runs
- **THEN** stdout contains a schema-versioned draft with both ids and no file is created

#### Scenario: Unreachable target

- **GIVEN** an endpoint that answers 503
- **WHEN** `--discover` runs
- **THEN** it exits non-zero and the message names the target and the status

#### Scenario: A preset target resolves its endpoint and key

- **GIVEN** `lunate --discover groq` with `GROQ_API_KEY` set
- **WHEN** discovery runs
- **THEN** it calls the Groq preset base URL with that key and drafts qualified ids

#### Scenario: A multi-API entry follows its recommendation

- **GIVEN** a listing entry that declares `generate-content` and `interactions` and recommends `generate-content`
- **WHEN** discovery parses the listing
- **THEN** the entry is drafted on family `google` with the `generate-content` endpoint, and the other listed models are drafted too

#### Scenario: An unusable multi-API entry is skipped

- **GIVEN** a listing with one entry that declares several API families and recommends no family this client supports, and one ordinary entry
- **WHEN** discovery parses the listing
- **THEN** the multi-API entry is absent from the draft, the ordinary entry is present, and discovery succeeds

